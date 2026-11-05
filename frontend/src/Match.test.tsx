import { act, fireEvent, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { expect, test, vi } from 'vitest'
import App from './App'
import { server } from './test/server'

const batch = { batchId: 'batch1', version: 'v1', status: 'notStarted', totalPairCount: 10, completedPairCount: 0,
  failedPairCount: 0, unfinishedPairCount: 10, activePairCount: 0, activeOperation: null, availableActions: ['start'], pairs: [] }

test('One Refresh updates imports and open batch rows without replacing an inspection', async () => {
  let refreshed = false
  const pair = { batchId: 'batch1', patientId: 'P01', nctId: 'NCT00000001', state: 'completed', pairSummary: 'conditionLabelAlignmentUndetermined', structuredChecks: null, latestAttempt: null }
  server.use(
    http.get('*/api/auth/me', () => HttpResponse.json({ username: 'viewer', role: 'Viewer' })),
    http.get('*/api/study-imports', () => HttpResponse.json({ version: 'v1', isImporting: false, isExhausted: false, nextAction: 'next', totalImportedStudyCount: refreshed ? 2 : 1, attempts: [] })),
    http.get('*/api/match/batches', () => HttpResponse.json({ configurationError: null, batches: [{ ...batch, completedPairCount: refreshed ? 4 : 1 }] })),
    http.get('*/api/match/batches/batch1', () => HttpResponse.json({ ...batch, completedPairCount: refreshed ? 4 : 1, pairs: [pair, { ...pair, patientId: 'P02', state: refreshed ? 'completed' : 'unfinished', pairSummary: null }] })),
    http.get('*/api/match/pairs/P01/NCT00000001', () => HttpResponse.json({ ...pair, inputSnapshot: null, conditionLabels: [], skipReason: 'Retained assessment.', assessmentLimits: 'Assessment limits.' })),
  )
  const user = userEvent.setup()
  render(<App />)
  await user.click(await screen.findByRole('button', { name: 'View batch 1' }))
  await user.click(await screen.findByRole('button', { name: 'Inspect P01 NCT00000001' }))
  const assessment = await screen.findByRole('region', { name: 'Assessment P01 NCT00000001' })
  expect(screen.queryByRole('button', { name: 'Refresh assessments' })).not.toBeInTheDocument()
  refreshed = true
  await user.click(await screen.findByRole('button', { name: 'Refresh' }))
  expect(await screen.findByText('2 studies')).toBeVisible()
  expect(await screen.findByText('4/10 completed')).toBeVisible()
  expect(await screen.findByRole('button', { name: 'Inspect P02 NCT00000001' })).toBeVisible()
  expect(screen.getByRole('button', { name: 'Close batch 1' })).toHaveAttribute('aria-expanded', 'true')
  expect(screen.getByRole('region', { name: 'Assessment P01 NCT00000001' })).toBe(assessment)
})

test('Import waits for a pending Refresh to finish', async () => {
  let reads = 0
  let posts = 0
  let release!: () => void
  let reached!: () => void
  const held = new Promise<void>(resolve => { release = resolve })
  const requested = new Promise<void>(resolve => { reached = resolve })
  server.use(
    http.get('*/api/auth/me', () => HttpResponse.json({ username: 'operator', role: 'Operator' })),
    http.get('*/api/study-imports', async () => {
      if (++reads > 1) { reached(); await held }
      return HttpResponse.json({ version: `v${reads}`, isImporting: false, isExhausted: false, nextAction: 'next', totalImportedStudyCount: reads, attempts: [] })
    }),
    http.post('*/api/study-imports', () => { posts++; return HttpResponse.json({}) }),
  )
  const user = userEvent.setup()
  render(<App />)
  await user.click(await screen.findByRole('button', { name: 'Refresh' }))
  await requested
  try {
    expect(screen.getByRole('button', { name: 'Load Next 10' })).toBeDisabled()
    await user.click(screen.getByRole('button', { name: 'Load Next 10' }))
    expect(posts).toBe(0)
  } finally { await act(async () => release()) }
  expect(await screen.findByText('2 studies')).toBeVisible()
  expect(screen.getByRole('button', { name: 'Load Next 10' })).toBeEnabled()
})

test.each([['ALL', 'All'], ['MALE', 'Male'], ['FEMALE', 'Female']])('Batch notice appears once and the snapshot separates Patient and Study facts for %s', async (registrySex, sexLabel) => {
  const notice = "This assessment checks structured age and sex requirements and compares cancer type, stage, and disease state with the study's listed conditions. Biomarkers, treatment history, other requirements, and free-text eligibility criteria are not assessed. Results do not establish clinical eligibility or enrollment approval."
  const pair = { batchId: 'batch1', patientId: 'P01', nctId: 'NCT00000001', state: 'completed', pairSummary: 'conditionLabelAlignmentUndetermined', structuredChecks: null, latestAttempt: null }
  server.use(
    http.get('*/api/auth/me', () => HttpResponse.json({ username: 'viewer', role: 'Viewer' })),
    http.get('*/api/match/batches', () => HttpResponse.json({ configurationError: null, batches: [batch] })),
    http.get('*/api/match/batches/batch1', () => HttpResponse.json({ ...batch, pairs: [pair] })),
    http.get('*/api/match/pairs/P01/NCT00000001', () => HttpResponse.json({ ...pair, conditionLabels: [], skipReason: 'No conditions supplied.', assessmentLimits: notice,
      inputSnapshot: { patient: { ageYears: 42, sex: 'female', cancerType: 'Breast cancer', stage: { status: 'known', value: 'II', classificationSystem: 'Demo staging system' }, diseaseState: { status: 'known', value: 'recurrent' } },
        study: { minimumAge: '18 Years', maximumAge: '65 Years', sex: registrySex, conditions: ['Breast cancer'] } } })),
  )
  const user = userEvent.setup()
  render(<App />)
  await user.click(await screen.findByRole('button', { name: 'View batch 1' }))
  await user.click(await screen.findByRole('button', { name: 'Inspect P01 NCT00000001' }))
  const assessment = await screen.findByRole('region', { name: 'Assessment P01 NCT00000001' })
  expect(screen.getAllByText(notice)).toHaveLength(1)
  expect(within(assessment).queryByText(notice)).not.toBeInTheDocument()
  await user.click(within(assessment).getByText('Exact input snapshot'))
  const patient = within(assessment).getByRole('region', { name: 'Patient snapshot' })
  const study = within(assessment).getByRole('region', { name: 'Study snapshot' })
  expect(within(patient).getByText('42')).toBeVisible()
  expect(within(patient).getByText('Female')).toBeVisible()
  expect(within(patient).getByText('Recurrent')).toBeVisible()
  expect(within(patient).getByText('II')).toBeVisible()
  expect(within(patient).getByText('Classification system')).toBeVisible()
  expect(within(patient).getByText('Demo staging system')).toBeVisible()
  expect(within(patient).queryByText('18 Years — 65 Years')).not.toBeInTheDocument()
  expect(within(study).getByText('18 Years — 65 Years')).toBeVisible()
  expect(within(study).getByText(sexLabel)).toBeVisible()
  expect(within(study).queryByText('Recurrent')).not.toBeInTheDocument()
  expect(within(study).getByRole('list').tagName).toBe('UL')
  expect(within(study).getByRole('listitem')).toHaveTextContent('Breast cancer')
})

test.each([
  { status: 'unknown', value: null, system: null, disease: null, expectedStage: 'Unknown', expectedSystem: 'Unknown', expectedDisease: 'Unknown', conditions: null },
  { status: 'notApplicable', value: null, system: null, disease: null, expectedStage: 'Not applicable', expectedSystem: 'Not applicable', expectedDisease: 'Not applicable', conditions: [] },
  { status: 'known', value: 'recurrent', system: 'Custom SYSTEM', disease: 'newlyDiagnosed', expectedStage: 'recurrent', expectedSystem: 'Custom SYSTEM', expectedDisease: 'Newly diagnosed', conditions: [' '] },
  { status: 'known', value: 'II', system: null, disease: 'refractory', expectedStage: 'II', expectedSystem: 'System unknown', expectedDisease: 'Refractory', conditions: [] },
])('Patient details and snapshot agree for $status stage $value and absent conditions', async sample => {
  const patient = { id: 'P01', ageYears: 42, sex: 'male', cancerType: 'unknown',
    stage: { status: sample.status, value: sample.value, classificationSystem: sample.system },
    diseaseState: { status: sample.status, value: sample.disease } }
  const pair = { batchId: 'batch1', patientId: 'P01', nctId: 'NCT00000001', state: 'completed', pairSummary: 'conditionLabelAlignmentUndetermined', structuredChecks: null, latestAttempt: null }
  server.use(
    http.get('*/api/auth/me', () => HttpResponse.json({ username: 'viewer', role: 'Viewer' })),
    http.get('*/api/match/batches', () => HttpResponse.json({ configurationError: null, batches: [batch] })),
    http.get('*/api/match/batches/batch1', () => HttpResponse.json({ ...batch, pairs: [pair] })),
    http.get('*/api/match/pairs/P01/NCT00000001', () => HttpResponse.json({ ...pair, conditionLabels: [], skipReason: null, assessmentLimits: '',
      inputSnapshot: { patient, study: { minimumAge: null, maximumAge: null, sex: null, conditions: sample.conditions } } })),
    http.get('*/api/patients', () => HttpResponse.json([patient])),
    http.get('*/api/patients/P01', () => HttpResponse.json(patient)),
  )
  const user = userEvent.setup()
  render(<App />)
  await user.click(await screen.findByRole('button', { name: 'View batch 1' }))
  await user.click(await screen.findByRole('button', { name: 'Inspect P01 NCT00000001' }))
  await user.click(await screen.findByText('Exact input snapshot'))
  const snapshot = screen.getByRole('region', { name: 'Patient snapshot' })
  const facts = (root: HTMLElement) => Object.fromEntries([...root.querySelectorAll('dl > div')].map(row => [row.querySelector('dt')?.textContent, row.querySelector('dd')?.textContent]))
  const expected = { Sex: 'Male', 'Cancer type': 'unknown', 'Stage at diagnosis': sample.expectedStage, 'Classification system': sample.expectedSystem, 'Disease state': sample.expectedDisease }
  expect(facts(snapshot)).toMatchObject(expected)
  expect(within(screen.getByRole('region', { name: 'Study snapshot' })).getByText('Not available')).toBeVisible()
  await user.click(screen.getByRole('link', { name: 'Patients' }))
  await user.click(await screen.findByRole('link', { name: 'P01' }))
  await screen.findByRole('heading', { name: 'Patient P01' })
  expect(facts(screen.getByRole('main'))).toMatchObject(expected)
})

test('Inspection retains each condition summary, explanation and evidence beneath its supplied text', async () => {
  const pair = { batchId: 'batch1', patientId: 'P01', nctId: 'NCT00000001', state: 'completed', pairSummary: 'mixedConditionLabelFindings', latestAttempt: null }
  const finding = { result: 'undetermined', explanation: 'Finding explanation.', patientEvidence: [{ sourceId: 'patient.cancerType', value: 'Cancer TEXT' }], studyEvidence: [{ sourceId: 'study.conditions.1', quote: 'Second SOURCE condition' }], examinedSources: ['study.conditions.1'] }
  const firstFinding = { ...finding, studyEvidence: [{ sourceId: 'study.conditions.0', quote: 'First SOURCE condition' }], examinedSources: ['study.conditions.0'] }
  const condition = { conditionLabelId: 'study.conditions.1', result: 'undetermined', requirementSummary: 'Generated requirement summary.', explanation: 'Condition explanation.', cancerType: finding, stage: finding, diseaseState: finding, unassessedQualifiers: [{ qualifier: 'Biomarker qualifier', reason: 'Outside supported scope.', studyEvidence: finding.studyEvidence }] }
  server.use(
    http.get('*/api/auth/me', () => HttpResponse.json({ username: 'viewer', role: 'Viewer' })),
    http.get('*/api/match/batches', () => HttpResponse.json({ configurationError: null, batches: [batch] })),
    http.get('*/api/match/batches/batch1', () => HttpResponse.json({ ...batch, pairs: [pair] })),
    http.get('*/api/match/pairs/P01/NCT00000001', () => HttpResponse.json({ ...pair, skipReason: 'Retained skip explanation.', assessmentLimits: '',
      structuredChecks: { age: { result: 'satisfied', explanation: 'Age explanation.' }, sex: { result: 'noRestrictionIdentified', explanation: 'Sex explanation.' } },
      conditionLabels: [condition, { ...condition, conditionLabelId: 'study.conditions.0', requirementSummary: 'First requirement summary.', explanation: 'First explanation.', cancerType: firstFinding, stage: firstFinding, diseaseState: firstFinding, unassessedQualifiers: [] }],
      inputSnapshot: { patient: { ageYears: 42, sex: 'female', cancerType: 'Cancer TEXT', stage: { status: 'unknown', value: null, classificationSystem: null }, diseaseState: { status: 'unknown', value: null } }, study: { minimumAge: null, maximumAge: null, sex: null, conditions: ['First SOURCE condition', 'Second SOURCE condition'] } } })),
  )
  const user = userEvent.setup()
  render(<App />)
  await user.click(await screen.findByRole('button', { name: 'View batch 1' }))
  await user.click(await screen.findByRole('button', { name: 'Inspect P01 NCT00000001' }))
  const assessment = await screen.findByRole('region', { name: 'Assessment P01 NCT00000001' })
  expect(within(assessment).getByText('Mixed condition-label findings')).toBeVisible()
  expect(within(assessment).getByText('Retained skip explanation.')).toBeVisible()
  expect(within(assessment).getByRole('heading', { name: 'Age check: Satisfied' })).toBeVisible()
  expect(within(assessment).getByText('Age explanation.')).toBeVisible()
  expect(within(assessment).getByRole('heading', { name: 'Sex check: No restriction identified' })).toBeVisible()
  expect(within(assessment).getByText('Sex explanation.')).toBeVisible()
  const heading = within(assessment).getByRole('heading', { name: 'Condition 2 · Second SOURCE condition' })
  const second = within(heading.closest('article')!)
  expect(second.getByText('Generated requirement summary.')).toBeVisible()
  expect(second.getByText('Condition explanation.')).toBeVisible()
  expect(second.getAllByText('Finding explanation.')).toHaveLength(3)
  expect(second.getAllByText('Patient cancer type:')).toHaveLength(3)
  expect(second.getAllByText('Second SOURCE condition', { exact: true })).toHaveLength(4)
  expect(second.getAllByText('Examined: Study condition 2')).toHaveLength(1)
  expect(heading.closest('article')!.lastElementChild).toHaveTextContent('Examined: Study condition 2')
  expect(second.getByText('Outside supported scope.', { exact: false })).toBeVisible()
  const firstHeading = within(assessment).getByRole('heading', { name: 'Condition 1 · First SOURCE condition' })
  const first = within(firstHeading.closest('article')!)
  expect(first.getByText('First requirement summary.')).toBeVisible()
  expect(first.getByText('First explanation.')).toBeVisible()
  expect(first.getAllByText('Examined: Study condition 1')).toHaveLength(1)
  expect(firstHeading.closest('article')!.lastElementChild).toHaveTextContent('Examined: Study condition 1')
  expect(first.queryByText('Examined: Study condition 2')).not.toBeInTheDocument()
  expect(first.getByText('None stated.')).toBeVisible()
  expect(within(assessment).getByText('Exact input snapshot')).toBeVisible()
  expect(within(assessment).queryByText(/study\.conditions\./)).not.toBeInTheDocument()
})

test('Each import contains its own batch summary and expandable results', async () => {
  server.use(
    http.get('*/api/auth/me', () => HttpResponse.json({ username: 'viewer', role: 'Viewer' })),
    http.get('*/api/study-imports', () => HttpResponse.json({ version: 'v1', isImporting: false, isExhausted: true, nextAction: 'none', totalImportedStudyCount: 1,
      attempts: [{ id: 'batch1', status: 'completed', startedAt: '2026-11-03T12:00:00Z', finishedAt: null, retryOfAttemptId: null,
        fetchedCount: 1, importedCount: 1, duplicateCount: 0, rejectedCount: 0, importedNctIds: ['NCT00000001'], duplicateNctIds: [], rejections: [], failureCode: null }] })),
    http.get('*/api/match/batches', () => HttpResponse.json({ configurationError: null, batches: [batch] })),
    http.get('*/api/match/batches/batch1', () => HttpResponse.json(batch)),
  )
  const user = userEvent.setup()
  render(<App />)
  const card = await screen.findByRole('listitem', { name: 'Completed import' })
  expect(await within(card).findByText('0/10 completed')).toBeVisible()
  const toggle = within(card).getByRole('button', { name: 'View batch 1' })
  expect(toggle).toHaveAttribute('aria-expanded', 'false')
  await user.click(toggle)
  expect(await within(card).findByRole('region', { name: 'Batch assessments' })).toBeVisible()
  await user.click(within(card).getByRole('button', { name: 'Close batch 1' }))
  expect(within(card).queryByRole('region', { name: 'Batch assessments' })).not.toBeInTheDocument()
  expect(within(card).getByRole('button', { name: 'View batch 1' })).toHaveAttribute('aria-expanded', 'false')
})

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
  expect(await screen.findByRole('heading', { name: 'Batch 1 · Unfinished' })).toBeVisible()
  expect(posts).toBe(1)
  expect(screen.queryByRole('button', { name: 'Start match' })).not.toBeInTheDocument()
})

test.each([
  ['cancelled', 'Match cancelled. Saved results are retained.', 'status'],
  ['sessionExpired', 'Match stopped because your session expired. Saved results are retained.', 'alert'],
  ['logout', 'Match stopped because you logged out. Saved results are retained.', 'alert'],
  ['disconnected', 'Match stopped because the connection was lost. Saved results are retained.', 'alert'],
  ['inferenceUnavailable', 'Match stopped because the local inference service or model is unavailable. Check the configured model and route. Saved results are retained.', 'alert'],
  ['internalError', 'Match stopped because of an unexpected error. Saved results are retained.', 'alert'],
])('Stopped match %s has a notice above its own card even when collapsed', async (reason, message, role) => {
  let stopped = false
  const other = { ...batch, batchId: 'batch2', availableActions: [] }
  const finished = () => ({ ...batch, status: stopped ? 'unfinished' : 'notStarted', availableActions: stopped ? [] : ['start'] })
  const event = (type: string, sequence: number) => JSON.stringify({ ...batch, type, sequence, operationId: 'op1', serverTime: '2026-11-03T12:00:00Z', patientId: null, nctId: null, status: sequence === 1 ? 'running' : 'unfinished', stopReason: sequence === 1 ? null : reason }) + '\n'
  server.use(
    http.get('*/api/auth/me', () => HttpResponse.json({ username: 'operator', role: 'Operator' })),
    http.get('*/api/match/batches', () => HttpResponse.json({ configurationError: null, batches: [other, finished()] })),
    http.get('*/api/match/batches/batch1', () => HttpResponse.json(finished())),
    http.post('*/api/match/batches/batch1/operations', () => { stopped = true; return new HttpResponse(event('operationStarted', 1) + event('operationStopped', 2), { headers: { 'Content-Type': 'application/x-ndjson' } }) }),
  )
  const user = userEvent.setup()
  render(<App />)
  await user.click(await screen.findByRole('button', { name: 'Start match' }))
  const notice = await screen.findByText(message)
  expect(notice).toHaveAttribute('role', role)
  const item = notice.closest('li')!
  expect(within(item).getByRole('heading', { name: 'Batch 2 · Unfinished' })).toBeVisible()
  expect(notice.nextElementSibling).toContainElement(within(item).getByRole('heading', { name: 'Batch 2 · Unfinished' }))
  await user.click(within(item).getByRole('button', { name: 'Close batch 2' }))
  expect(notice).toBeVisible()
  expect(within(item).queryByRole('region', { name: 'Batch assessments' })).not.toBeInTheDocument()
  expect(screen.getAllByText(message)).toHaveLength(1)
})

test('Accepted cancellation keeps the existing waiting and stopped sequence with batch-local status notices', async () => {
  let output!: ReadableStreamDefaultController<Uint8Array>
  let stopped = false
  const encoder = new TextEncoder()
  const event = (type: string, sequence: number) => JSON.stringify({ ...batch, type, sequence, operationId: 'op1', serverTime: '2026-11-03T12:00:00Z', patientId: null, nctId: null, status: sequence === 1 ? 'running' : 'unfinished', stopReason: sequence === 1 ? null : 'cancelled' }) + '\n'
  server.use(
    http.get('*/api/auth/me', () => HttpResponse.json({ username: 'operator', role: 'Operator' })),
    http.get('*/api/match/batches', () => HttpResponse.json({ configurationError: null, batches: [{ ...batch, status: stopped ? 'unfinished' : 'notStarted', availableActions: stopped ? [] : ['start'] }] })),
    http.get('*/api/match/batches/batch1', () => HttpResponse.json({ ...batch, status: stopped ? 'unfinished' : 'notStarted' })),
    http.post('*/api/match/batches/batch1/operations', () => new HttpResponse(new ReadableStream<Uint8Array>({ start(controller) { output = controller; controller.enqueue(encoder.encode(event('operationStarted', 1))) } }), { headers: { 'Content-Type': 'application/x-ndjson' } })),
    http.post('*/api/match/operations/op1/cancel', () => new HttpResponse(null, { status: 202 })),
  )
  const user = userEvent.setup()
  render(<App />)
  await user.click(await screen.findByRole('button', { name: 'Start match' }))
  await user.click(await screen.findByRole('button', { name: 'Cancel match' }))
  const waiting = await screen.findByText('Cancellation requested. Waiting for active work to stop.')
  expect(waiting).toHaveAttribute('role', 'status')
  const item = waiting.closest('li')!
  stopped = true
  await act(async () => { output.enqueue(encoder.encode(event('operationStopped', 2))); output.close() })
  expect(await within(item).findByText('Match cancelled. Saved results are retained.')).toHaveAttribute('role', 'status')
  expect(screen.queryByText('Cancellation requested. Waiting for active work to stop.')).not.toBeInTheDocument()
})

test('A rejected start uses readable batch-local wording without claiming saved state was refreshed', async () => {
  let posts = 0
  server.use(
    http.get('*/api/auth/me', () => HttpResponse.json({ username: 'operator', role: 'Operator' })),
    http.get('*/api/match/batches', () => HttpResponse.json({ configurationError: null, batches: [batch] })),
    http.get('*/api/match/batches/batch1', () => HttpResponse.json(batch)),
    http.post('*/api/match/batches/batch1/operations', () => { posts++; return HttpResponse.json({ code: 'batch_state_changed', title: 'batch_state_changed' }, { status: 409 }) }),
  )
  const user = userEvent.setup()
  render(<App />)
  await user.click(await screen.findByRole('button', { name: 'Start match' }))
  const notice = await screen.findByText('This batch changed in another session. Review its refreshed status before continuing.')
  expect(notice).toHaveAttribute('role', 'alert')
  expect(notice.closest('li')).not.toBeNull()
  expect(screen.queryByText(/Saved state has been refreshed/)).not.toBeInTheDocument()
  expect(posts).toBe(1)
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

test('Opening a batch refreshes the summary and cancellation controls after another shared tab starts it', async () => {
  server.use(
    http.get('*/api/auth/me', () => HttpResponse.json({ username: 'operator', role: 'Operator' })),
    http.get('*/api/match/batches', () => HttpResponse.json({ configurationError: null, batches: [batch] })),
    http.get('*/api/match/batches/batch1', () => HttpResponse.json({ ...batch, status: 'running', availableActions: [], completedPairCount: 3, unfinishedPairCount: 6, activePairCount: 1,
      activeOperation: { operationId: 'other-tab-operation', action: 'start', startedAt: '2026-11-03T12:00:00Z', selectedPairCount: 10, canCancel: true } })),
  )
  const user = userEvent.setup()
  render(<App />)
  await user.click(await screen.findByRole('button', { name: 'View batch 1' }))
  expect(await screen.findByRole('button', { name: 'Cancel match' })).toBeVisible()
  expect(screen.getByText('3/10 completed')).toBeVisible()
  expect(screen.queryByRole('button', { name: 'Start match' })).not.toBeInTheDocument()
  await user.click(screen.getByRole('button', { name: 'Close batch 1' }))
  expect(screen.getByRole('button', { name: 'Cancel match' })).toBeVisible()
})

test('Closing a loading inspection prevents its late response from replacing another row', async () => {
  let release!: () => void
  let reached!: () => void
  let finishRequest!: () => void
  let finishHandler!: () => void
  const waiting = new Promise<void>(resolve => { release = resolve })
  const requested = new Promise<void>(resolve => { reached = resolve })
  const requestFinished = new Promise<void>(resolve => { finishRequest = resolve })
  const handlerFinished = new Promise<void>(resolve => { finishHandler = resolve })
  const originalFetch = globalThis.fetch
  const fetchObserver = vi.spyOn(globalThis, 'fetch').mockImplementation((...args) => {
    const result = originalFetch(...args)
    return String(args[0]).endsWith('/api/match/pairs/P01/NCT00000001') ? result.finally(finishRequest) : result
  })
  const pair = { batchId: 'batch1', patientId: 'P01', nctId: 'NCT00000001', state: 'completed', pairSummary: 'conditionLabelAlignmentUndetermined', structuredChecks: null, latestAttempt: null }
  const details = { ...pair, inputSnapshot: null, conditionLabels: [], skipReason: 'No conditions supplied.', assessmentLimits: 'Assessment limits.' }
  server.use(
    http.get('*/api/auth/me', () => HttpResponse.json({ username: 'viewer', role: 'Viewer' })),
    http.get('*/api/match/batches', () => HttpResponse.json({ configurationError: null, batches: [batch] })),
    http.get('*/api/match/batches/batch1', () => HttpResponse.json({ ...batch, pairs: [pair, { ...pair, patientId: 'P02' }] })),
    http.get('*/api/match/pairs/P01/NCT00000001', async () => { reached(); await waiting; const response = HttpResponse.json(details); finishHandler(); return response }),
    http.get('*/api/match/pairs/P02/NCT00000001', () => HttpResponse.json({ ...details, patientId: 'P02' })),
  )
  try {
    const user = userEvent.setup()
    render(<App />)
    await user.click(await screen.findByRole('button', { name: 'View batch 1' }))
    await user.click(await screen.findByRole('button', { name: 'Inspect P01 NCT00000001' }))
    await requested
    expect(screen.getByText('Loading assessment…')).toBeVisible()
    await user.click(screen.getByRole('button', { name: 'Close details P01 NCT00000001' }))
    await requestFinished
    await user.click(screen.getByRole('button', { name: 'Inspect P02 NCT00000001' }))
    expect(await screen.findByRole('region', { name: 'Assessment P02 NCT00000001' })).toBeVisible()
    await act(async () => { release(); await handlerFinished })
    expect(screen.queryByRole('region', { name: 'Assessment P01 NCT00000001' })).not.toBeInTheDocument()
    expect(screen.getByRole('region', { name: 'Assessment P02 NCT00000001' })).toBeVisible()
  } finally { release(); fetchObserver.mockRestore() }
})

test('Live progress survives a successful focus check and assessment inspection stays on Dashboard', async () => {
  let checks = 0
  let batchReads = 0
  let releaseRefresh!: () => void
  let refreshReached!: () => void
  let refreshHandlerFinished!: () => void
  const heldRefresh = new Promise<void>(resolve => { releaseRefresh = resolve })
  const refreshRequested = new Promise<void>(resolve => { refreshReached = resolve })
  const refreshFinished = new Promise<void>(resolve => { refreshHandlerFinished = resolve })
  let output: ReadableStreamDefaultController<Uint8Array> | undefined
  let cancelled = false
  const pair = { batchId: 'batch1', patientId: 'P01', nctId: 'NCT00000001', state: 'completed', pairSummary: 'conditionLabelAlignmentUndetermined', structuredChecks: null, latestAttempt: null }
  const full = { ...batch, pairs: [pair] }
  const encoder = new TextEncoder()
  const event = (type: string, sequence: number, completed = 1) => JSON.stringify({ type, sequence, batchId: 'batch1', operationId: 'op1', version: `v${sequence}`, serverTime: '2026-11-03T12:00:00Z', status: 'running', totalPairCount: 10, completedPairCount: completed, failedPairCount: 0, unfinishedPairCount: 9 - completed, activePairCount: 1, patientId: null, nctId: null }) + '\n'
  server.use(
    http.get('*/api/auth/me', () => HttpResponse.json({ username: ++checks === 1 ? 'operator' : 'operator-rechecked', role: 'Operator' })),
    http.get('*/api/match/batches', async () => {
      if (++batchReads === 3) {
        refreshReached(); await heldRefresh
        refreshHandlerFinished()
        return HttpResponse.json({ configurationError: null, batches: [{ ...batch, status: 'running', availableActions: [], activeOperation: { operationId: 'op1', canCancel: true } }] })
      }
      return HttpResponse.json({ configurationError: null, batches: batchReads > 1 ? [batch, { ...batch, batchId: 'batch2', status: 'noWork', availableActions: [] }] : [batch] })
    }),
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
  await user.click(screen.getByRole('button', { name: 'Close batch 1' }))
  expect(screen.queryByRole('region', { name: 'Batch assessments' })).not.toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Cancel match' })).toBeVisible()
  expect(cancelled).toBe(false)
  await act(async () => { output!.enqueue(encoder.encode(event('heartbeat', 2, 3))) })
  expect(await screen.findByText('3/10 completed')).toBeVisible()
  await user.click(screen.getByRole('button', { name: 'View batch 1' }))
  fireEvent.focus(window)
  expect(await screen.findByText(/operator-rechecked/)).toBeVisible()
  expect(await screen.findByRole('heading', { name: 'Dashboard' })).toBeVisible()
  expect(cancelled).toBe(false)
  await user.click(screen.getByRole('button', { name: 'Inspect P01 NCT00000001' }))
  const assessment = await screen.findByRole('region', { name: 'Assessment P01 NCT00000001' })
  expect(assessment).toBeVisible()
  await user.click(screen.getByRole('button', { name: 'Refresh' }))
  expect(await screen.findByRole('heading', { name: 'Batch 2 · No work' })).toBeVisible()
  expect(screen.getByText('3/10 completed')).toBeVisible()
  expect(screen.getByRole('button', { name: 'Cancel match' })).toBeVisible()
  expect(screen.getByRole('region', { name: 'Assessment P01 NCT00000001' })).toBe(assessment)
  expect(cancelled).toBe(false)
  expect(within(assessment).queryByRole('button', { name: 'Close assessment' })).not.toBeInTheDocument()
  const inspectionToggle = screen.getByRole('button', { name: 'Close details P01 NCT00000001' })
  expect(assessment.closest('tr')?.previousElementSibling).toBe(inspectionToggle.closest('tr'))
  expect(inspectionToggle).toHaveAttribute('aria-expanded', 'true')
  await user.click(screen.getByRole('button', { name: 'Close assessment' }))
  expect(screen.queryByRole('region', { name: 'Assessment P01 NCT00000001' })).not.toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Inspect P01 NCT00000001' })).toHaveFocus()
  await user.click(screen.getByRole('button', { name: 'Inspect P01 NCT00000001' }))
  expect(await screen.findByRole('region', { name: 'Assessment P01 NCT00000001' })).toBeVisible()
  await user.click(screen.getByRole('button', { name: 'Close details P01 NCT00000001' }))
  expect(screen.queryByRole('region', { name: 'Assessment P01 NCT00000001' })).not.toBeInTheDocument()
  expect(window.location.pathname).toBe('/')
  expect(cancelled).toBe(false)
  await user.click(screen.getByRole('button', { name: 'Refresh' }))
  await refreshRequested
  await act(async () => { output!.enqueue(encoder.encode(event('operationFinished', 3, 3))); output!.close() })
  expect(await screen.findByRole('button', { name: 'Start match' })).toBeEnabled()
  await act(async () => { releaseRefresh(); await refreshFinished })
  expect(screen.getByRole('button', { name: 'Start match' })).toBeEnabled()
  expect(screen.queryByRole('button', { name: 'Cancel match' })).not.toBeInTheDocument()
})
