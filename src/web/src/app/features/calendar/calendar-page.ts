import {
  ChangeDetectionStrategy,
  Component,
  computed,
  ElementRef,
  inject,
  viewChild,
} from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { catchError, forkJoin, map, of, throwError } from 'rxjs';
import { apiError } from '../../services/api-error';
import { AuthSession } from '../../services/auth-session';
import { loadInOrder } from '../../services/load-in-order';
import { SessionApi } from '../../services/session-api';
import {
  addDays,
  addMonths,
  dateKey,
  dayLabel,
  earliestDate,
  isCalendarDate,
  latestDate,
  mondayOffset,
  monthLabel,
  startOfMonth,
  timeLabel,
} from '../../shared/dates';
import { Icon } from '../../shared/icon';
import {
  formatBpm,
  formatDuration,
  sessionCount,
  sessionDuration,
  summarizeMeasurements,
} from '../../shared/measurement-format';
import { DayMeasurements } from './day-measurements';
import { SessionDetail } from './session-detail';

@Component({
  selector: 'app-calendar-page',
  imports: [DayMeasurements, Icon, SessionDetail],
  templateUrl: './calendar-page.html',
  styleUrl: './calendar-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CalendarPage {
  private readonly api = inject(SessionApi);
  private readonly auth = inject(AuthSession);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly query = toSignal(this.route.queryParamMap, {
    initialValue: this.route.snapshot.queryParamMap,
  });
  private readonly detail = viewChild<ElementRef<HTMLElement>>('detail');

  protected readonly today = dateKey(new Date(), this.auth.timeZone());
  protected readonly selectedDate = computed(() => {
    const date = this.query().get('date');
    return isCalendarDate(date) ? date : this.today;
  });
  protected readonly selectedSession = computed(() => this.query().get('session'));
  protected readonly month = computed(() => startOfMonth(this.selectedDate()));
  protected readonly monthTitle = computed(() => monthLabel(this.month()));
  protected readonly atFirstMonth = computed(() => this.month() === earliestDate);
  protected readonly atLastMonth = computed(() => this.month() === startOfMonth(latestDate));
  protected readonly weekdays = [
    ['Pn', 'Poniedziałek'],
    ['Wt', 'Wtorek'],
    ['Śr', 'Środa'],
    ['Cz', 'Czwartek'],
    ['Pt', 'Piątek'],
    ['So', 'Sobota'],
    ['Nd', 'Niedziela'],
  ];

  protected readonly dates = rxResource({
    params: this.month,
    stream: ({ params }) => this.api.getDates(params),
  });
  private readonly markedDates = computed(
    () => new Set(this.dates.hasValue() ? this.dates.value().map(({ date }) => date) : []),
  );
  protected readonly cells = computed(() => {
    const month = this.month();
    const leading = mondayOffset(month);
    const nextMonth = addMonths(month, 1);
    return Array.from({ length: 42 }, (_, index) => {
      if (index < leading) return null;
      const date = addDays(month, index - leading);
      if (date >= nextMonth) return null;
      const marked = this.markedDates().has(date);
      return {
        date,
        day: index - leading + 1,
        marked,
        label: dayLabel(date) + (marked ? ', dzień z pomiarami' : ''),
      };
    });
  });

  protected readonly day = rxResource({
    params: this.selectedDate,
    stream: ({ params }) =>
      this.api.getDay(params).pipe(
        map((samples) => ({
          samples,
          summary: summarizeMeasurements(samples),
          sessionIds: [
            ...new Set(samples.flatMap(({ sessionId }) => (sessionId ? [sessionId] : []))),
          ],
        })),
      ),
  });
  protected readonly sessions = rxResource({
    params: () => (this.day.hasValue() ? this.day.value().sessionIds : undefined),
    stream: ({ params }) =>
      loadInOrder(params, (id) =>
        forkJoin({
          session: this.api.getSession(id),
          statistics: this.api.getSessionStatistics(id),
        }).pipe(catchError((error) => (error.status === 404 ? of(null) : throwError(() => error)))),
      ).pipe(map((rows) => rows.filter((row) => row !== null))),
  });
  protected readonly sessionNames = computed(() => {
    const rows = this.sessions.hasValue() ? this.sessions.value() : [];
    return new Map(rows.map(({ session }) => [session.id, session.name]));
  });
  protected readonly dayTitle = computed(() => dayLabel(this.selectedDate()));
  protected readonly dayStats = computed(() => {
    const summary = this.day.hasValue() ? this.day.value().summary : null;
    return [
      { label: 'Średnie tętno', value: summary?.averageBpm },
      { label: 'Minimalne', value: summary?.minimumBpm },
      { label: 'Maksymalne', value: summary?.maximumBpm },
    ];
  });

  protected readonly errorMessage = apiError;
  protected readonly bpm = formatBpm;
  protected readonly duration = formatDuration;
  protected readonly sessionCount = sessionCount;
  protected readonly sessionDuration = sessionDuration;
  protected readonly sessionTime = (value: string) => timeLabel(value, this.auth.timeZone());

  protected changeMonth(offset: number): void {
    this.selectDate(addMonths(this.month(), offset));
  }

  protected selectDate(date: string): void {
    if (!isCalendarDate(date)) return;
    void this.router.navigate([], { relativeTo: this.route, queryParams: { date } });
  }

  protected selectSession(id: string): void {
    const session = this.selectedSession() === id ? null : id;
    void this.router
      .navigate([], { relativeTo: this.route, queryParams: { date: this.selectedDate(), session } })
      .then((changed) => {
        if (changed && session)
          requestAnimationFrame(() =>
            this.detail()?.nativeElement.scrollIntoView({ block: 'nearest' }),
          );
      });
  }
}
