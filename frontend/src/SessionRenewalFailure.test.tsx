import { act, fireEvent, render, screen } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { expect, test, vi } from 'vitest'
import App from './App'
import { server } from './test/server'

test('a failed extension after a concurrent browsing renewal shows a retry error without an imminent-expiry warning', async () => {
  let release!: () => void
  const waiting = new Promise<void>(resolve => { release = resolve })
  const initial = { username: 'viewer', role: 'Viewer', serverTime: '2026-10-27T12:00:00Z',
    expiresAt: '2026-10-27T12:00:20Z' }
  server.use(
    http.get('*/api/auth/me', () => HttpResponse.json(initial)),
    http.post('*/api/auth/extend-session', async () => { await waiting; return HttpResponse.error() }),
  )
  render(<App />)
  fireEvent.click(await screen.findByRole('button', { name: 'Stay signed in' }))
  await screen.findByRole('button', { name: 'Extending session…' })
  server.use(
    http.get('*/api/study-imports', () => HttpResponse.json({ version: 'initial', isImporting: false,
      isExhausted: false, nextAction: 'initial', totalImportedStudyCount: 0, attempts: [] }, {
      headers: { 'X-Session-Expires-At': '2026-10-27T12:30:00Z',
        'X-Server-Time': '2026-10-27T12:00:00Z', 'X-Session-Renewed': 'true' },
    })),
  )
  fireEvent.click(await screen.findByRole('button', { name: 'Refresh' }))
  await vi.waitFor(() => expect(document.querySelector('.session-controls time')).toHaveAttribute('datetime', '2026-10-27T12:30:00Z'))
  await act(async () => { release() })
  expect(await screen.findByRole('alert')).toHaveTextContent(/Unable to/)
  expect(document.querySelector('.session-controls time')).toHaveAttribute('datetime', '2026-10-27T12:30:00Z')
  expect(screen.queryByText(/Stay signed in to continue/)).not.toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Retry session extension' })).toBeEnabled()
  expect(screen.getByRole('heading', { name: 'Dashboard' })).toBeVisible()

  server.use(http.post('*/api/auth/extend-session', () => HttpResponse.json({ ...initial,
    expiresAt: '2026-10-27T12:31:00Z', serverTime: '2026-10-27T12:01:00Z' })))
  fireEvent.click(screen.getByRole('button', { name: 'Retry session extension' }))
  await vi.waitFor(() => expect(screen.queryByRole('alert')).not.toBeInTheDocument())
  expect(screen.queryByRole('button', { name: 'Retry session extension' })).not.toBeInTheDocument()
})

test('a blocked extension keeps the confirmed expiry and a successful retry dismisses its warning', async () => {
  const initial = { username: 'viewer', role: 'Viewer', serverTime: '2026-10-27T12:00:00Z',
    expiresAt: '2026-10-27T12:00:20Z' }
  server.use(
    http.get('*/api/auth/me', () => HttpResponse.json(initial)),
    http.post('*/api/auth/extend-session', () => HttpResponse.error()),
  )
  render(<App />)
  fireEvent.click(await screen.findByRole('button', { name: 'Stay signed in' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('Unable to extend your session with this request.')
  expect(document.querySelector('.session-controls time')).toHaveAttribute('datetime', initial.expiresAt)
  expect(screen.getByText(/Stay signed in to continue/)).toBeVisible()
  server.use(http.post('*/api/auth/extend-session', () => HttpResponse.json({ ...initial,
    expiresAt: '2026-10-27T12:30:00Z' })))
  fireEvent.click(screen.getByRole('button', { name: 'Retry session extension' }))
  await vi.waitFor(() => expect(screen.queryByRole('alert')).not.toBeInTheDocument())
  expect(screen.queryByText(/Stay signed in to continue/)).not.toBeInTheDocument()
})
