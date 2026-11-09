import { setupServer } from 'msw/node'
import { http, HttpResponse } from 'msw'

export const server = setupServer(
  http.get('*/api/match/pairs', () => HttpResponse.json([])),
  http.get('*/api/match/batches', () => HttpResponse.json({ configurationError: null, activeOperation: null, batches: [] })),
  http.get('*/api/auth/me', () => HttpResponse.json({ code: 'authentication_required' }, { status: 401 })),
  http.get('*/api/auth/csrf', () => HttpResponse.json({ requestToken: 'test-token' })),
  http.get('*/api/study-imports', () => HttpResponse.json({ version: 'initial', isImporting: false,
    isExhausted: false, nextAction: 'initial', totalImportedStudyCount: 0, attempts: [] })),
)
