import { test, expect } from '@playwright/test'
import type { Page } from '@playwright/test'

async function readingDuringRecovery(page: Page) {
  let reconciliationFails = false
  const checks = { age: { result: 'satisfied', explanation: 'Controlled age check.', patientEvidence: {}, studyEvidence: {} }, sex: { result: 'noRestrictionIdentified', explanation: 'All sexes.', patientEvidence: {}, studyEvidence: {} } }
  const completed = { batchId: 'reading', patientId: 'P010', nctId: 'NCT00000001', state: 'completed', pairSummary: 'conditionLabelAlignmentSupported', structuredChecks: checks, latestAttempt: { status: 'completed', failureCode: null, failureExplanation: null } }
  const batch = { batchId: 'reading', version: 'v1', status: 'unfinished', totalPairCount: 2, completedPairCount: 1, failedPairCount: 0, unfinishedPairCount: 1, activePairCount: 0, activeOperation: null, availableActions: ['continue'], pairs: [{ ...completed, patientId: 'P005', state: 'unfinished', pairSummary: null, latestAttempt: null }, completed] }
  // Replace only the browser HTTP boundary so the test controls real stream
  // delivery while the application runs its normal parser and reconciliation.
  await page.addInitScript(() => {
    const original = window.fetch
    const transport = window as Window & { progress?: ReadableStreamDefaultController<Uint8Array>; operationPosts?: number }
    window.fetch = async (...args) => {
      const url = args[0] instanceof Request ? args[0].url : String(args[0])
      if (url.endsWith('/api/match/batches/reading/operations') && args[1]?.method === 'POST') {
        transport.operationPosts = (transport.operationPosts ?? 0) + 1
        return new Response(new ReadableStream<Uint8Array>({ start(controller) { transport.progress = controller } }), { headers: { 'Content-Type': 'application/x-ndjson' } })
      }
      return original(...args)
    }
  })
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname
    if (reconciliationFails && path.startsWith('/api/match/batches')) { await route.fulfill({ status: 503, json: { code: 'unavailable' } }); return }
    const now = new Date()
    const body = path === '/api/auth/me' ? { username: 'operator', role: 'Operator', serverTime: now.toISOString(), expiresAt: new Date(+now + 1800000).toISOString() }
      : path === '/api/auth/csrf' ? { requestToken: 'reading-test' }
        : path === '/api/study-imports' ? { version: 'v1', isImporting: false, isExhausted: true, nextAction: 'none', totalImportedStudyCount: 1, attempts: [] }
          : path === '/api/match/batches' ? { configurationError: null, activeOperation: null, batches: [batch] }
            : path === '/api/match/batches/reading' ? batch
              : { ...completed, skipReason: null, conditionLabels: [], assessmentLimits: 'Limited assessment.', inputSnapshot: {
                patient: { ageYears: 42, sex: 'female', cancerType: 'Breast cancer', stage: { status: 'unknown', value: null, classificationSystem: null }, diseaseState: { status: 'unknown', value: null } },
                study: { minimumAge: '18 Years', maximumAge: '65 Years', sex: 'ALL', conditions: ['Breast cancer'] },
              }, attemptHistory: [{ id: 'completed-attempt', operationId: 'earlier-operation', startedAt: now.toISOString(), finishedAt: now.toISOString(), status: 'completed', failureCode: null, failureExplanation: null, route: 'native', model: 'test-model', requestTimeoutSeconds: 600, elapsedMilliseconds: 1000 }] }
    await route.fulfill({ json: body })
  })
  let sequence = 0
  async function emit(type: string, changes: Record<string, unknown> = {}) {
    const event = { type, batchId: 'reading', operationId: 'recovery', version: `v${sequence + 2}`, sequence: ++sequence, serverTime: '2026-11-09T12:00:00Z', status: 'running', totalPairCount: 2, completedPairCount: 1, failedPairCount: 0, unfinishedPairCount: 1, activePairCount: 0, patientId: null, nctId: null, pairSummary: null, structuredChecks: null, failureCode: null, failureExplanation: null, stopReason: null, ...changes }
    await page.evaluate(event => {
      const transport = window as Window & { progress?: ReadableStreamDefaultController<Uint8Array> }
      transport.progress!.enqueue(new TextEncoder().encode(JSON.stringify(event) + '\n'))
      if (event.type === 'operationStopped' || event.type === 'operationFinished') transport.progress!.close()
    }, event)
  }
  await page.goto('/')
  await page.getByRole('button', { name: 'Continue unfinished', exact: true }).click()
  await page.waitForFunction(() => !!(window as Window & { progress?: unknown }).progress)
  await emit('operationStarted')
  await page.getByRole('button', { name: 'Inspect P010 NCT00000001', exact: true }).click()
  const inspection = page.getByRole('region', { name: 'Assessment P010 NCT00000001', exact: true })
  await inspection.getByText('Exact input snapshot', { exact: true }).click()
  await inspection.getByText('Attempt history', { exact: true }).click()
  await inspection.evaluate(element => element.setAttribute('data-retained', 'true'))
  await page.addStyleTag({ content: 'html { overflow-anchor: none; }' })
  const heading = inspection.getByRole('heading', { name: 'Assessment: P010 · NCT00000001', exact: true })
  await heading.evaluate(element => window.scrollTo({ top: window.scrollY + element.getBoundingClientRect().top - 100, behavior: 'instant' }))
  return { inspection, heading, emit, checks, failReconciliation(value: boolean) { reconciliationFails = value } }
}

test('Streamed changes above a retained inspection preserve its reading position', async ({ page }) => {
  const fixture = await readingDuringRecovery(page)
  const before = (await fixture.heading.boundingBox())!.y
  const explanation = 'Controlled failure explanation that wraps across several lines. '.repeat(30)
  await fixture.emit('pairFailed', { patientId: 'P005', nctId: 'NCT00000001', structuredChecks: fixture.checks, failedPairCount: 1, unfinishedPairCount: 0, failureCode: 'invalidResponse', failureExplanation: explanation })
  await expect(page.getByText(explanation, { exact: false })).toBeAttached()
  await expect.poll(async () => (await fixture.heading.boundingBox())!.y).toBeCloseTo(before, 0)
  await expect(fixture.inspection).toHaveAttribute('data-retained', 'true')
  await fixture.emit('operationFinished', { status: 'finishedWithFailures', failedPairCount: 1, unfinishedPairCount: 0 })
})

test('Failed reconciliation retains open results and blocks recovery until Refresh confirms state', async ({ page }) => {
  const fixture = await readingDuringRecovery(page)
  const before = (await fixture.heading.boundingBox())!.y
  fixture.failReconciliation(true)
  await fixture.emit('operationStopped', { status: 'unfinished', stopReason: 'cancelled' })
  await expect(page.getByText('Unable to refresh saved results. Use Refresh before taking another action.', { exact: true })).toBeAttached()
  await expect(fixture.inspection).toHaveAttribute('data-retained', 'true')
  await expect(fixture.inspection.locator('details[open]')).toHaveCount(2)
  await expect(page.getByRole('button', { name: 'Continue unfinished', exact: true })).toHaveCount(0)
  await expect.poll(async () => (await fixture.heading.boundingBox())!.y).toBeCloseTo(before, 0)
  fixture.failReconciliation(false)
  await page.getByRole('button', { name: 'Refresh', exact: true }).evaluate((button: HTMLButtonElement) => button.click())
  await expect(page.getByRole('button', { name: 'Continue unfinished', exact: true })).toBeEnabled()
  await expect(fixture.inspection).toHaveAttribute('data-retained', 'true')
  await expect(fixture.inspection.locator('details[open]')).toHaveCount(2)
  expect(await page.evaluate(() => (window as Window & { operationPosts?: number }).operationPosts)).toBe(1)
})
