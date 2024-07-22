import Conditions from '../../../../../resources/conditions';
import { Responses } from '../../../../../resources/responses';
import ZoneId from '../../../../../resources/zone_id';
import { RaidbossData } from '../../../../../types/data';
import { TriggerSet } from '../../../../../types/trigger';

// TODO: Add better (directional?) callout for Drowsie's Wallop (ivy cleaves)

export type Data = RaidbossData;

const triggerSet: TriggerSet<Data> = {
  id: 'Ihuykatumu',
  zoneId: ZoneId.Ihuykatumu,
  timelineFile: 'ihuykatumu.txt',
  triggers: [
    // ** Prime Punutiy ** //
    {
      id: 'Ihuykatumu Prime Punutiy Punutiy Press',
      type: 'StartsUsing',
      netRegex: { id: '8E8C', source: 'Prime Punutiy', capture: false },
      response: Responses.aoe(),
    },
    {
      id: 'Ihuykatumu Prime Punutiy Decay',
      type: 'StartsUsing',
      netRegex: { id: '8E99', source: 'Ihuykatumu Flytrap', capture: false },
      alertText: (_data, _matches, output) => output.text!(),
      outputStrings: {
        text: {
          en: 'Get under Flytrap',
          de: 'Geh unter die Fliegenfalle',
          cn: '进入月环',
        },
      },
    },
    {
      id: 'Ihuykatumu Prime Punutiy Inhale',
      type: 'StartsUsing',
      netRegex: { id: '8E8E', source: 'Prime Punutiy', capture: false },
      durationSeconds: 10, // prolonged ground damage
      alertText: (_data, _matches, output) => output.text!(),
      outputStrings: {
        text: {
          en: 'Stay out of inhale',
          de: 'Steh auserhalb des Soges',
          cn: '躲开 Boss 吸气 + 全场 AoE',
        },
      },
    },
    {
      id: 'Ihuykatumu Prime Punutiy Shore Shaker',
      type: 'StartsUsing',
      netRegex: { id: '8EA2', source: 'Prime Punutiy', capture: false },
      durationSeconds: 6,
      response: Responses.getOutThenIn(),
    },

    // ** Drowsie ** //
    {
      id: 'Ihuykatumu Drowsie Uppercut',
      type: 'StartsUsing',
      netRegex: { id: '98DC', source: 'Drowsie' },
      response: Responses.tankBuster(),
    },
    {
      id: 'Ihuykatumu Drowsie Wallop Small',
      type: 'StartsUsing',
      netRegex: { id: '8E7F', source: 'Ihuykatumu Ivy', capture: false },
      delaySeconds: 2,
      suppressSeconds: 1,
      infoText: (_data, _matches, output) => output.text!(),
      outputStrings: {
        text: {
          en: 'Dodge Ivy cleaves (small)',
          de: 'Efeu-Cleave ausweichen (klein)',
          cn: '注意触手直线AoE (小)',
        },
      },
    },
    {
      id: 'Ihuykatumu Drowsie Wallop Large',
      type: 'StartsUsing',
      netRegex: { id: '8E82', source: 'Ihuykatumu Ivy', capture: false },
      delaySeconds: 2,
      suppressSeconds: 1,
      infoText: (_data, _matches, output) => output.text!(),
      outputStrings: {
        text: {
          en: 'Dodge Ivy cleaves (big)',
          de: 'Efeu-Cleave ausweichen (groß)',
          cn: '注意触手直线AoE (大)',
        },
      },
    },
    {
      id: 'Ihuykatumu Drowsie Sneeze',
      type: 'StartsUsing',
      netRegex: { id: '8E7B', source: 'Drowsie', capture: false },
      response: Responses.awayFromFront(),
    },
    {
      id: 'Ihuykatumu Drowsie Flagrant Spread',
      type: 'HeadMarker',
      netRegex: { id: '008B' },
      condition: Conditions.targetIsYou(),
      response: Responses.moveAway(),
    },

    // ** Apollyon ** //
    {
      id: 'Ihuykatumu Apollyon Blade',
      type: 'StartsUsing',
      netRegex: { id: ['8DFB', '8E04'], source: 'Apollyon' },
      response: Responses.tankBuster(),
    },
    {
      id: 'Ihuykatumu Apollyon High Wind',
      type: 'StartsUsing',
      netRegex: { id: '8DF5', source: 'Apollyon', capture: false },
      response: Responses.aoe(),
    },
    {
      id: 'Ihuykatumu Apollyon Swarming Locust First Cleaves',
      type: 'Ability',
      netRegex: { id: '8DF7', source: 'Apollyon', capture: false },
      alertText: (_data, _matches, output) => output.text!(),
      outputStrings: {
        text: {
          en: 'Away on 3rd jump',
          de: 'Weg vom 3. Sprung',
          cn: '远离第三次跳跃落点',
        },
      },
    },
    {
      id: 'Ihuykatumu Apollyon Swarming Locust Second Cleaves',
      type: 'Ability',
      netRegex: { id: '8DF7', source: 'Apollyon', capture: false },
      delaySeconds: 7.5,
      alertText: (_data, _matches, output) => output.text!(),
      outputStrings: {
        text: {
          en: 'Away on 3rd jump',
          de: 'Weg vom 3. Sprung',
          cn: '远离第三次跳跃落点',
        },
      },
    },
    {
      id: 'Ihuykatumu Apollyon Thunder III',
      type: 'HeadMarker',
      netRegex: { id: '006C' },
      condition: Conditions.targetIsYou(),
      response: Responses.spread(),
    },
    {
      id: 'Ihuykatumu Apollyon Wind Sickle',
      type: 'Ability',
      netRegex: { id: '8E05', source: 'Apollyon', capture: false },
      durationSeconds: 8,
      infoText: (_data, _matches, output) => output.text!(),
      outputStrings: {
        text: {
          en: 'In, then follow jump',
          de: 'Rein, dann Sprüngen folgen',
          cn: '进入月环 => 去BOSS身后',
        },
      },
    },
    {
      id: 'Ihuykatumu Apollyon Windwhistle 1',
      type: 'Ability',
      netRegex: { id: '8E07', source: 'Apollyon', capture: false },
      delaySeconds: 7,
      infoText: (_data, _matches, output) => output.text!(),
      outputStrings: {
        text: {
          en: 'Avoid Whirlwind star lines',
          de: 'Wirbelwind-Sternenlinien vermeiden',
          cn: '注意风圈星形的直线AoE',
        },
      },
    },
    {
      id: 'Ihuykatumu Apollyon Windwhistle 2',
      type: 'Ability',
      netRegex: { id: '8E07', source: 'Apollyon', capture: false },
      delaySeconds: 15,
      infoText: (_data, _matches, output) => output.text!(),
      outputStrings: {
        text: {
          en: 'Avoid Whirlwind star lines',
          de: 'Wirbelwind-Sternenlinien vermeiden',
        },
      },
    },
    {
      id: 'Ihuykatumu Apollyon Windwhistle 3',
      type: 'Ability',
      netRegex: { id: '8E07', source: 'Apollyon', capture: false },
      delaySeconds: 23,
      infoText: (_data, _matches, output) => output.text!(),
      outputStrings: {
        text: {
          en: 'Avoid Whirlwind star lines',
          de: 'Wirbelwind-Sternenlinien vermeiden',
        },
      },
    },
  ],
  timelineReplace: [
    {
      'locale': 'de',
      'replaceSync': {
        'Apollyon': 'Apollyon',
        'Drowsie': 'Schläfrich',
        'Green Clot': 'grün(?:e|er|es|en) Klumpen',
        'Ihuykatumu Flytrap': 'Ihuykatumu-Fliegenfalle',
        'Ihuykatumu Ivy': 'Ihuykatumu-Efeuranke',
        'Ihuykatumu Sandworm': 'Ihuykatumu-Sandwurm',
        'Prime Punutiy': 'Alpha-Punutiy',
        '(?<! )Punutiy': 'Punutiy',
      },
      'replaceText': {
        '\\(cast\\)': '(wirken)',
        '\\(inner ring\\)': '(innerer Ring)',
        '\\(large\\)': '(groß)',
        '\\(outer ring\\)': '(äußerer Ring)',
        '\\(puddle\\)': '(Fläche)',
        '\\(small\\)': '(klein)',
        'Arise': 'Erscheinung',
        'Blade(?!s )': 'Sense',
        'Blades of Famine': 'Heuschreckenklinge',
        'Bury': 'Impakt',
        'Cutting Wind': 'Windklinge',
        'Decay': 'Verwesung',
        'Drowsy Dance': 'Schläfriger Tanz',
        'High Wind': 'Starkböen',
        'Hydrowave': 'Hydro-Welle',
        'Levinsickle': 'Blitzsichel',
        'Punutiy Flop': 'Punutiy-Sprung',
        'Punutiy Press': 'Punutiy-Presse',
        'Razor Storm': 'Gewaltige Luftklinge',
        'Razor Zephyr': 'Luftklinge',
        'Resurface': 'Auftauchen',
        'Shore Shaker': 'Küstenbeben',
        'Sneeze': 'Großer Nieser',
        'Song of the Punutiy': 'Punutiy-Ruf',
        'Sow': 'Saat',
        'Spit': 'Ausspeien',
        'Swarming Locust': 'Heuschreckenschwarm',
        'Thunder III': 'Blitzga',
        'Uppercut': 'Aufwärtshaken',
        'Wallop': 'Eindreschen',
        'Wind Sickle': 'Windsichel',
        'Windwhistle': 'Windruf',
        'Wing of Lightning': 'Fächerentladung',
      },
    },
    {
      'locale': 'fr',
      'missingTranslations': true,
      'replaceSync': {
        'Apollyon': 'apollyon',
        'Drowsie': 'Somnolent',
        'Green Clot': 'caillot vert',
        'Ihuykatumu Flytrap': 'piège-mouche de l\'Ihuykatumu',
        'Ihuykatumu Ivy': 'lierre de l\'Ihuykatumu',
        'Ihuykatumu Sandworm': 'ver des sables de l\'Ihuykatumu',
        'Prime Punutiy': 'punutiy alpha',
        '(?<! )Punutiy': 'punutiy',
      },
      'replaceText': {
        'Arise': 'Apparition',
        'Blade(?!s )': 'Ravisseuse',
        'Blades of Famine': 'Lames locustes',
        'Bury': 'Impact',
        'Cutting Wind': 'Lame du vent',
        'Decay': 'Décomposition',
        'Drowsy Dance': 'Danse de Somnolent',
        'High Wind': 'Grands vents',
        'Hydrowave': 'Hydro-vague',
        'Levinsickle': 'Faucilles foudroyantes',
        'Punutiy Flop': 'Bond punutiy',
        'Punutiy Press': 'Aplatissement punutiy',
        'Razor Storm': 'Vent tranchant massif',
        'Razor Zephyr': 'Vent tranchant',
        'Resurface': 'Turbinage',
        'Shore Shaker': 'Secousse côtière',
        'Sneeze': 'Éternuement',
        'Song of the Punutiy': 'Appel punutiy',
        'Sow': 'Semence',
        'Spit': 'Crachat morbide',
        'Swarming Locust': 'Nuée de locustes',
        'Thunder III': 'Méga Foudre',
        'Uppercut': 'Uppercut',
        'Wallop': 'Rossée',
        'Wind Sickle': 'Faucilles foudroyantes',
        'Windwhistle': 'Appel du vent',
        'Wing of Lightning': 'Décharge en éventail',
      },
    },
    {
      'locale': 'ja',
      'missingTranslations': true,
      'replaceSync': {
        'Apollyon': 'アポリオン',
        'Drowsie': 'ドラウジー',
        'Green Clot': 'グリーンクロット',
        'Ihuykatumu Flytrap': 'イフイカ・フライトラップ',
        'Ihuykatumu Ivy': 'イフイカ・アイビー',
        'Ihuykatumu Sandworm': 'イフイカ・サンドウォーム',
        'Prime Punutiy': 'アルファ・プヌティー',
        '(?<! )Punutiy': 'プヌティー',
      },
      'replaceText': {
        'Arise': '出現',
        'Blade(?!s )': 'カマ',
        'Blades of Famine': 'ローカストブレイド',
        'Bury': '衝撃',
        'Cutting Wind': '風刃',
        'Decay': 'ディケイ',
        'Drowsy Dance': 'ドラウジーダンス',
        'High Wind': 'ハイウィンド',
        'Hydrowave': 'ハイドロウェーブ',
        'Levinsickle': 'ライトニングシックル',
        'Punutiy Flop': 'プヌティーリープ',
        'Punutiy Press': 'プヌティープレス',
        'Razor Storm': 'マッシブ・エアレイザー',
        'Razor Zephyr': 'エアレイザー',
        'Resurface': 'リサーフェス',
        'Shore Shaker': 'ショアシェイカー',
        'Sneeze': 'くしゃみ',
        'Song of the Punutiy': 'プヌティーコール',
        'Sow': '種まき',
        'Spit': '吐出す',
        'Swarming Locust': 'ローカストスウォーム',
        'Thunder III': 'サンダガ',
        'Uppercut': 'アッパーカット',
        'Wallop': '叩きつけ',
        'Wind Sickle': 'ウィンドシックル',
        'Windwhistle': '風呼び',
        'Wing of Lightning': '扇状放電',
      },
    },
  ],
};

export default triggerSet;
