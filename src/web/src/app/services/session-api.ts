import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { defer, EMPTY, expand, map, Observable, reduce } from 'rxjs';
import {
  HeartRateMeasurement,
  HeartRateStatistics,
  MeasurementDate,
  MeasurementSession,
  PagedResponse,
} from '../models/measurement-session';

@Injectable({ providedIn: 'root' })
export class SessionApi {
  private readonly http = inject(HttpClient);

  getSessions(page: number, pageSize: number): Observable<PagedResponse<MeasurementSession>> {
    return this.http.get<PagedResponse<MeasurementSession>>('/api/sessions', {
      params: { page, pageSize },
    });
  }

  getSession(id: string): Observable<MeasurementSession> {
    return this.http.get<MeasurementSession>(`/api/sessions/${encodeURIComponent(id)}`);
  }

  getSessionStatistics(id: string): Observable<HeartRateStatistics> {
    return this.http.get<HeartRateStatistics>(`/api/sessions/${encodeURIComponent(id)}/statistics`);
  }

  getMeasurements(id: string): Observable<HeartRateMeasurement[]> {
    return this.getAllMeasurements(`/api/sessions/${encodeURIComponent(id)}/measurements`);
  }

  getDates(month: string): Observable<MeasurementDate[]> {
    // The API paginates by day, so a page of 31 contains the entire month.
    return this.http
      .get<PagedResponse<MeasurementDate>>('/api/measurements/dates', {
        params: { month, page: 1, pageSize: 31 },
      })
      .pipe(map(({ items }) => items));
  }

  getDay(date: string): Observable<HeartRateMeasurement[]> {
    return this.getAllMeasurements(`/api/measurements/day/${encodeURIComponent(date)}`);
  }

  private getAllMeasurements(url: string): Observable<HeartRateMeasurement[]> {
    const getPage = (page: number) =>
      this.http.get<PagedResponse<HeartRateMeasurement>>(url, { params: { page, pageSize: 100 } });

    return defer(() =>
      getPage(1).pipe(
        expand(({ page, totalPages }) => (page < totalPages ? getPage(page + 1) : EMPTY)),
        reduce((items, page) => {
          items.push(...page.items);
          return items;
        }, [] as HeartRateMeasurement[]),
      ),
    );
  }
}
