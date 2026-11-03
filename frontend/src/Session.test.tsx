import { act, fireEvent, render, screen } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { afterEach, expect, test, vi } from 'vitest'
import App from './App'
import { server } from './test/server'

function session(minutes = 4) {
  return { username: 'viewer', role: 'Viewer', serverTime: '2026-10-27T12:00:00Z',
    expiresAt: `2026-10-27T12:${String(minutes).padStart(2, '0')}:00Z` }
}

afterEach(() => vi.useRealTimers())

test('warning uses server time despite browser clock differences and successful extension preserves the page', async () => {
  server.use(
    http.get('*/api/auth/me', () => HttpResponse.json(session())),
    http.post('*/api/auth/extend-session', () => HttpResponse.json(session(30))),
  )
  render(<App />)
  expect(await screen.findByRole('heading', { name: 'Dashboard' })).toBeVisible()
  expect(await screen.findByText(/Your session expires in 4 minutes/)).toBeVisible()
  fireEvent.click(screen.getByRole('button', { name: 'Stay signed in' }))
  expect(await screen.findByText(/Session expires at/)).toBeVisible()
  expect(screen.queryByText(/Your session expires in/)).not.toBeInTheDocument()
  expect(screen.getByRole('heading', { name: 'Dashboard' })).toBeVisible()
})

test('extension failure preserves the confirmed deadline and offers Retry', async () => {
  server.use(
    http.get('*/api/auth/me', () => HttpResponse.json(session())),
    http.post('*/api/auth/extend-session', () => HttpResponse.error()),
  )
  render(<App />)
  await screen.findByRole('button', { name: 'Stay signed in' })
  const confirmed = screen.getByText(/Session expires at/).textContent
  fireEvent.click(screen.getByRole('button', { name: 'Stay signed in' }))
  expect(await screen.findByText(/Unable to extend your session/)).toBeVisible()
  expect(screen.getByText(/Session expires at/).textContent).toBe(confirmed)
  server.use(http.post('*/api/auth/extend-session', () => HttpResponse.json(session(30))))
  fireEvent.click(screen.getByRole('button', { name: 'Retry session extension' }))
  await vi.waitFor(() => expect(screen.queryByText(/Unable to extend your session/)).not.toBeInTheDocument())
})

test('expired extension requires login', async () => {
  server.use(
    http.get('*/api/auth/me', () => HttpResponse.json(session())),
    http.post('*/api/auth/extend-session', () => HttpResponse.json({}, { status: 401 })),
  )
  render(<App />)
  fireEvent.click(await screen.findByRole('button', { name: 'Stay signed in' }))
  expect(await screen.findByLabelText('Username')).toBeVisible()
})

test('warning appears at five minutes without making keepalive requests', async () => {
  vi.useFakeTimers({ toFake: ['setInterval', 'clearInterval', 'performance'] })
  let checks = 0
  server.use(http.get('*/api/auth/me', () => { checks++; return HttpResponse.json(session(6)) }))
  render(<App />)
  await screen.findByRole('heading', { name: 'Dashboard' })
  expect(screen.queryByRole('button', { name: 'Stay signed in' })).not.toBeInTheDocument()
  await act(async () => { vi.advanceTimersByTime(60_000) })
  expect(screen.getByRole('button', { name: 'Stay signed in' })).toBeVisible()
  expect(checks).toBe(1)
})

test('ordinary requests update confirmed renewal without replacing the Dashboard on focus', async () => {
  let reads = 0
  server.use(
    http.get('*/api/auth/me', () => HttpResponse.json(session())),
    http.get('*/api/study-imports', () => {
      reads++
      return HttpResponse.json({ version: 'initial', isImporting: false, isExhausted: false,
        nextAction: 'initial', totalImportedStudyCount: 0, attempts: [] }, reads > 1 ? {
        headers: { 'X-Session-Expires-At': '2026-10-27T12:30:00Z', 'X-Server-Time': '2026-10-27T12:00:00Z', 'X-Session-Renewed': 'true' },
      } : {})
    }),
  )
  render(<App />)
  await screen.findByRole('button', { name: 'Stay signed in' })
  const dashboard = screen.getByRole('heading', { name: 'Dashboard' })
  fireEvent.click(await screen.findByRole('button', { name: 'Refresh' }))
  await vi.waitFor(() => expect(screen.queryByRole('button', { name: 'Stay signed in' })).not.toBeInTheDocument())
  fireEvent.focus(window)
  await vi.waitFor(() => expect(dashboard).toBeVisible())
  expect(screen.getByRole('heading', { name: 'Dashboard' })).toBe(dashboard)
  expect(reads).toBe(2)
  // A stale focus response must not move the confirmed renewed deadline back.
  expect(screen.queryByRole('button', { name: 'Stay signed in' })).not.toBeInTheDocument()
})

test('a delayed extension cannot overwrite a later confirmed renewal', async () => {
  let release!: () => void
  const waiting = new Promise<void>(resolve => { release = resolve })
  server.use(
    http.get('*/api/auth/me', () => HttpResponse.json(session())),
    http.post('*/api/auth/extend-session', async () => { await waiting; return HttpResponse.json(session(30)) }),
  )
  render(<App />)
  fireEvent.click(await screen.findByRole('button', { name: 'Stay signed in' }))
  await screen.findByRole('button', { name: 'Extending session…' })
  server.use(http.get('*/api/study-imports', () => HttpResponse.json({ version: 'initial', isImporting: false,
    isExhausted: false, nextAction: 'initial', totalImportedStudyCount: 0, attempts: [] }, {
    headers: { 'X-Session-Expires-At': '2026-10-27T12:40:00Z', 'X-Server-Time': '2026-10-27T12:10:00Z', 'X-Session-Renewed': 'true' },
  })))
  fireEvent.click(await screen.findByRole('button', { name: 'Refresh' }))
  await vi.waitFor(() => expect(document.querySelector('.session-controls time')).toHaveAttribute('datetime', '2026-10-27T12:40:00Z'))
  await act(async () => { release() })
  await vi.waitFor(() => expect(document.querySelector('.session-controls time')).toHaveAttribute('datetime', '2026-10-27T12:40:00Z'))
})
