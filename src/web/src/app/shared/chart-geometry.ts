export interface ChartPoint {
  position: number;
  value: number | null;
}

export type ChartRange = readonly [start: number, end: number];

export function chartGeometry(points: readonly ChartPoint[], fill = false) {
  let low = Infinity;
  let high = -Infinity;
  for (const { value } of points) {
    if (value === null) continue;
    low = Math.min(low, value);
    high = Math.max(high, value);
  }

  if (!Number.isFinite(low)) return { ticks: [], line: '', area: '', dots: [] };

  const spread = high - low;
  const step = spread <= 35 ? 10 : spread <= 75 ? 20 : spread <= 150 ? 40 : 50;
  const floor = Math.max(0, Math.floor((low - step / 2) / step) * step);
  const ceiling = Math.ceil((high + step / 2) / step) * step;
  const y = (value: number) => (100 * (ceiling - value)) / (ceiling - floor);
  const ticks = Array.from({ length: (ceiling - floor) / step + 1 }, (_, index) => {
    const value = ceiling - index * step;
    return { value, top: y(value) };
  });

  const lines: string[] = [];
  const areas: string[] = [];
  const dots: { left: number; top: number }[] = [];
  let segment: { x: number; y: number }[] = [];

  const finishSegment = () => {
    if (segment.length > 1) {
      const path = segment.map(({ x, y }) => x.toFixed(2) + ' ' + y.toFixed(2)).join(' L');
      lines.push('M' + path);
      if (fill) {
        areas.push(
          'M' + path + ' L' + segment[segment.length - 1].x + ' 100 L' + segment[0].x + ' 100 Z',
        );
      }
    } else if (segment.length === 1) {
      dots.push({ left: segment[0].x / 10, top: segment[0].y });
    }
    segment = [];
  };

  for (const point of points) {
    if (point.value === null) finishSegment();
    else segment.push({ x: point.position * 1000, y: y(point.value) });
  }
  finishSegment();

  return { ticks, line: lines.join(' '), area: areas.join(' '), dots };
}
