import { HeartRateMeasurement, MeasurementSession } from '../models/measurement-session';

interface MeasurementSummary {
  averageBpm: number | null;
  minimumBpm: number | null;
  maximumBpm: number | null;
  weightedBpm: number;
  durationSeconds: number;
}

export function summarizeMeasurements(
  samples: readonly HeartRateMeasurement[],
): MeasurementSummary {
  let minimum = Infinity;
  let maximum = -Infinity;
  let weightedBpm = 0;
  let durationSeconds = 0;

  for (const sample of samples) {
    minimum = Math.min(minimum, sample.bpm);
    maximum = Math.max(maximum, sample.bpm);
    weightedBpm += sample.bpm * sample.intervalSeconds;
    durationSeconds += sample.intervalSeconds;
  }

  return {
    averageBpm: durationSeconds ? weightedBpm / durationSeconds : null,
    minimumBpm: samples.length ? minimum : null,
    maximumBpm: samples.length ? maximum : null,
    weightedBpm,
    durationSeconds,
  };
}

const bpmNumber = new Intl.NumberFormat('pl-PL', { maximumFractionDigits: 0 });
export const formatBpm = (value: number | null | undefined): string =>
  value == null ? '—' : bpmNumber.format(value);

export function formatDuration(seconds: number): string {
  const whole = Math.max(0, Math.round(seconds));
  if (whole < 60) return whole + ' s';
  const minutes = Math.floor(whole / 60);
  const hours = Math.floor(minutes / 60);
  const remainder = minutes % 60;
  return hours ? hours + ' godz.' + (remainder ? ' ' + remainder + ' min' : '') : minutes + ' min';
}

export function sessionDuration(session: MeasurementSession): string {
  return session.finishedAtUtc
    ? formatDuration((Date.parse(session.finishedAtUtc) - Date.parse(session.startedAtUtc)) / 1000)
    : 'W toku';
}

const plural = new Intl.PluralRules('pl-PL');

export function sessionCount(count: number): string {
  const form = plural.select(count);
  return count + (form === 'one' ? ' sesja' : form === 'few' ? ' sesje' : ' sesji');
}
