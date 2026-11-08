export const festivalGreetings = {
  diwali: { icon: '🪔', en: 'Happy Diwali!', hi: 'दीपावली की हार्दिक शुभकामनाएँ!' },
  holi: { icon: '🌈', en: 'Happy Holi!', hi: 'होली की हार्दिक शुभकामनाएँ!' },
  christmas: { icon: '🎄', en: 'Merry Christmas!', hi: 'क्रिसमस की हार्दिक शुभकामनाएँ!' },
  newYear: { icon: '✨', en: 'Happy New Year!', hi: 'नव वर्ष की हार्दिक शुभकामनाएँ!' },
  republicDay: { icon: '🇮🇳', en: 'Happy Republic Day, India!', hi: 'गणतंत्र दिवस की हार्दिक शुभकामनाएँ!' },
  indiaIndependence: { icon: '🇮🇳', en: 'Happy Independence Day, India!', hi: 'स्वतंत्रता दिवस की हार्दिक शुभकामनाएँ!' },
  dussehra: { icon: '✨', en: 'Happy Dussehra!', hi: 'दशहरे की हार्दिक शुभकामनाएँ!' },
  julyFourth: { icon: '🇺🇸', en: 'Happy Fourth of July!', hi: 'अमेरिकी स्वतंत्रता दिवस की शुभकामनाएँ!' },
  thanksgiving: { icon: '🍂', en: 'Happy Thanksgiving!', hi: 'थैंक्सगिविंग की हार्दिक शुभकामनाएँ!' },
  canadaDay: { icon: '🇨🇦', en: 'Happy Canada Day!', hi: 'कनाडा दिवस की हार्दिक शुभकामनाएँ!' },
  singaporeNationalDay: { icon: '🇸🇬', en: 'Happy National Day, Singapore!', hi: 'सिंगापुर के राष्ट्रीय दिवस की शुभकामनाएँ!' },
  lunarNewYear: { icon: '🏮', en: 'Happy Lunar New Year!', hi: 'चंद्र नव वर्ष की हार्दिक शुभकामनाएँ!' },
} as const;

type Festival = keyof typeof festivalGreetings;
type FestivalDate = readonly [festival: Festival, month: number, day: number];

const globalDates: readonly FestivalDate[] = [['newYear', 1, 1], ['christmas', 12, 25]];
const annual: Partial<Record<string, readonly FestivalDate[]>> = {
  IN: [['republicDay', 1, 26], ['indiaIndependence', 8, 15]],
  US: [['julyFourth', 7, 4]],
  CA: [['canadaDay', 7, 1]],
  SG: [['singaporeNationalDay', 8, 9]],
};

// Movable dates follow each country's published calendar. See docs/festival-greetings.md.
const published: Partial<Record<string, Partial<Record<number, readonly FestivalDate[]>>>> = {
  IN: {
    2026: [['holi', 3, 4], ['dussehra', 10, 20], ['diwali', 11, 8]],
    2027: [['holi', 3, 23], ['dussehra', 10, 9], ['diwali', 10, 29]],
  },
  SG: {
    2026: [['lunarNewYear', 2, 17], ['lunarNewYear', 2, 18], ['diwali', 11, 8]],
    2027: [['lunarNewYear', 2, 6], ['lunarNewYear', 2, 7], ['diwali', 10, 28]],
  },
};

const zoneCountries: Partial<Record<string, string>> = {
  'Asia/Kolkata': 'IN', 'Asia/Calcutta': 'IN', 'Asia/Singapore': 'SG',
  'America/New_York': 'US', 'America/Chicago': 'US', 'America/Denver': 'US', 'America/Los_Angeles': 'US',
  'America/Toronto': 'CA', 'America/Vancouver': 'CA', 'America/Edmonton': 'CA', 'America/Halifax': 'CA',
};

export function festivalCountry(timeZone: string, locales: readonly string[]): string | undefined {
  if (Object.hasOwn(zoneCountries, timeZone)) return zoneCountries[timeZone];
  // Prefer the first locale with an explicit region when the zone is inconclusive.
  for (const value of locales) {
    try {
      const region = new Intl.Locale(value).region;
      if (region) return region;
    } catch { /* Browsers may pass a custom locale. */ }
  }
  return locales.some(locale => /^hi(?:-|$)/i.test(locale)) ? 'IN' : undefined;
}

function weekdayOfMonth(year: number, month: number, weekday: number, occurrence: number): number {
  const first = new Date(Date.UTC(year, month - 1, 1)).getUTCDay();
  return 1 + (weekday - first + 7) % 7 + (occurrence - 1) * 7;
}

export function activeFestival(today: Date, country?: string): Festival | undefined {
  const year = today.getFullYear();
  // Compare calendar days so a daylight-saving change cannot shorten the display window.
  const day = Date.UTC(year, today.getMonth(), today.getDate());
  const region = country ?? '';
  const dates = [...globalDates, ...(annual[region] ?? []), ...(published[region]?.[year] ?? [])];
  if (country === 'US') dates.push(['thanksgiving', 11, weekdayOfMonth(year, 11, 4, 4)]);
  if (country === 'CA') dates.push(['thanksgiving', 10, weekdayOfMonth(year, 10, 1, 2)]);

  return dates.find(([festival, month, date]) => {
    const offset = (day - Date.UTC(year, month - 1, date)) / 86_400_000;
    if (festival === 'diwali' || festival === 'holi' || festival === 'christmas') return offset >= -2 && offset <= 1;
    return offset === 0;
  })?.[0];
}
