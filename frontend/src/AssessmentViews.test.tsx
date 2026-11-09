import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { expect, test } from 'vitest'
import App from './App'
import { server } from './test/server'

const patient = { id: 'P001', ageYears: 42, sex: 'female', cancerType: 'Demo cancer',
  stage: { status: 'unknown', value: null, classificationSystem: null }, diseaseState: { status: 'notApplicable', value: null } }
const study = { nctId: 'NCT00000001', title: 'Constructed test study', recruitmentStatus: 'RECRUITING', studyType: 'INTERVENTIONAL',
  conditions: [], briefSummary: null, minimumAge: null, maximumAge: null, sex: 'ALL', registryUrl: 'https://clinicaltrials.gov/study/NCT00000001' }
const pair = { batchId: 'test-batch', patientId: patient.id, nctId: study.nctId, state: 'completed',
  pairSummary: 'conditionLabelAlignmentUndetermined', structuredChecks: null, latestAttempt: null }
const detail = { ...pair, inputSnapshot: { patient, study }, conditionLabels: [], skipReason: 'No condition text supplied; inference skipped.', attemptHistory: [] }

function setup(path: string, role = 'Viewer') {
  window.history.replaceState(null, '', path)
  server.use(
    http.get('*/api/auth/me', () => HttpResponse.json({ username: role.toLowerCase(), role })),
    http.get('*/api/patients/P001', () => HttpResponse.json(patient)),
    http.get('*/api/studies/NCT00000001', () => HttpResponse.json(study)),
    http.get('*/api/match/pairs', () => HttpResponse.json([pair])),
    http.get('*/api/match/pairs/P001/NCT00000001', () => HttpResponse.json(detail)),
  )
}

test.each([
  ['/patients/P001', 'patientId', 'P001', 'Viewer'], ['/studies/NCT00000001', 'nctId', 'NCT00000001', 'Viewer'],
  ['/patients/P001', 'patientId', 'P001', 'Operator'], ['/studies/NCT00000001', 'nctId', 'NCT00000001', 'Operator'],
])('%s exposes saved assessments for %s to %s (%s)', async (path, filter, id, role) => {
  setup(path, role)
  let query = ''
  server.use(http.get('*/api/match/pairs', ({ request }) => { query = new URL(request.url).search; return HttpResponse.json([pair]) }))
  const user = userEvent.setup()
  render(<App />)
  await user.click(await screen.findByRole('button', { name: 'Inspect P001 NCT00000001' }))
  expect(query).toBe(`?${filter}=${id}`)
  expect(screen.getByRole('button', { name: 'Close details P001 NCT00000001' })).toHaveTextContent(/^Close details$/)
  const inspection = await screen.findByRole('region', { name: 'Assessment P001 NCT00000001' })
  expect(within(inspection).getByText(detail.skipReason)).toBeVisible()
  await user.click(within(inspection).getByText('Exact input snapshot'))
  expect(within(inspection).getByRole('region', { name: 'Patient snapshot' })).toHaveTextContent('Not applicable')
  const notice = screen.getByText(/This assessment checks structured age and sex/)
  expect(notice).toHaveClass('notice')
  expect(screen.getAllByText(/This assessment checks structured age and sex/)).toHaveLength(1)
  expect(screen.queryByRole('button', { name: /Start match|Continue unfinished|Retry failed|Cancel match/ })).not.toBeInTheDocument()
  await user.click(screen.getByRole('button', { name: 'Close assessment' }))
  expect(screen.getByRole('button', { name: 'Inspect P001 NCT00000001' })).toHaveFocus()
})

test.each(['/patients/P001', '/studies/NCT00000001'])('%s handles empty results, read failures and explicit retry', async path => {
  setup(path)
  let failed = true
  server.use(http.get('*/api/match/pairs', () => failed ? new HttpResponse(null, { status: 503 }) : HttpResponse.json([])))
  const user = userEvent.setup()
  render(<App />)
  expect(await screen.findByText('Unable to load assessments.')).toBeVisible()
  failed = false
  await user.click(screen.getByRole('button', { name: 'Retry assessments' }))
  expect(await screen.findByText('No assessments available yet.')).toBeVisible()
})

test.each(['/patients/P001', '/studies/NCT00000001'])('%s expires safely when assessment reads lose authentication', async path => {
  setup(path)
  server.use(http.get('*/api/match/pairs', () => HttpResponse.json({}, { status: 401 })))
  render(<App />)
  expect(await screen.findByRole('heading', { name: 'Log in' })).toBeVisible()
  expect(screen.queryByRole('heading', { name: 'Assessments' })).not.toBeInTheDocument()
})

test('Execution states never invent pair summaries and manual Refresh updates stored results', async () => {
  setup('/patients/P001')
  let completed = false
  server.use(http.get('*/api/match/pairs', () => HttpResponse.json(completed ? [pair] : [
    { ...pair, state: 'unfinished', pairSummary: null },
    { ...pair, nctId: 'NCT00000002', state: 'running', pairSummary: null },
    { ...pair, nctId: 'NCT00000003', state: 'failed', pairSummary: null, latestAttempt: { status: 'failed', failureCode: 'invalidResponse', failureExplanation: 'Controlled failure.' } },
  ])))
  const user = userEvent.setup()
  render(<App />)
  expect(await screen.findByText('Unfinished')).toBeVisible()
  expect(screen.getByText('Running')).toBeVisible()
  expect(screen.getByText(/Failed.*Controlled failure/)).toBeVisible()
  expect(screen.queryByText('Condition-label alignment undetermined')).not.toBeInTheDocument()
  completed = true
  await user.click(screen.getByRole('button', { name: 'Refresh assessments' }))
  expect(await screen.findByText('Condition-label alignment undetermined')).toBeVisible()
})

test('Refresh retains an open snapshot while updating its saved details', async () => {
  setup('/patients/P001')
  let updated = false
  server.use(http.get('*/api/match/pairs/P001/NCT00000001', () => HttpResponse.json({ ...detail,
    skipReason: updated ? 'Updated saved details.' : detail.skipReason })))
  const user = userEvent.setup()
  render(<App />)
  await user.click(await screen.findByRole('button', { name: 'Inspect P001 NCT00000001' }))
  const inspection = await screen.findByRole('region', { name: 'Assessment P001 NCT00000001' })
  await user.click(within(inspection).getByText('Exact input snapshot'))
  updated = true
  await user.click(screen.getByRole('button', { name: 'Refresh assessments' }))
  expect(await within(inspection).findByText('Updated saved details.')).toBeVisible()
  expect(within(inspection).getByRole('region', { name: 'Patient snapshot' })).toBeVisible()
})

test.each(['/patients/missing', '/studies/missing'])('Missing resource %s does not load assessment sections', async path => {
  setup(path)
  server.use(http.get(`*/api${path}`, () => HttpResponse.json({}, { status: 404 })))
  render(<App />)
  expect(await screen.findByRole('heading', { name: /not found/ })).toBeVisible()
  expect(screen.queryByRole('heading', { name: 'Assessments' })).not.toBeInTheDocument()
})

test('A failed detail read offers recovery without hiding the source page', async () => {
  setup('/studies/NCT00000001')
  let fail = true
  server.use(http.get('*/api/match/pairs/P001/NCT00000001', () => fail ? new HttpResponse(null, { status: 503 }) : HttpResponse.json(detail)))
  const user = userEvent.setup()
  render(<App />)
  await user.click(await screen.findByRole('button', { name: 'Inspect P001 NCT00000001' }))
  expect(await screen.findByRole('alert')).toHaveTextContent('Unable to load assessment details.')
  expect(screen.getByRole('heading', { name: 'Study NCT00000001' })).toBeVisible()
  await user.click(screen.getByRole('button', { name: 'Close assessment' }))
  fail = false
  await user.click(screen.getByRole('button', { name: 'Inspect P001 NCT00000001' }))
  expect(await screen.findByRole('region', { name: 'Assessment P001 NCT00000001' })).toBeVisible()
})
