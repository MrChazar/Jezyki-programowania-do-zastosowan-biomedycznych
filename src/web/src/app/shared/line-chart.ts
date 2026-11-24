import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { chartGeometry, ChartPoint, ChartRange } from './chart-geometry';

type Handle = 0 | 1;

@Component({
  selector: 'app-line-chart',
  templateUrl: './line-chart.html',
  styleUrl: './line-chart.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LineChart {
  readonly points = input.required<readonly ChartPoint[]>();
  readonly labels = input.required<readonly string[]>();
  readonly label = input.required<string>();
  readonly fill = input(false);
  readonly interactive = input(false);
  readonly range = input<ChartRange | null>(null);
  readonly positionLabel = input<(position: number) => string>(
    (position) => Math.round(position * 100) + '%',
  );
  readonly rangeChange = output<ChartRange | null>();

  protected readonly geometry = computed(() => chartGeometry(this.points(), this.fill()));
  protected readonly handles = [0, 1] as const;
  private drag: {
    id: number;
    anchor: number;
    handle?: Handle;
    previous: ChartRange | null;
  } | null = null;

  protected start(event: PointerEvent, plot: HTMLElement): void {
    if (!this.interactive() || !event.isPrimary || event.button !== 0) return;

    const element = (event.target as Element).closest<HTMLButtonElement>('[data-handle]');
    const handle = element ? (element.dataset['handle'] === '0' ? 0 : 1) : undefined;
    const anchor = this.pointerPosition(event, plot);
    this.drag = { id: event.pointerId, anchor, handle, previous: this.range() };
    if (element) element.focus();
    else this.rangeChange.emit([anchor, anchor]);
    plot.setPointerCapture(event.pointerId);
    event.preventDefault();
  }

  protected move(event: PointerEvent, plot: HTMLElement): void {
    if (this.drag?.id !== event.pointerId) return;

    const { anchor, handle, previous } = this.drag;
    const position = this.pointerPosition(event, plot);
    if (handle !== undefined && previous) this.setHandle(previous, handle, position);
    else this.rangeChange.emit([Math.min(anchor, position), Math.max(anchor, position)]);
  }

  protected end(event: PointerEvent, plot: HTMLElement): void {
    if (this.drag?.id !== event.pointerId) return;
    this.move(event, plot);
    this.drag = null;
  }

  protected cancel(event: PointerEvent): void {
    if (this.drag?.id !== event.pointerId) return;
    this.rangeChange.emit(this.drag.previous);
    this.drag = null;
  }

  protected moveByKey(event: KeyboardEvent, handle: Handle, range: ChartRange): void {
    const step = event.shiftKey ? 0.1 : 0.01;
    let position = range[handle];
    switch (event.key) {
      case 'ArrowLeft':
      case 'ArrowDown':
        position -= step;
        break;
      case 'ArrowRight':
      case 'ArrowUp':
        position += step;
        break;
      case 'Home':
        position = 0;
        break;
      case 'End':
        position = 1;
        break;
      default:
        return;
    }
    event.preventDefault();
    this.setHandle(range, handle, position);
  }

  private setHandle(range: ChartRange, handle: Handle, position: number): void {
    if (handle === 0) this.rangeChange.emit([Math.max(0, Math.min(position, range[1])), range[1]]);
    else this.rangeChange.emit([range[0], Math.min(1, Math.max(position, range[0]))]);
  }

  private pointerPosition(event: PointerEvent, plot: HTMLElement): number {
    const { left, width } = plot.getBoundingClientRect();
    return Math.min(1, Math.max(0, (event.clientX - left) / width));
  }
}
