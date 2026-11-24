import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, forkJoin, map, of, switchMap, throwError } from 'rxjs';
import { apiError } from '../../services/api-error';
import { AuthSession } from '../../services/auth-session';
import { loadInOrder } from '../../services/load-in-order';
import { SessionApi } from '../../services/session-api';
import { chartGeometry } from '../../shared/chart-geometry';
import {
  addDays,
  dateKey,
  dateTimeLabel,
  dayLabel,
  earliestDate,
  isCalendarDate,
  shortDayLabel,
  startOfMonth,
  startOfWeek,
  weekLabel,
} from '../../shared/dates';
import { Icon } from '../../shared/icon';
import { LineChart } from '../../shared/line-chart';
import { formatBpm, sessionDuration, summarizeMeasurements } from '../../shared/measurement-format';

@Component({
  selector: 'app-dashboard-page',
  imports: [Icon, LineChart, RouterLink],
  templateUrl: './dashboard-page.html',
  styleUrl: './dashboard-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardPage {
  private readonly auth = inject(AuthSession);
  private readonly api = inject(SessionApi);
  protected readonly user = this.auth.profile;
  protected readonly today = dateKey(new Date(), this.auth.timeZone());
  protected readonly firstDate = earliestDate;
  protected readonly currentWeek = startOfWeek(this.today);
  protected readonly weekStart = signal(this.currentWeek);
  protected readonly allSessions = signal(false);
  protected readonly page = signal(1);

  private readonly days = computed(() =>
    Array.from({ length: 7 }, (_, index) => addDays(this.weekStart(), index)),
  );
  protected readonly weekTitle = computed(() => weekLabel(this.weekStart()));
  protected readonly weekLabels = computed(() => this.days().map(shortDayLabel));

  protected readonly week = rxResource({
    params: this.days,
    stream: ({ params: dates }) => {
      const months = [...new Set(dates.map(startOfMonth))];
      return forkJoin(months.map((month) => this.api.getDates(month))).pipe(
        switchMap((groups) => {
          const available = new Set(groups.flatMap((days) => days.map(({ date }) => date)));
          return loadInOrder(dates, (date) =>
            (available.has(date) ? this.api.getDay(date) : of([])).pipe(
              map((samples) => ({ date, ...summarizeMeasurements(samples) })),
            ),
          );
        }),
      );
    },
  });

  protected readonly recent = rxResource({
    params: () => ({ page: this.page(), pageSize: this.allSessions() ? 10 : 2 }),
    stream: ({ params: { page, pageSize } }) =>
      this.api.getSessions(page, pageSize).pipe(
        switchMap((response) =>
          loadInOrder(response.items, (session) =>
            this.api.getSessionStatistics(session.id).pipe(
              map(({ averageBpm }) => ({ session, averageBpm })),
              catchError((error) => (error.status === 404 ? of(null) : throwError(() => error))),
            ),
          ).pipe(
            map((rows) => ({
              sessions: rows.filter((row) => row !== null),
              totalCount: response.totalCount,
              totalPages: response.totalPages,
            })),
          ),
        ),
      ),
  });

  protected readonly weekError = computed(() =>
    apiError(this.week.error(), 'Nie udało się pobrać pomiarów tygodnia.'),
  );
  protected readonly sessionsError = computed(() =>
    apiError(this.recent.error(), 'Nie udało się pobrać ostatnich sesji.'),
  );

  protected readonly chartPoints = computed(() =>
    this.week.hasValue()
      ? this.week.value().map((day, index) => ({ position: index / 6, value: day.averageBpm }))
      : [],
  );
  private readonly trend = computed(() => chartGeometry(this.chartPoints()));
  protected readonly chartLabel = computed(() => {
    const summaries = (this.week.hasValue() ? this.week.value() : [])
      .filter((day) => day.averageBpm !== null)
      .map((day) => dayLabel(day.date) + ', ' + formatBpm(day.averageBpm) + ' bpm');
    return ['Średnie tętno każdego dnia', ...summaries].join('. ');
  });

  protected readonly metrics = computed(() => {
    const days = this.week.hasValue() ? this.week.value() : [];
    const ordered = days
      .filter((day) => day.averageBpm !== null)
      .sort((a, b) => a.averageBpm! - b.averageBpm!);
    const low = ordered[0];
    const high = ordered[ordered.length - 1];
    const duration = days.reduce((sum, day) => sum + day.durationSeconds, 0);
    const average = duration
      ? days.reduce((sum, day) => sum + day.weightedBpm, 0) / duration
      : null;
    const { line, ticks } = this.trend();
    const marker = (day: (typeof days)[number] | undefined) => {
      if (!line || day?.averageBpm == null) return null;
      const top = ticks[0].value;
      const bottom = ticks[ticks.length - 1].value;
      return {
        x: (1000 * days.indexOf(day)) / 6,
        y: (100 * (top - day.averageBpm)) / (top - bottom),
      };
    };
    return [
      {
        title: 'Średnie tętno',
        value: average,
        detail: 'Średnia z 7 dni',
        path: line,
        marker: null,
      },
      {
        title: 'Najniższa średnia dzienna',
        value: low?.averageBpm,
        detail: low && dayLabel(low.date),
        path: line,
        marker: marker(low),
      },
      {
        title: 'Najwyższa średnia dzienna',
        value: high?.averageBpm,
        detail: high && dayLabel(high.date),
        path: line,
        marker: marker(high),
      },
    ];
  });

  protected readonly bpm = formatBpm;
  protected readonly duration = sessionDuration;
  protected readonly sessionDate = (value: string) => dateKey(value, this.auth.timeZone());
  protected readonly sessionTime = (value: string) => dateTimeLabel(value, this.auth.timeZone());

  protected changeWeek(offset: number): void {
    this.chooseWeek(addDays(this.weekStart(), 7 * offset));
  }

  protected chooseWeek(day: string): void {
    if (isCalendarDate(day) && day <= this.today) this.weekStart.set(startOfWeek(day));
  }

  protected toggleSessionList(): void {
    this.page.set(1);
    this.allSessions.update((expanded) => !expanded);
  }

  protected changePage(offset: number): void {
    this.page.update((page) => page + offset);
  }
}
