import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  linkedSignal,
} from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { forkJoin } from 'rxjs';
import { MeasurementSession } from '../../models/measurement-session';
import { apiError } from '../../services/api-error';
import { AuthSession } from '../../services/auth-session';
import { SessionApi } from '../../services/session-api';
import { ChartRange } from '../../shared/chart-geometry';
import { dateKey, fullDayLabel, timeLabel } from '../../shared/dates';
import { Icon } from '../../shared/icon';
import { LineChart } from '../../shared/line-chart';
import {
  formatBpm,
  formatDuration,
  sessionDuration,
  summarizeMeasurements,
} from '../../shared/measurement-format';

@Component({
  selector: 'app-session-detail',
  imports: [Icon, LineChart],
  templateUrl: './session-detail.html',
  styleUrl: './session-detail.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SessionDetail {
  private readonly api = inject(SessionApi);
  private readonly auth = inject(AuthSession);
  readonly sessionId = input.required<string>();
  protected readonly session = rxResource({
    params: this.sessionId,
    stream: ({ params }) =>
      forkJoin({
        details: this.api.getSession(params),
        statistics: this.api.getSessionStatistics(params),
        measurements: this.api.getMeasurements(params),
      }),
  });

  protected readonly selecting = linkedSignal<string, boolean>({
    source: this.sessionId,
    computation: () => false,
  });
  protected readonly range = linkedSignal<string, ChartRange | null>({
    source: this.sessionId,
    computation: () => null,
  });

  protected readonly timeline = computed(() => {
    const measurements = this.session.hasValue() ? this.session.value().measurements : [];
    const samples = measurements.map((sample) => ({
      sample,
      time: Date.parse(sample.measuredAtUtc),
    }));
    const from = samples[0]?.time ?? 0;
    const duration = (samples.at(-1)?.time ?? from) - from;
    const points = samples.map(({ sample, time }) => ({
      value: sample.bpm,
      position: duration ? (time - from) / duration : 0.5,
    }));
    return { samples, from, duration, points };
  });

  protected readonly timeAt = (position: number): string => {
    const { from, duration } = this.timeline();
    return timeLabel(from + position * duration, this.auth.timeZone(), duration < 300_000);
  };
  protected readonly ticks = computed(() => {
    const { duration } = this.timeline();
    if (duration === 0) return [this.timeAt(0)];
    const count = Math.min(7, Math.max(2, Math.floor(duration / 1000) + 1));
    return Array.from({ length: count }, (_, index) => this.timeAt(index / (count - 1)));
  });

  protected readonly rangeSummary = computed(() => {
    const range = this.range();
    if (range === null) return null;

    const { samples, from, duration } = this.timeline();
    const start = from + range[0] * duration;
    const end = from + range[1] * duration;
    const timeZone = this.auth.timeZone();
    const seconds = (end - start) / 1000;
    const showSeconds = seconds < 300;
    const selected = samples
      .filter(({ time }) => time >= start && time <= end)
      .map(({ sample }) => sample);
    return {
      label:
        timeLabel(start, timeZone, showSeconds) +
        '–' +
        timeLabel(end, timeZone, showSeconds) +
        ' · ' +
        formatDuration(seconds),
      summary: summarizeMeasurements(selected),
    };
  });

  protected readonly bpm = formatBpm;
  protected readonly error = computed(() =>
    apiError(this.session.error(), 'Nie udało się pobrać szczegółów sesji.'),
  );

  protected formatSession(session: MeasurementSession): string {
    const timeZone = this.auth.timeZone();
    const { startedAtUtc, finishedAtUtc } = session;
    const startDay = dateKey(startedAtUtc, timeZone);
    const short = finishedAtUtc
      ? Date.parse(finishedAtUtc) - Date.parse(startedAtUtc) < 300_000
      : false;
    const start = fullDayLabel(startDay) + ' · ' + timeLabel(startedAtUtc, timeZone, short);
    if (!finishedAtUtc) return start + ' · W toku';
    const endDay = dateKey(finishedAtUtc, timeZone);
    const end = timeLabel(finishedAtUtc, timeZone, short);
    return (
      start +
      '–' +
      (endDay === startDay ? '' : fullDayLabel(endDay) + ' · ') +
      end +
      ' · ' +
      sessionDuration(session)
    );
  }

  protected toggleSelection(): void {
    this.selecting.update((value) => !value);
    if (this.selecting() && !this.range()) this.range.set([0, 1]);
  }

  protected clearRange(): void {
    this.range.set(null);
    this.selecting.set(false);
  }
}
