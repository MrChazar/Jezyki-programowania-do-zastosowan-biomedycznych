import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  linkedSignal,
} from '@angular/core';
import { HeartRateMeasurement } from '../../models/measurement-session';
import { AuthSession } from '../../services/auth-session';
import { fullDayLabel, timeLabel } from '../../shared/dates';
import { Icon } from '../../shared/icon';

const measurementsPerPage = 25;
const sampleBpm = new Intl.NumberFormat('pl-PL', { maximumFractionDigits: 6 });

@Component({
  selector: 'app-day-measurements',
  imports: [Icon],
  templateUrl: './day-measurements.html',
  styleUrl: './day-measurements.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DayMeasurements {
  private readonly auth = inject(AuthSession);

  readonly date = input.required<string>();
  readonly measurements = input.required<readonly HeartRateMeasurement[]>();
  readonly sessionNames = input.required<ReadonlyMap<string, string>>();

  private readonly page = linkedSignal({ source: this.measurements, computation: () => 1 });

  protected readonly dateLabel = computed(() => fullDayLabel(this.date()));
  protected readonly start = computed(() => (this.page() - 1) * measurementsPerPage);
  protected readonly end = computed(() =>
    Math.min(this.start() + measurementsPerPage, this.measurements().length),
  );
  protected readonly rows = computed(() => {
    const timeZone = this.auth.timeZone();
    return this.measurements()
      .slice(this.start(), this.end())
      .map((sample) => ({
        ...sample,
        time: timeLabel(sample.measuredAtUtc, timeZone, true),
        formattedBpm: sampleBpm.format(sample.bpm),
      }));
  });

  protected changePage(offset: number): void {
    const lastPage = Math.max(1, Math.ceil(this.measurements().length / measurementsPerPage));
    this.page.update((page) => Math.min(lastPage, Math.max(1, page + offset)));
  }
}
