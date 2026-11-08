import { activeFestival, festivalCountry } from './festival-calendar';

describe('storefront festival calendar', () => {
  for (const [festival, month, day, country] of [
    ['diwali', 11, 8, 'IN'], ['holi', 3, 4, 'IN'], ['christmas', 12, 25, 'US'],
  ] as const) {
    it(`shows ${festival} for exactly two days before through one day after`, () => {
      const on = (offset: number) => activeFestival(new Date(2026, month - 1, day + offset), country);
      expect(on(-3)).toBeUndefined();
      for (const offset of [-2, -1, 0, 1]) expect(on(offset)).toBe(festival);
      expect(on(2)).toBeUndefined();
    });
  }

  it('uses the separately published Diwali dates for India and Singapore', () => {
    expect(activeFestival(new Date(2027, 9, 26), 'SG')).toBe('diwali');
    expect(activeFestival(new Date(2027, 9, 26), 'IN')).toBeUndefined();
    expect(activeFestival(new Date(2027, 9, 30), 'IN')).toBe('diwali');
    expect(activeFestival(new Date(2027, 9, 30), 'SG')).toBeUndefined();
    expect(activeFestival(new Date(2026, 10, 8), 'US')).toBeUndefined();
  });

  it('uses recurring country dates and keeps Christmas available without a country', () => {
    expect(activeFestival(new Date(2026, 10, 26), 'US')).toBe('thanksgiving');
    expect(activeFestival(new Date(2026, 9, 12), 'CA')).toBe('thanksgiving');
    expect(activeFestival(new Date(2026, 9, 12), 'US')).toBeUndefined();
    expect(activeFestival(new Date(2030, 11, 23))).toBe('christmas');
  });

  it('prefers a recognized time zone and falls back to the visitor locale', () => {
    expect(festivalCountry('Asia/Calcutta', ['en-US'])).toBe('IN');
    expect(festivalCountry('America/Toronto', ['en-US'])).toBe('CA');
    expect(festivalCountry('UTC', ['invalid_locale', 'en-SG'])).toBe('SG');
    expect(festivalCountry('UTC', ['hi'])).toBe('IN');
    expect(festivalCountry('UTC', ['en'])).toBeUndefined();
  });
});
