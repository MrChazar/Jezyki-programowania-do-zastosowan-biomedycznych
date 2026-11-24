import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  ElementRef,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { rxResource, takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { finalize, fromEvent, map, tap } from 'rxjs';
import { apiError } from '../../services/api-error';
import { AuthSession } from '../../services/auth-session';
import { Icon } from '../../shared/icon';

const sidebarKey = 'heartrate.sidebar-collapsed';

function sidebarPreference(): boolean {
  try {
    return localStorage.getItem(sidebarKey) === 'true';
  } catch {
    return false;
  }
}

@Component({
  selector: 'app-shell',
  imports: [Icon, ReactiveFormsModule, RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './app-shell.html',
  styleUrl: './app-shell.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '(document:keydown.escape)': 'closeDrawer()' },
})
export class AppShell {
  private readonly auth = inject(AuthSession);
  private readonly destroyRef = inject(DestroyRef);
  private readonly drawerOpen = signal(false);
  private readonly media = window.matchMedia('(max-width: 760px)');
  private readonly mobile = toSignal(
    fromEvent<MediaQueryListEvent>(this.media, 'change').pipe(
      map((event) => event.matches),
      tap((matches) => {
        if (!matches) this.drawerOpen.set(false);
      }),
    ),
    { initialValue: this.media.matches },
  );
  private readonly mobileToggle = viewChild<ElementRef<HTMLButtonElement>>('mobileToggle');
  private readonly drawerClose = viewChild<ElementRef<HTMLButtonElement>>('drawerClose');
  private readonly settings = viewChild.required<ElementRef<HTMLDialogElement>>('settings');

  protected readonly mobileOpen = computed(() => this.mobile() && this.drawerOpen());
  protected readonly mobileHidden = computed(() => this.mobile() && !this.mobileOpen());
  protected readonly collapsed = signal(sidebarPreference());
  protected readonly profile = this.auth.profile;
  protected readonly profileRequest = rxResource({ stream: () => this.auth.getProfile() });
  protected readonly profileError = computed(() =>
    apiError(this.profileRequest.error(), 'Nie udało się pobrać profilu.'),
  );
  protected readonly signingOut = signal(false);
  protected readonly saving = signal(false);
  protected readonly settingsError = signal('');
  protected readonly profileForm = inject(FormBuilder).nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    surname: ['', [Validators.maxLength(100)]],
  });

  protected toggleSidebar(): void {
    this.collapsed.update((value) => !value);
    try {
      localStorage.setItem(sidebarKey, String(this.collapsed()));
    } catch {
      // The layout can still be changed without persistent storage.
    }
  }

  protected openDrawer(): void {
    this.drawerOpen.set(true);
    requestAnimationFrame(() => this.drawerClose()?.nativeElement.focus());
  }

  protected closeDrawer(): void {
    if (!this.drawerOpen()) return;
    this.drawerOpen.set(false);
    if (this.mobile()) requestAnimationFrame(() => this.mobileToggle()?.nativeElement.focus());
  }

  protected openSettings(): void {
    const profile = this.profile();
    if (!profile) return;
    this.closeDrawer();
    this.profileForm.reset({ name: profile.name, surname: profile.surname });
    this.settingsError.set('');
    this.settings().nativeElement.showModal();
  }

  protected saveSettings(): void {
    this.profileForm.markAllAsTouched();
    const { name, surname } = this.profileForm.getRawValue();
    if (this.profileForm.invalid || !name.trim() || this.saving()) return;

    this.saving.set(true);
    this.settingsError.set('');
    this.auth
      .updateProfile(name.trim(), surname.trim())
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.saving.set(false)),
      )
      .subscribe({
        next: () => this.settings().nativeElement.close(),
        error: (error) => this.settingsError.set(apiError(error, 'Nie udało się zapisać profilu.')),
      });
  }

  protected logout(): void {
    if (this.signingOut()) return;
    this.signingOut.set(true);
    this.auth
      .logout()
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.signingOut.set(false)),
      )
      .subscribe();
  }
}
