import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { expect, test } from 'vitest'
import App from './App'
import { server } from './test/server'

const initial = { version: 'v1', isImporting: false, isExhausted: false, nextAction: 'initial', totalImportedStudyCount: 0, attempts: [] }
const study = { nctId: 'NCT00000001', title: ' Cancer study ', recruitmentStatus: 'RECRUITING', studyType: 'INTERVENTIONAL', conditions: ['Cancer', ''] }
const completed = {
  ...initial, version: 'v2', isExhausted: true, nextAction: 'none', totalImportedStudyCount: 1,
  attempts: [{ id: 'a1', startedAt: '2026-11-02T12:00:00Z', finishedAt: '2026-11-02T12:00:01Z', status: 'completed', retryOfAttemptId: null,
    fetchedCount: 3, importedCount: 1, duplicateCount: 1, rejectedCount: 1, importedNctIds: ['NCT00000001'], duplicateNctIds: ['NCT00000001'],
    rejections: [{ recordNumber: 3, nctId: null, code: 'invalid_record', reason: 'Record must be an object.' }], failureCode: null }],
}
function signedIn(role = 'Operator') {
  server.use(http.get('*/api/auth/me', () => HttpResponse.json({ username: role.toLowerCase(), role })))
}

test('Operator explicitly imports a page, inspects counts and rejections, and browses stored study details', async () => {
  signedIn()
  let posts = 0
  server.use(
    http.get('*/api/study-imports', () => HttpResponse.json(initial)),
    http.post('*/api/study-imports', async ({ request }) => {
      expect(request.headers.get('X-CSRF-TOKEN')).toBe('test-token')
      expect(await request.json()).toEqual({ expectedVersion: 'v1' })
      posts++
      return HttpResponse.json(completed)
    }),
    http.get('*/api/studies', () => HttpResponse.json([study])),
    http.get('*/api/studies/NCT00000001', () => HttpResponse.json({ ...study, briefSummary: '<script>source text</script>', minimumAge: null, maximumAge: 'N/A', sex: 'ALL', registryUrl: 'https://clinicaltrials.gov/study/NCT00000001' })),
  )
  const user = userEvent.setup()
  render(<App />)
  await user.click(await screen.findByRole('button', { name: 'Import first 10' }))
  expect(await screen.findByText('No more registry pages.')).toBeVisible()
  const completedImport = screen.getByRole('listitem', { name: 'Completed import' })
  expect(completedImport).toHaveTextContent('Record 3: Record must be an object.')
  expect(completedImport).toHaveTextContent('Fetched: 3 · Imported: 1 · Duplicates: 1 · Rejected: 1')
  expect(posts).toBe(1)
  await user.click(screen.getByRole('link', { name: 'Studies' }))
  const studyLink = await screen.findByRole('link', { name: 'NCT00000001' })
  expect(screen.getByText('Recruiting')).toBeVisible()
  expect(screen.getByText('Interventional')).toBeVisible()
  expect(document.title).toBe('Studies — OncoMatch')
  await user.click(studyLink)
  expect(await screen.findByRole('heading', { name: 'Study NCT00000001' })).toBeVisible()
  expect(screen.getByText('<script>source text</script>')).toBeVisible()
  expect(screen.getByText('All')).toBeVisible()
  expect(screen.getByText('Recruiting')).toBeVisible()
  expect(screen.getByText('Interventional')).toBeVisible()
  expect(screen.getAllByText('Not available').length).toBeGreaterThan(0)
  expect(screen.getByRole('link', { name: 'View on ClinicalTrials.gov' })).toHaveAttribute('href', 'https://clinicaltrials.gov/study/NCT00000001')
  expect(document.title).toBe('Study NCT00000001 — OncoMatch')
})

test.each([
  ['NOT_YET_RECRUITING', 'OBSERVATIONAL', 'Not yet recruiting', 'Observational', 'MALE', 'Male'],
  ['FUTURE_STATUS', 'FUTURE_TYPE', 'FUTURE_STATUS', 'FUTURE_TYPE', 'FEMALE', 'Female'],
])('Study codes %s and %s have consistent list and detail labels', async (status, type, statusLabel, typeLabel, sex, sexLabel) => {
  signedIn('Viewer')
  server.use(
    http.get('*/api/studies', () => HttpResponse.json([{ ...study, recruitmentStatus: status, studyType: type, conditions: [] }])),
    http.get('*/api/studies/NCT00000001', () => HttpResponse.json({ ...study, recruitmentStatus: status, studyType: type, conditions: null, briefSummary: 'Source TEXT', minimumAge: null, maximumAge: null, sex, registryUrl: 'https://clinicaltrials.gov/study/NCT00000001' })),
  )
  const user = userEvent.setup()
  render(<App />)
  await user.click(await screen.findByRole('link', { name: 'Studies' }))
  const link = await screen.findByRole('link', { name: study.nctId })
  expect(screen.getByText(statusLabel)).toBeVisible()
  expect(screen.getByText(typeLabel)).toBeVisible()
  expect(screen.getByText('Not available')).toBeVisible()
  await user.click(link)
  await screen.findByRole('heading', { name: `Study ${study.nctId}` })
  expect(screen.getByText(statusLabel)).toBeVisible()
  expect(screen.getByText(typeLabel)).toBeVisible()
  expect(screen.getByText('Source TEXT')).toBeVisible()
  expect(screen.getByText(sexLabel)).toBeVisible()
})

test('Viewer reads history and Refresh without import controls or registry mutations', async () => {
  signedIn('Viewer')
  let reads = 0
  server.use(http.get('*/api/study-imports', () => { reads++; return HttpResponse.json(reads === 1 ? initial : completed) }))
  const user = userEvent.setup()
  render(<App />)
  expect(await screen.findByText('No import attempts yet.')).toBeVisible()
  expect(screen.queryByRole('button', { name: /Import first|Load Next|Retry import/ })).not.toBeInTheDocument()
  await user.click(screen.getByRole('button', { name: 'Refresh' }))
  expect(await screen.findByRole('listitem', { name: 'Completed import' })).toHaveTextContent('Fetched: 3 · Imported: 1 · Duplicates: 1 · Rejected: 1')
  expect(reads).toBe(2)
})

test('A failed history Refresh preserves the cards but blocks importing until history is confirmed again', async () => {
  signedIn()
  let reads = 0
  let posts = 0
  server.use(
    http.get('*/api/study-imports', () => ++reads === 2 ? new HttpResponse(null, { status: 503 }) : HttpResponse.json({ ...completed, isExhausted: false, nextAction: 'next' })),
    http.post('*/api/study-imports', () => { posts++; return HttpResponse.json(completed) }),
  )
  const user = userEvent.setup()
  render(<App />)
  expect(await screen.findByRole('button', { name: 'Load Next 10' })).toBeEnabled()
  const card = screen.getByRole('listitem', { name: 'Completed import' })
  await user.click(screen.getByRole('button', { name: 'Refresh' }))
  expect(await screen.findByText('Unable to load import history.')).toBeVisible()
  expect(screen.getByRole('listitem', { name: 'Completed import' })).toBe(card)
  expect(screen.getByRole('button', { name: 'Load Next 10' })).toBeDisabled()
  await user.click(screen.getByRole('button', { name: 'Load Next 10' }))
  expect(posts).toBe(0)
  await user.click(screen.getByRole('button', { name: 'Refresh' }))
  await waitFor(() => expect(screen.getByRole('button', { name: 'Load Next 10' })).toBeEnabled())
  await user.click(screen.getByRole('button', { name: 'Load Next 10' }))
  expect(await screen.findByText('No more registry pages.')).toBeVisible()
  expect(posts).toBe(1)
})

test.each(['import_state_changed', 'import_in_progress', 'registry_unavailable', 'lost-response'])('import failure %s reconciles history without automatic resubmission', async code => {
  signedIn()
  let posts = 0
  let reads = 0
  server.use(
    http.get('*/api/study-imports', () => { reads++; return HttpResponse.json(reads === 1 ? initial : completed) }),
    http.post('*/api/study-imports', () => { posts++; return code === 'lost-response' ? HttpResponse.error() : HttpResponse.json({ code }, { status: code.startsWith('import_') ? 409 : 502 }) }),
  )
  const user = userEvent.setup()
  render(<App />)
  await user.click(await screen.findByRole('button', { name: 'Import first 10' }))
  expect(await screen.findByRole('listitem', { name: 'Completed import' })).toHaveTextContent('Fetched: 3 · Imported: 1 · Duplicates: 1 · Rejected: 1')
  expect(screen.getByRole('alert')).toBeVisible()
  expect(posts).toBe(1)
  expect(reads).toBe(2)
})

test('failed reconciliation removes import controls until an explicit history retrieval succeeds', async () => {
  signedIn()
  let reads = 0
  let posts = 0
  server.use(
    http.get('*/api/study-imports', () => ++reads === 1 ? HttpResponse.json(initial) : HttpResponse.error()),
    http.post('*/api/study-imports', () => { posts++; return HttpResponse.error() }),
  )
  const user = userEvent.setup()
  render(<App />)
  await user.click(await screen.findByRole('button', { name: 'Import first 10' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('Unable to reload import history.')
  expect(screen.queryByRole('button', { name: 'Import first 10' })).not.toBeInTheDocument()
  server.use(http.get('*/api/study-imports', () => HttpResponse.json(initial)))
  await user.click(screen.getByRole('button', { name: 'Retry' }))
  expect(await screen.findByRole('button', { name: 'Import first 10' })).toBeEnabled()
  expect(posts).toBe(1)
})

test.each(['next', 'retry'])('zero-import pages and failures expose the explicit %s action', async nextAction => {
  signedIn()
  server.use(http.get('*/api/study-imports', () => HttpResponse.json({ ...completed, isExhausted: false, nextAction,
    attempts: [{ ...completed.attempts[0], status: nextAction === 'retry' ? 'failed' : 'completed',
      fetchedCount: nextAction === 'retry' ? null : 2, importedCount: nextAction === 'retry' ? null : 0,
      duplicateCount: nextAction === 'retry' ? null : 1, rejectedCount: nextAction === 'retry' ? null : 1 }] })))
  render(<App />)
  expect(await screen.findByRole('button', { name: nextAction === 'retry' ? 'Retry import' : 'Load Next 10' })).toBeEnabled()
  if (nextAction === 'retry') expect(screen.getByRole('listitem', { name: 'Failed import' })).toHaveTextContent('Fetched: Not available')
})

test.each(['navigate', 'logout'])('leaving Dashboard by %s aborts an active import and discards its late response', async action => {
  signedIn()
  let release!: () => void
  let requestSignal: AbortSignal | undefined
  const wait = new Promise<void>(resolve => { release = resolve })
  server.use(
    http.post('*/api/study-imports', async ({ request }) => { requestSignal = request.signal; await wait; return HttpResponse.json(completed) }),
    http.get('*/api/studies', () => HttpResponse.json([])),
    http.post('*/api/auth/logout', () => new HttpResponse(null, { status: 204 })),
  )
  const user = userEvent.setup()
  render(<App />)
  await user.click(await screen.findByRole('button', { name: 'Import first 10' }))
  expect(await screen.findByRole('button', { name: 'Importing…' })).toBeDisabled()
  if (action === 'navigate') {
    await user.click(screen.getByRole('link', { name: 'Studies' }))
    await screen.findByRole('heading', { name: 'Studies' })
  } else {
    await user.click(screen.getByRole('button', { name: 'Log out' }))
    await screen.findByLabelText('Username')
  }
  expect(requestSignal?.aborted).toBe(true)
  await act(async () => release())
  expect(screen.queryByText('Completed import')).not.toBeInTheDocument()
})

test('checking the same session on focus preserves an active Dashboard import', async () => {
  signedIn()
  let release!: () => void
  let requestSignal: AbortSignal | undefined
  const wait = new Promise<void>(resolve => { release = resolve })
  server.use(http.post('*/api/study-imports', async ({ request }) => {
    requestSignal = request.signal
    await wait
    return HttpResponse.json(completed)
  }))
  const user = userEvent.setup()
  render(<App />)
  await user.click(await screen.findByRole('button', { name: 'Import first 10' }))
  await screen.findByRole('button', { name: 'Importing…' })
  fireEvent.focus(window)
  await screen.findByRole('heading', { name: 'Dashboard' })
  try { expect(requestSignal?.aborted).toBe(false) }
  finally { await act(async () => release()) }
  expect(await screen.findByText('No more registry pages.')).toBeVisible()
})

test('confirmed session expiry on focus aborts the import and a late response cannot restore protected data', async () => {
  signedIn()
  let release!: () => void
  let requestSignal: AbortSignal | undefined
  const wait = new Promise<void>(resolve => { release = resolve })
  server.use(http.post('*/api/study-imports', async ({ request }) => {
    requestSignal = request.signal; await wait; return HttpResponse.json(completed)
  }))
  const user = userEvent.setup()
  render(<App />)
  await user.click(await screen.findByRole('button', { name: 'Import first 10' }))
  await screen.findByRole('button', { name: 'Importing…' })
  server.use(http.get('*/api/auth/me', () => HttpResponse.json({}, { status: 401 })))
  fireEvent.focus(window)
  await screen.findByLabelText('Username')
  expect(requestSignal?.aborted).toBe(true)
  await act(async () => release())
  expect(screen.queryByRole('heading', { name: 'Dashboard' })).not.toBeInTheDocument()
  expect(screen.queryByText('Completed import')).not.toBeInTheDocument()
})

test('a protected study deep link returns after login and session expiry clears study data', async () => {
  window.history.replaceState(null, '', '/studies/NCT00000001')
  server.use(
    http.post('*/api/auth/login', () => HttpResponse.json({ username: 'viewer', role: 'Viewer' })),
    http.get('*/api/studies/NCT00000001', () => HttpResponse.json({ ...study, registryUrl: 'https://clinicaltrials.gov/study/NCT00000001' })),
  )
  const user = userEvent.setup()
  render(<App />)
  await user.type(await screen.findByLabelText('Username'), 'viewer')
  await user.type(screen.getByLabelText('Password'), 'ViewerDemo1!')
  await user.click(screen.getByRole('button', { name: 'Log in' }))
  expect(await screen.findByRole('heading', { name: 'Study NCT00000001' })).toBeVisible()
  server.use(http.get('*/api/studies', () => HttpResponse.json({}, { status: 401 })))
  await user.click(screen.getByRole('link', { name: 'Studies' }))
  expect(await screen.findByLabelText('Username')).toBeVisible()
  expect(screen.queryByText('Cancer study')).not.toBeInTheDocument()
})

test('study retrieval errors offer Retry and a successful empty response is distinct', async () => {
  signedIn('Viewer')
  window.history.replaceState(null, '', '/studies')
  let posts = 0
  server.use(
    http.get('*/api/studies', () => HttpResponse.json({}, { status: 503 })),
    http.post('*/api/study-imports', () => { posts++; return HttpResponse.json(completed) }),
  )
  const user = userEvent.setup()
  render(<App />)
  expect(await screen.findByRole('alert')).toHaveTextContent('Unable to load study information.')
  expect(screen.queryByText('No studies imported yet.')).not.toBeInTheDocument()
  server.use(http.get('*/api/studies', () => HttpResponse.json([])))
  await user.click(screen.getByRole('button', { name: 'Retry' }))
  expect(await screen.findByText('No studies imported yet.')).toBeVisible()
  expect(posts).toBe(0)
})

test('a study miss displays Study not found', async () => {
  signedIn('Viewer')
  window.history.replaceState(null, '', '/studies/NCT00000000')
  server.use(http.get('*/api/studies/NCT00000000', () => HttpResponse.json({}, { status: 404 })))
  render(<App />)
  expect(await screen.findByRole('heading', { name: 'Study not found' })).toBeVisible()
})
