export const importErrors: Record<string, string> = {
  import_in_progress: 'Another import is running. Refresh to check its progress.',
  import_state_changed: 'Import state changed in another session. Review the refreshed history before importing.',
  imports_exhausted: 'No more registry pages.',
  registry_timeout: 'The registry request timed out. You can explicitly retry.',
  registry_unavailable: 'The registry is unavailable. You can explicitly retry.',
  registry_invalid_response: 'The registry returned an invalid response. You can explicitly retry.',
  csrf_invalid: 'Refresh the page and try again.',
  access_denied: 'Your account cannot import studies.',
}
