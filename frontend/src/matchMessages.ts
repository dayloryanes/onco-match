export type MatchNotice = { text: string; tone: 'status' | 'alert' }

export function stoppedMatchNotice(reason: string | null): MatchNotice {
  const reasons: Record<string, string> = {
    cancelled: 'Match cancelled.',
    sessionExpired: 'Match stopped because your session expired.',
    logout: 'Match stopped because you logged out.',
    disconnected: 'Match stopped because the connection was lost.',
    inferenceUnavailable: 'Match stopped because the local inference service or model is unavailable. Check the configured model and route.',
    internalError: 'Match stopped because of an unexpected error.',
  }
  return { text: `${reasons[reason ?? ''] ?? 'Match stopped.'} Saved results are retained.`, tone: reason === 'cancelled' ? 'status' : 'alert' }
}

export function matchRequestMessage(code: string, fallback: string) {
  const messages: Record<string, string> = {
    operation_in_progress: 'Another match operation is running. Use Refresh to check its progress.',
    batch_state_changed: 'This batch changed in another session. Review its refreshed status before continuing.',
    no_work_for_action: 'No work is available for this action.',
    batch_not_found: 'This batch is no longer available. Use Refresh to update the dashboard.',
    operation_not_found: 'This match operation is no longer available. Use Refresh to check the batch status.',
    access_denied: 'Your account cannot perform this match action.',
    csrf_invalid: 'Refresh the page and try again.',
    invalid_request: 'Unable to start match because the request is invalid. Use Refresh before trying again.',
  }
  return messages[code] ?? fallback
}
