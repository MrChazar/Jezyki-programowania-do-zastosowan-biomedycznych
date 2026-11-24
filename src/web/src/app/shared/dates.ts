export const earliestDate = '1900-01-01';
export const latestDate = '9998-12-31';

const utc = { timeZone: 'UTC' };
const longDay = new Intl.DateTimeFormat('pl-PL', {
  ...utc,
  weekday: 'long',
  day: 'numeric',
  month: 'long',
});
const fullDay = new Intl.DateTimeFormat('pl-PL', {
  ...utc,
  day: 'numeric',
  month: 'long',
  year: 'numeric',
});
const month = new Intl.DateTimeFormat('pl-PL', { ...utc, month: 'long', year: 'numeric' });
const shortWeekdays = ['Nd', 'Pon', 'Wt', 'Śr', 'Czw', 'Pt', 'Sob'];

export const dateFromKey = (date: string): Date => new Date(date + 'T00:00:00Z');
const keyFromDate = (date: Date): string => date.toISOString().slice(0, 10);
export const startOfMonth = (date: string): string => date.slice(0, 7) + '-01';

export function isCalendarDate(value: string | null): value is string {
  if (value === null || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return false;
  const date = dateFromKey(value);
  return (
    value >= earliestDate && value <= latestDate && !isNaN(+date) && keyFromDate(date) === value
  );
}

export function addDays(day: string, amount: number): string {
  const date = dateFromKey(day);
  date.setUTCDate(date.getUTCDate() + amount);
  return keyFromDate(date);
}

export function addMonths(day: string, amount: number): string {
  const date = dateFromKey(startOfMonth(day));
  date.setUTCMonth(date.getUTCMonth() + amount);
  return keyFromDate(date);
}

export function mondayOffset(day: string): number {
  return (dateFromKey(day).getUTCDay() + 6) % 7;
}

export function startOfWeek(day: string): string {
  return addDays(day, -mondayOffset(day));
}

export function dateKey(instant: Date | string, timeZone: string): string {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).formatToParts(new Date(instant));
  const part = (name: Intl.DateTimeFormatPartTypes) =>
    parts.find(({ type }) => type === name)?.value;
  return [part('year')?.padStart(4, '0'), part('month'), part('day')].join('-');
}

const capitalize = (value: string): string =>
  value.charAt(0).toLocaleUpperCase('pl-PL') + value.slice(1);

export const dayLabel = (day: string): string => capitalize(longDay.format(dateFromKey(day)));
export const fullDayLabel = (day: string): string => fullDay.format(dateFromKey(day));
export const monthLabel = (day: string): string => capitalize(month.format(dateFromKey(day)));
export const shortDayLabel = (day: string): string => {
  const date = dateFromKey(day);
  return shortWeekdays[date.getUTCDay()] + ' ' + date.getUTCDate();
};
export const weekLabel = (day: string): string =>
  fullDay.formatRange(dateFromKey(day), dateFromKey(addDays(day, 6)));

export function timeLabel(value: string | number, timeZone: string, seconds = false): string {
  return new Intl.DateTimeFormat('pl-PL', {
    timeZone,
    hour: '2-digit',
    minute: '2-digit',
    second: seconds ? '2-digit' : undefined,
  }).format(new Date(value));
}

export function dateTimeLabel(value: string, timeZone: string): string {
  const date = new Intl.DateTimeFormat('pl-PL', {
    timeZone,
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  }).format(new Date(value));
  return date + ' · ' + timeLabel(value, timeZone);
}
