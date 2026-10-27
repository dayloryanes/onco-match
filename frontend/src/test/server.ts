import { setupServer } from 'msw/node'
import { http, HttpResponse } from 'msw'

export const server = setupServer(
  http.get('*/api/auth/me', () => HttpResponse.json({ code: 'authentication_required' }, { status: 401 })),
  http.get('*/api/auth/csrf', () => HttpResponse.json({ requestToken: 'test-token' })),
)
