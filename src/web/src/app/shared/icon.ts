import { ChangeDetectionStrategy, Component, input } from '@angular/core';

type IconName =
  | 'home'
  | 'calendar'
  | 'settings'
  | 'logout'
  | 'menu'
  | 'close'
  | 'panel'
  | 'left'
  | 'right'
  | 'down'
  | 'arrow'
  | 'user'
  | 'mail'
  | 'lock'
  | 'eye'
  | 'eye-off'
  | 'range';

@Component({
  selector: 'app-icon',
  template: `
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      stroke-width="1.85"
      stroke-linecap="round"
      stroke-linejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      @switch (name()) {
        @case ('home') {
          <path d="m3 10 9-7 9 7v10H3zM9 20v-7h6v7" />
        }
        @case ('calendar') {
          <rect x="3" y="5" width="18" height="16" rx="2" />
          <path d="M16 3v4M8 3v4M3 10h18M8 14h1m3 0h1m3 0h1" />
        }
        @case ('settings') {
          <path
            d="m10 3-.7 2.4-2.2 1.3-2.5-.6-2 3.5 1.8 1.8v2.5L2.6 16l2 3.5 2.5-.6 2.2 1.3.7 2.3h4l.7-2.3 2.2-1.3 2.5.6 2-3.5-1.8-1.8v-2.5l1.8-1.8-2-3.5-2.5.6-2.2-1.3L14 3z"
          />
          <circle cx="12" cy="12.7" r="3.2" />
        }
        @case ('logout') {
          <path d="M10 4H4v16h6m3-12 4 4-4 4m-5-4h12" />
        }
        @case ('menu') {
          <path d="M4 6h16M4 12h16M4 18h16" />
        }
        @case ('close') {
          <path d="m6 6 12 12M18 6 6 18" />
        }
        @case ('panel') {
          <rect x="3" y="4" width="18" height="16" rx="2" />
          <path d="M9 4v16m7-11-3 3 3 3" />
        }
        @case ('left') {
          <path d="m15 6-6 6 6 6" />
        }
        @case ('right') {
          <path d="m9 6 6 6-6 6" />
        }
        @case ('down') {
          <path d="m7 10 5 5 5-5" />
        }
        @case ('arrow') {
          <path d="M5 12h14m-5-5 5 5-5 5" />
        }
        @case ('user') {
          <circle cx="12" cy="7" r="3.5" />
          <path d="M4 21v-2a8 8 0 0 1 16 0v2" />
        }
        @case ('mail') {
          <rect x="3" y="5" width="18" height="14" rx="2.5" />
          <path d="m4 7 8 6 8-6" />
        }
        @case ('lock') {
          <rect x="5" y="10" width="14" height="11" rx="2" />
          <path d="M8 10V7a4 4 0 1 1 8 0v3" />
        }
        @case ('eye') {
          <path d="M2 12s3.6-6.5 10-6.5 10 6.5 10 6.5-3.6 6.5-10 6.5S2 12 2 12Z" />
          <circle cx="12" cy="12" r="2.5" />
        }
        @case ('eye-off') {
          <path
            d="m3 3 18 18m-2.3-5.3A16 16 0 0 0 22 12s-3.6-6.5-10-6.5a10 10 0 0 0-3.4.6M6 8a15 15 0 0 0-4 4s3.6 6.5 10 6.5a10 10 0 0 0 4.4-1"
          />
        }
        @case ('range') {
          <path d="M9 3v18m6-18v18M6 6H4v12h2m12-12h2v12h-2M9 6h6m-6 12h6" />
        }
      }
    </svg>
  `,
  styles: `
    :host {
      display: inline-flex;
      width: 1.25rem;
      height: 1.25rem;
      flex: none;
    }

    svg {
      width: 100%;
      height: 100%;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Icon {
  readonly name = input.required<IconName>();
}
