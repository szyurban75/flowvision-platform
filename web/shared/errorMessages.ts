/** Error body returned by the platform services: { code, message }. */
export interface ApiErrorBody {
  code?: string;
  message?: string;
}

/**
 * Plain-language text for the errors defined in ADR-001 (409, 503, 500) and for sign-in codes.
 * Technical details are never shown; the services log them.
 */
const messages: Record<string, string> = {
  CONCURRENCY: 'The record was changed by another user. Reload it and try again.',
  SERVICE_UNAVAILABLE: 'The database is not available right now. Try again in a few minutes.',
  INTERNAL_ERROR: 'An unexpected error occurred. Contact support with the time of the action.',
  INVALID_CREDENTIALS: 'Wrong user name or password.',
  PASSWORD_RESET_REQUIRED: 'Your password must be reset by an administrator.',
  INTERFACE_NOT_ALLOWED: 'Your account has no access to this interface.',
  NOT_SIGNED_IN: 'Sign in first.',
};

const fallbackByStatus: Record<number, string> = {
  401: 'Sign in first.',
  403: 'You do not have permission for this action.',
  409: messages.CONCURRENCY,
  503: messages.SERVICE_UNAVAILABLE,
};

/** Returns the message to show the user for a failed request. */
export function plainErrorMessage(status: number, body?: ApiErrorBody | null): string {
  if (body?.code && messages[body.code]) {
    return messages[body.code];
  }
  if (status in fallbackByStatus) {
    return fallbackByStatus[status];
  }
  return messages.INTERNAL_ERROR;
}
