﻿using Advanced_Combat_Tracker;
using FFXIV_ACT_Plugin;
using Newtonsoft.Json;
using RainbowMage.OverlayPlugin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Cactbot {

  public interface ILogger {
    void LogDebug(string format, params object[] args);
    void LogError(string format, params object[] args);
    void LogWarning(string format, params object[] args);
    void LogInfo(string format, params object[] args);
  }


  public class CactbotOverlay : OverlayBase<CactbotOverlayConfig>, ILogger {
    private static int kFastTimerMilli = 16;
    private static int kSlowTimerMilli = 300;
    private static int kUberSlowTimerMilli = 3000;

    private SemaphoreSlim log_lines_semaphore_ = new SemaphoreSlim(1);
    // Not thread-safe, as OnLogLineRead may happen at any time. Use |log_lines_semaphore_| to access it.
    private List<string> log_lines_ = new List<string>(40);
    // Used on the fast timer to avoid allocing List every time.
    private List<string> last_log_lines_ = new List<string>(40);

    // When true, the update function should reset notify state back to defaults.
    bool reset_notify_state_ = false;

    private StringBuilder dispatch_string_builder_ = new StringBuilder(1000);
    JsonTextWriter dispatch_json_writer_;
    JsonSerializer dispatch_serializer_;

    private System.Timers.Timer fast_update_timer_;
    // Held while the |fast_update_timer_| is running.
    private SemaphoreSlim fast_update_timer_semaphore_ = new SemaphoreSlim(1);
    private FFXIVProcess ffxiv_;
    private FightTracker fight_tracker_;
    private WipeDetector wipe_detector_;
    private System.Threading.SynchronizationContext main_thread_sync_;

    public delegate void GameExistsHandler(JSEvents.GameExistsEvent e);
    public event GameExistsHandler OnGameExists;

    public delegate void GameActiveChangedHandler(JSEvents.GameActiveChangedEvent e);
    public event GameActiveChangedHandler OnGameActiveChanged;

    public delegate void ZoneChangedHandler(JSEvents.ZoneChangedEvent e);
    public event ZoneChangedHandler OnZoneChanged;

    public delegate void PlayerChangedHandler(JSEvents.PlayerChangedEvent e);
    public event PlayerChangedHandler OnPlayerChanged;

    public delegate void TargetChangedHandler(JSEvents.TargetChangedEvent e);
    public event TargetChangedHandler OnTargetChanged;

    public delegate void TargetCastingHandler(JSEvents.TargetCastingEvent e);
    public event TargetCastingHandler OnTargetCasting;

    public delegate void FocusCastingHandler(JSEvents.FocusCastingEvent e);
    public event FocusCastingHandler OnFocusCasting;

    public delegate void LogHandler(JSEvents.LogEvent e);
    public event LogHandler OnLogsChanged;

    public delegate void InCombatChangedHandler(JSEvents.InCombatChangedEvent e);
    public event InCombatChangedHandler OnInCombatChanged;

    public delegate void PlayerDiedHandler(JSEvents.PlayerDiedEvent e);
    public event PlayerDiedHandler OnPlayerDied;

    public delegate void PartyWipeHandler(JSEvents.PartyWipeEvent e);
    public event PartyWipeHandler OnPartyWipe;
    public void Wipe() {
      Advanced_Combat_Tracker.ActGlobals.oFormActMain.EndCombat(false);
      OnPartyWipe(new JSEvents.PartyWipeEvent());
    }

    public delegate void DataFilesReadHandler(JSEvents.DataFilesRead e);
    public event DataFilesReadHandler OnDataFilesRead;

    public CactbotOverlay(CactbotOverlayConfig config)
        : base(config, config.Name) {
      main_thread_sync_ = System.Windows.Forms.WindowsFormsSynchronizationContext.Current;
      ffxiv_ = new FFXIVProcess(this);
      fight_tracker_ = new FightTracker(this);
      wipe_detector_ = new WipeDetector(this);
      dispatch_json_writer_ = new JsonTextWriter(new System.IO.StringWriter(dispatch_string_builder_));
      dispatch_serializer_ = JsonSerializer.CreateDefault();


      // Our own timer with a higher frequency than OverlayPlugin since we want to see
      // the effect of log messages quickly.
      fast_update_timer_ = new System.Timers.Timer();
      fast_update_timer_.Elapsed += (o, args) => {
        try {
          SendFastRateEvents();
        } catch (Exception e) {
          LogError("Exception in SendFastRateEvents: " + e.Message + " \n" + e.StackTrace);
        }
      };
      fast_update_timer_.AutoReset = false;

      // Incoming events.
      Advanced_Combat_Tracker.ActGlobals.oFormActMain.OnLogLineRead += OnLogLineRead;

      // Outgoing JS events.
      OnGameExists += (e) => DispatchToJS(e);
      OnGameActiveChanged += (e) => DispatchToJS(e);
      OnZoneChanged += (e) => DispatchToJS(e);
      if (config.LogUpdatesEnabled) {
        OnLogsChanged += (e) => DispatchToJS(e);
      }
      OnPlayerChanged += (e) => DispatchToJS(e);
      OnTargetChanged += (e) => DispatchToJS(e);
      OnTargetCasting += (e) => DispatchToJS(e);
      OnFocusCasting += (e) => DispatchToJS(e);
      OnInCombatChanged += (e) => DispatchToJS(e);
      OnPlayerDied += (e) => DispatchToJS(e);
      OnPartyWipe += (e) => DispatchToJS(e);
      OnDataFilesRead += (e) => DispatchToJS(e);

      fast_update_timer_.Interval = kFastTimerMilli;
      fast_update_timer_.Start();
    }

    public override void Dispose() {
      fast_update_timer_.Stop();
      Advanced_Combat_Tracker.ActGlobals.oFormActMain.OnLogLineRead -= OnLogLineRead;
      base.Dispose();
    }

    public override void Navigate(string url) {
      // Wait for the fast timer to end before we proceed.
      fast_update_timer_semaphore_.Wait();

      // When we navigate, reset all state so that the newly loaded page can receive all updates.
      reset_notify_state_ = true;

      // We navigate only when the timer isn't running, as the browser window will disappear out
      // from under it. Once the navigation is done, the Browser is gone and we can run the timer
      // again.
      base.Navigate(url);
      fast_update_timer_semaphore_.Release();
    }

    private void OnLogLineRead(bool isImport, LogLineEventArgs args) {
      // isImport happens when somebody is importing old encounters and all the log lines are processed.
      // Don't need to send all of these to the overlay.
      if (isImport)
        return;
      log_lines_semaphore_.Wait();
      log_lines_.Add(args.logLine);
      log_lines_semaphore_.Release();
    }

    // This is called by the OverlayPlugin every 1s which is not often enough for us, so we
    // do our own update mechanism as well.
    protected override void Update() {
      SendSlowRateEvents();
    }

    // Sends an event called |event_name| to javascript, with an event.detail that contains
    // the fields and values of the |detail| structure.
    public void DispatchToJS(JSEvent e) {
      dispatch_string_builder_.Append("document.dispatchEvent(new CustomEvent('");
      dispatch_string_builder_.Append(e.EventName());
      dispatch_string_builder_.Append("', { detail: ");
      dispatch_serializer_.Serialize(dispatch_json_writer_, e);
      dispatch_string_builder_.Append(" }));");
      this.Overlay.Renderer.ExecuteScript(dispatch_string_builder_.ToString());
      dispatch_string_builder_.Clear();
    }

    // Events that we want to update less often because they aren't are critical.
    private void SendSlowRateEvents() {
      // Handle startup and shutdown. And do not fire any events until the page has loaded and had a chance to
      // register its event handlers.
      //if (Overlay == null || Overlay.Renderer == null || Overlay.Renderer.Browser == null || Overlay.Renderer.Browser.IsLoading)
      //  return;

      // NOTE: This function runs on a different thread that SendFastRateEvents(), so anything it calls needs to be thread-safe!
    }

    // Events that we want to update as soon as possible.
    private void SendFastRateEvents() {
      // Hold this while we're in here to prevent the Renderer or Browser from disappearing from under us.
      fast_update_timer_semaphore_.Wait();

      // Handle startup and shutdown. And do not fire any events until the page has loaded and had a chance to
      // register its event handlers.
      if (Overlay == null || Overlay.Renderer == null || Overlay.Renderer.Browser == null || Overlay.Renderer.Browser.IsLoading) {
        fast_update_timer_semaphore_.Release();
        fast_update_timer_.Interval = kSlowTimerMilli;
        fast_update_timer_.Start();
        return;
      }

      if (reset_notify_state_)
        notify_state_ = new NotifyState();
      reset_notify_state_ = false;

      if (!notify_state_.sent_data_dir && Config.Url.Length > 0) {
        notify_state_.sent_data_dir = true;

        var web = new System.Net.WebClient();

        var data_file_paths = new List<string>();
        try {
          var data_dir_manifest = new Uri(new Uri(Config.Url), "data/manifest.txt");
          var manifest_reader = new StringReader(web.DownloadString(data_dir_manifest));
          for (var line = manifest_reader.ReadLine(); line != null; line = manifest_reader.ReadLine())
            data_file_paths.Add(line);
        } catch (System.Net.WebException e) {
          if (e.Status == System.Net.WebExceptionStatus.ProtocolError &&
              e.Response is System.Net.HttpWebResponse &&
              ((System.Net.HttpWebResponse)e.Response).StatusCode == System.Net.HttpStatusCode.NotFound) {
            // Ignore file not found.
          } else if (e.InnerException != null &&
            (e.InnerException is FileNotFoundException || e.InnerException is DirectoryNotFoundException)) {
            // Ignore file not found.
          } else if (e.InnerException != null && e.InnerException.InnerException != null &&
            (e.InnerException.InnerException is FileNotFoundException || e.InnerException.InnerException is DirectoryNotFoundException)) {
            // Ignore file not found.
          } else {
            LogError("Unable to read manifest file: " + e.Message);
          }
        } catch (Exception e) {
          LogError("Unable to read manifest file: " + e.Message);
        }

        if (data_file_paths.Count > 0) {
          var file_data = new Dictionary<string, string>();
          foreach (string data_filename in data_file_paths) {
            try {
              var file_path = new Uri(new Uri(Config.Url), "data/" + data_filename);
              var file_reader = new StringReader(web.DownloadString(file_path));
              file_data[data_filename] = file_reader.ReadToEnd();
            } catch (Exception e) {
              LogError("Unable to read data file: " + e.Message);
            }
          }
          OnDataFilesRead(new JSEvents.DataFilesRead(file_data));
        }
      }

      bool game_exists = ffxiv_.FindProcess();
      if (game_exists != notify_state_.game_exists) {
        notify_state_.game_exists = game_exists;
        OnGameExists(new JSEvents.GameExistsEvent(game_exists));
      }

      bool game_active = game_active = ffxiv_.IsActive();
      if (game_active != notify_state_.game_active) {
        notify_state_.game_active = game_active;
        OnGameActiveChanged(new JSEvents.GameActiveChangedEvent(game_active));
      }

      // Silently stop sending other messages if the ffxiv process isn't around.
      if (!game_exists) {
        fast_update_timer_semaphore_.Release();
        fast_update_timer_.Interval = kUberSlowTimerMilli;
        fast_update_timer_.Start();
        return;
      }

      // onInCombatChangedEvent: Fires when entering or leaving combat.
      bool in_combat = FFXIV_ACT_Plugin.ACTWrapper.InCombat;
      if (in_combat != notify_state_.in_combat) {
        notify_state_.in_combat = in_combat;
        OnInCombatChanged(new JSEvents.InCombatChangedEvent(in_combat));
      }

      // onZoneChangedEvent: Fires when the player changes their current zone.
      string zone_name = FFXIV_ACT_Plugin.ACTWrapper.CurrentZone;
      if (!zone_name.Equals(notify_state_.zone_name)) {
        notify_state_.zone_name = zone_name;
        OnZoneChanged(new JSEvents.ZoneChangedEvent(zone_name));
      }

      DateTime now = DateTime.Now;
      // The |player| can be null, such as during a zone change.
      FFXIVProcess.EntityData player = ffxiv_.GetSelfData();
      // The |target| can be null when no target is selected.
      FFXIVProcess.EntityData target = ffxiv_.GetTargetData();
      // The |target_casting| can be null when no target is selected.
      var target_casting = ffxiv_.GetTargetCastingData();
      // The |focus_casting| can be null when no focus is selected.
      var focus_casting = ffxiv_.GetFocusCastingData();

      // onPlayerDiedEvent: Fires when the player dies. All buffs/debuffs are
      // lost.
      if (player != null) {
        bool dead = player.hp == 0;
        if (dead != notify_state_.dead) {
          notify_state_.dead = dead;
          if (dead)
            OnPlayerDied(new JSEvents.PlayerDiedEvent());
        }
      }

      // onPlayerChangedEvent: Fires when current player data changes.
      // TODO: Is this always true cuz it's only doing pointer comparison?
      if (player != null && player != notify_state_.player) {
        notify_state_.player = player;
        if (player.job == FFXIVProcess.EntityJob.RDM) {
          var rdm = ffxiv_.GetRedMage();
          if (rdm != null) {
            var e = new JSEvents.PlayerChangedEvent(player);
            e.jobDetail = new JSEvents.PlayerChangedEvent.RedMageDetail(rdm.white, rdm.black);
            OnPlayerChanged(e);
          }
        } else if (player.job == FFXIVProcess.EntityJob.WAR) {
          var job = ffxiv_.GetWarrior();
          var e = new JSEvents.PlayerChangedEvent(player);
          if (job != null) {
            e.jobDetail = new JSEvents.PlayerChangedEvent.WarriorDetail(job.beast);
            OnPlayerChanged(e);
          }
        } else {
          // No job-specific data.
          OnPlayerChanged(new JSEvents.PlayerChangedEvent(player));
        }
      }

      // onTargetChangedEvent: Fires when current target or their state changes.
      // TODO: Is this always true cuz it's only doing pointer comparison?
      if (target != notify_state_.target) {
        notify_state_.target = target;
        if (target != null)
          OnTargetChanged(new JSEvents.TargetChangedEvent(target));
        else
          OnTargetChanged(new JSEvents.TargetChangedEvent(null));
      }

      // onTargetCastingEvent: Fires each tick while the target is casting, and once
      // with null when not casting.
      int target_cast_id = target_casting != null ? target_casting.cast_id : 0;
      if (target_cast_id != 0 || target_cast_id != notify_state_.target_cast_id) {
        notify_state_.target_cast_id = target_cast_id;
        // The game considers things to be casting once progress reaches the end for a while, as the server is
        // resolving lag or something. That breaks our start time tracking, so we just don't consider them to
        // be casting anymore once it reaches the end.
        if (target_cast_id != 0 && target_casting.casting_time_progress < target_casting.casting_time_length) {
          DateTime start = now.AddSeconds(-target_casting.casting_time_progress);
          // If the start is within the timer interval, assume it's the same cast. Since we sample the game
          // at a different rate than it ticks, there will be some jitter in the progress that we see, and this
          // helps avoid it.
          TimeSpan range = new TimeSpan(0, 0, 0, 0, kFastTimerMilli);
          if (start + range < notify_state_.target_cast_start || start - range > notify_state_.target_cast_start)
            notify_state_.target_cast_start = start;
          TimeSpan progress = now - notify_state_.target_cast_start;
          OnTargetCasting(new JSEvents.TargetCastingEvent(target_casting.cast_id, progress.TotalSeconds, target_casting.casting_time_length));
        } else {
          notify_state_.target_cast_start = new DateTime();
          OnTargetCasting(new JSEvents.TargetCastingEvent(0, 0, 0));
        }
      }

      // onFocusCastingEvent: Fires each tick while the focus target is casting, and
      // once with null when not casting.
      int focus_cast_id = focus_casting != null ? focus_casting.cast_id : 0;
      if (focus_cast_id != 0 || focus_cast_id != notify_state_.focus_cast_id) {
        notify_state_.focus_cast_id = focus_cast_id;
        // The game considers things to be casting once progress reaches the end for a while, as the server is
        // resolving lag or something. That breaks our start time tracking, so we just don't consider them to
        // be casting anymore once it reaches the end.
        if (focus_cast_id != 0 && focus_casting.casting_time_progress < focus_casting.casting_time_length) {
          DateTime start = now.AddSeconds(-focus_casting.casting_time_progress);
          // If the start is within the timer interval, assume it's the same cast. Since we sample the game
          // at a different rate than it ticks, there will be some jitter in the progress that we see, and this
          // helps avoid it.
          TimeSpan range = new TimeSpan(0, 0, 0, 0, kFastTimerMilli);
          if (start + range < notify_state_.focus_cast_start || start - range > notify_state_.focus_cast_start)
            notify_state_.focus_cast_start = start;
          TimeSpan progress = now - notify_state_.focus_cast_start;
          OnFocusCasting(new JSEvents.FocusCastingEvent(focus_casting.cast_id, progress.TotalSeconds, focus_casting.casting_time_length));
        } else {
          notify_state_.focus_cast_start = new DateTime();
          OnFocusCasting(new JSEvents.FocusCastingEvent(0, 0, 0));
        }
      }

      // onLogEvent: Fires when new combat log events from FFXIV are available. This fires after any
      // more specific events, some of which may involve parsing the logs as well.
      List<string> logs;
      log_lines_semaphore_.Wait();
      logs = log_lines_;
      log_lines_ = last_log_lines_;
      log_lines_semaphore_.Release();
      if (logs.Count > 0) {
        OnLogsChanged(new JSEvents.LogEvent(logs));
        logs.Clear();
      }
      last_log_lines_ = logs;

      fight_tracker_.Tick(DateTime.Now);

      fast_update_timer_semaphore_.Release();

      fast_update_timer_.Interval = game_active ? kFastTimerMilli : kSlowTimerMilli;
      fast_update_timer_.Start();
    }

    public int IncrementAndGetPullCount(string boss_id) {
      for (int i = 0; i < Config.BossInfoList.Count; ++i) {
        if (Config.BossInfoList[i].id == boss_id) {
          int pull_count = Config.BossInfoList[i].pull_count + 1;
          Config.BossInfoList[i] = new BossInfo(boss_id, pull_count);
          return pull_count;
        }
      }
      Config.BossInfoList.Add(new BossInfo(boss_id, 1));
      return 1;
    }

    // ILogger implementation.
    public void LogDebug(string format, params object[] args) {
      // The Log() method is not threadsafe. Since this is called from Timer threads,
      // it must post the task to the plugin main thread.
      main_thread_sync_.Post(
        (state) => { this.Log(LogLevel.Debug, format, args); },
        null);
    }
    public void LogError(string format, params object[] args) {
      // The Log() method is not threadsafe. Since this is called from Timer threads,
      // it must post the task to the plugin main thread.
      main_thread_sync_.Post(
        (state) => { this.Log(LogLevel.Error, format, args); },
        null);
    }
    public void LogWarning(string format, params object[] args) {
      // The Log() method is not threadsafe. Since this is called from Timer threads,
      // it must post the task to the plugin main thread.
      main_thread_sync_.Post(
        (state) => { this.Log(LogLevel.Warning, format, args); },
        null);
    }
    public void LogInfo(string format, params object[] args) {
      // The Log() method is not threadsafe. Since this is called from Timer threads,
      // it must post the task to the plugin main thread.
      main_thread_sync_.Post(
        (state) => { this.Log(LogLevel.Info, format, args); },
        null);
    }

    // State that is tracked and sent to JS when it changes.
    private class NotifyState {
      public bool sent_data_dir = false;
      public bool game_exists = false;
      public bool game_active = false;
      public bool in_combat = false;
      public bool dead = false;
      public string zone_name = "";
      public FFXIVProcess.EntityData player = null;
      public FFXIVProcess.EntityData target = null;
      public int target_cast_id = 0;
      public DateTime target_cast_start = new DateTime();
      public int focus_cast_id = 0;
      public DateTime focus_cast_start = new DateTime();
    }
    private NotifyState notify_state_ = new NotifyState();
  }

}  // namespace Cactbot