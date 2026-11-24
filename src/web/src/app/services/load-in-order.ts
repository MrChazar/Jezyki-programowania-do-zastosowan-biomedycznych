import { from, map, mergeMap, Observable, toArray } from 'rxjs';

export function loadInOrder<T, R>(
  items: readonly T[],
  load: (item: T) => Observable<R>,
): Observable<R[]> {
  return from(items).pipe(
    mergeMap((item, index) => load(item).pipe(map((value) => ({ index, value }))), 4),
    toArray(),
    map((results) => results.sort((a, b) => a.index - b.index).map(({ value }) => value)),
  );
}
