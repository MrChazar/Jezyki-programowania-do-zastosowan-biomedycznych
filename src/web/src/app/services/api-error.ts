import { HttpErrorResponse } from '@angular/common/http';

export function apiError(error: unknown, fallback: string): string {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) return 'Nie można połączyć się z API. Spróbuj ponownie.';
    if (error.status === 429) return 'Zbyt wiele prób. Odczekaj chwilę i spróbuj ponownie.';
    if (error.status < 500 && typeof error.error?.message === 'string') return error.error.message;
  }

  return fallback;
}
