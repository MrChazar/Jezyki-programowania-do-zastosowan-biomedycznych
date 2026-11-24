import { Location } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize, Observable } from 'rxjs';
import { apiError } from '../../services/api-error';
import { AuthSession } from '../../services/auth-session';
import { Icon } from '../../shared/icon';

@Component({
  selector: 'app-auth-page',
  imports: [Icon, ReactiveFormsModule, RouterLink],
  templateUrl: './auth-page.html',
  styleUrl: './auth-page.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuthPage {
  private readonly auth = inject(AuthSession);
  private readonly destroyRef = inject(DestroyRef);
  private readonly router = inject(Router);
  private readonly navigationState = inject(Location).getState() as { registeredEmail?: string };

  protected readonly registering = inject(ActivatedRoute).snapshot.data['register'] === true;
  protected readonly registeredEmail = this.registering
    ? ''
    : this.navigationState?.registeredEmail;
  protected readonly form = inject(FormBuilder).nonNullable.group({
    name: ['', this.registering ? [Validators.required, Validators.maxLength(100)] : []],
    email: [
      this.registeredEmail ?? '',
      [Validators.required, Validators.email, Validators.maxLength(254)],
    ],
    password: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(128)]],
    confirmation: ['', this.registering ? [Validators.required] : []],
  });
  protected readonly visible = signal(false);
  protected readonly confirmationVisible = signal(false);
  protected readonly busy = signal(false);
  protected readonly error = signal('');

  protected submit(): void {
    if (this.busy()) return;

    this.form.markAllAsTouched();
    this.error.set('');
    const { name, email, password, confirmation } = this.form.getRawValue();
    if (this.form.invalid || (this.registering && (!name.trim() || password !== confirmation))) {
      return;
    }

    this.busy.set(true);
    const request: Observable<unknown> = this.registering
      ? this.auth.register({ name: name.trim(), email: email.trim(), password })
      : this.auth.login({ email: email.trim(), password });

    request
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.busy.set(false)),
      )
      .subscribe({
        next: () => {
          if (this.registering) {
            void this.router.navigate(['/logowanie'], { state: { registeredEmail: email.trim() } });
          } else {
            void this.router.navigate(['/pulpit'], { replaceUrl: true });
          }
        },
        error: (error: HttpErrorResponse) =>
          this.error.set(
            error.status === 401
              ? 'Adres e-mail lub hasło są nieprawidłowe.'
              : apiError(
                  error,
                  this.registering ? 'Nie udało się utworzyć konta.' : 'Nie udało się zalogować.',
                ),
          ),
      });
  }
}
