export const languages = ['en', 'de', 'fr', 'ja', 'cn', 'ko'] as const;

export type Lang = typeof languages[number];

export type NonEnLang = Exclude<Lang, 'en'>;

export const langMap: { [lang in Lang]: { [lang in Lang]: string } } = {
  en: {
    en: 'English',
    de: 'German',
    fr: 'French',
    ja: 'Japanese',
    cn: 'Chinese',
    ko: 'Korean',
    es: 'Spanish',
  },
  de: {
    en: 'Englisch',
    de: 'Deutsch',
    fr: 'Französisch',
    ja: 'Japanisch',
    cn: 'Chinesisch',
    ko: 'Koreanisch',
    es: 'Spanisch',
  },
  fr: {
    en: 'Anglais',
    de: 'Allemand',
    fr: 'Français',
    ja: 'Japonais',
    cn: 'Chinois',
    ko: 'Coréen',
    es: 'espagnol',
  },
  ja: {
    en: '英語',
    de: 'ドイツ語',
    fr: 'フランス語',
    ja: '日本語',
    cn: '中国語',
    ko: '韓国語',
    es: 'スペイン語',
  },
  cn: {
    en: '英文',
    de: '德文',
    fr: '法文',
    ja: '日文',
    cn: '中文',
    ko: '韩文',
    es: '西班牙文',
  },
  ko: {
    en: '영어',
    de: '독일어',
    fr: '프랑스어',
    ja: '일본어',
    cn: '중국어',
    ko: '한국어',
    es: '스페인',
  },
   es: {
    en: 'Inglés',
    de: 'Aleman',
    fr: 'Frances',
    ja: 'Japonés',
    cn: 'Chino',
    ko: 'Coreano',
    es: 'Español',
  },
} as const;

export const isLang = (lang?: string): lang is Lang => {
  const langStrs: readonly string[] = languages;
  if (lang === undefined)
    return false;
  return langStrs.includes(lang);
};

export const langToLocale = (lang: Lang): string => {
  return {
    en: 'en',
    de: 'de',
    fr: 'fr',
    ja: 'ja',
    cn: 'zh-CN',
    ko: 'ko',
    es: 'es',
  }[lang];
};

export const browserLanguagesToLang = (languages: readonly string[]): Lang => {
  const lang = [...navigator.languages, 'en']
    .map((l) => l.slice(0, 2))
    // Remap `zh` to `cn` to match cactbot languages
    .map((l) => l === 'zh' ? 'cn' : l)
    .filter((l) => languages.includes(l))[0];
  return isLang(lang) ? lang : 'en';
};
