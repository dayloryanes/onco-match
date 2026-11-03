import { act, fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { expect, test } from 'vitest'
import App from './App'
import { server } from './test/server'

const batch = { batchId: 'batch1', version: 'v1', status: 'notStarted', totalPairCount: 10, completedPairCount: 0,
  failedPairCount: 0, unfinishedPairCount: 10, activePairCount: 0, activeOperation: null, availableActions: ['start'], pairs: [] }

test('Operator starts a batch and reconciles a broken stream without resubmitting', async () => {
  let posts = 0
  server.use(
    http.get('*/api/auth/me', () => HttpResponse.json({ username: 'operator', role: 'Operator' })),
    http.get('*/api/match/batches', () => HttpResponse.json({ configurationError: null, batches: [posts ? { ...batch, status: 'unfinished', availableActions: [] } : batch] })),
    http.get('*/api/match/batches/batch1', () => HttpResponse.json(batch)),
    http.post('*/api/match/batches/batch1/operations', async ({ request }) => {
      posts++
      expect(request.headers.get('X-CSRF-TOKEN')).toBe('test-token')
      expect(await request.json()).toEqual({ action: 'start', expectedVersion: 'v1' })
      return new HttpResponse('{"type":"bad-event"}\n', { headers: { 'Content-Type': 'application/x-ndjson' } })
    }),
  )
  const user = userEvent.setup()
  render(<App />)
  await user.click(await screen.findByRole('button', { name: 'Start match' }))
  expect(await screen.findByText(/Unable to confirm live progress/)).toBeVisible()
  expect(posts).toBe(1)
  expect(screen.queryByRole('button', { name: 'Start match' })).not.toBeInTheDocument()
})

test('Viewer can inspect shared batch counts but cannot start a match', async () => {
  server.use(
    http.get('*/api/auth/me', () => HttpResponse.json({ username: 'viewer', role: 'Viewer' })),
    http.get('*/api/match/batches', () => HttpResponse.json({ configurationError: null, batches: [batch] })),
  )
  render(<App />)
  expect(await screen.findByText('0/10 completed')).toBeVisible()
  expect(screen.queryByRole('button', { name: 'Start match' })).not.toBeInTheDocument()
})

test('Live progress survives a successful focus check and assessment inspection stays on Dashboard', async () => {
  let output: ReadableStreamDefaultController<Uint8Array> | undefined
  let cancelled = false
  const pair = { batchId: 'batch1', patientId: 'P01', nctId: 'NCT00000001', state: 'completed', pairSummary: 'conditionLabelAlignmentUndetermined', structuredChecks: null, latestAttempt: null }
  const full = { ...batch, pairs: [pair] }
  const encoder = new TextEncoder()
  const event = (type: string, sequence: number) => JSON.stringify({ type, sequence, batchId: 'batch1', operationId: 'op1', version: `v${sequence}`, serverTime: '2026-11-03T12:00:00Z', status: 'running', totalPairCount: 10, completedPairCount: 1, failedPairCount: 0, unfinishedPairCount: 8, activePairCount: 1, patientId: null, nctId: null }) + '\n'
  server.use(
    http.get('*/api/auth/me', () => HttpResponse.json({ username: 'operator', role: 'Operator' })),
    http.get('*/api/match/batches', () => HttpResponse.json({ configurationError: null, batches: [batch] })),
    http.get('*/api/match/batches/batch1', () => HttpResponse.json(full)),
    http.get('*/api/match/pairs/P01/NCT00000001', () => HttpResponse.json({ ...pair, inputSnapshot: null, conditionLabels: [], skipReason: 'No condition text supplied; inference skipped.', assessmentLimits: 'Assessment limits.' })),
    http.post('*/api/match/batches/batch1/operations', () => new HttpResponse(new ReadableStream<Uint8Array>({
      start(controller) { output = controller; controller.enqueue(encoder.encode(event('operationStarted', 1))) },
      cancel() { cancelled = true },
    }), { headers: { 'Content-Type': 'application/x-ndjson' } })),
  )
  const user = userEvent.setup()
  render(<App />)
  await user.click(await screen.findByRole('button', { name: 'Start match' }))
  expect(await screen.findByRole('button', { name: 'Cancel match' })).toBeVisible()
  fireEvent.focus(window)
  expect(await screen.findByRole('heading', { name: 'Dashboard' })).toBeVisible()
  expect(cancelled).toBe(false)
  await user.click(screen.getByRole('button', { name: 'Inspect P01 NCT00000001' }))
  expect(await screen.findByRole('region', { name: 'Assessment P01 NCT00000001' })).toBeVisible()
  expect(window.location.pathname).toBe('/')
  expect(cancelled).toBe(false)
  await act(async () => { output!.enqueue(encoder.encode(event('operationFinished', 2))); output!.close() })
  expect(await screen.findByRole('button', { name: 'Start match' })).toBeEnabled()
})
