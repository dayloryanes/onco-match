import { expect, test } from '@playwright/test'

for (const destination of ['/patients/P001', '/studies/NCT00000001']) {
  test(`Leaving the initiating Dashboard for ${destination} aborts its progress request`, async ({ page }) => {
    const pair = { batchId: 'navigation-test', patientId: 'P001', nctId: 'NCT00000001', state: 'unfinished',
      pairSummary: null, structuredChecks: null, latestAttempt: null }
    const batch = { batchId: 'navigation-test', version: 'v1', status: 'notStarted', totalPairCount: 1, completedPairCount: 0,
      failedPairCount: 0, unfinishedPairCount: 1, activePairCount: 0, activeOperation: null, availableActions: ['start'], pairs: [pair] }
    // Hold only the browser transport. Existing real-API cancellation tests
    // separately verify that a disconnected request stops backend work.
    await page.addInitScript(() => {
      const original = window.fetch
      window.fetch = async (...args) => {
        const url = String(args[0])
        if (url.endsWith('/api/match/batches/navigation-test/operations')) {
          const transport = window as Window & { navigationStreamStarted?: boolean; navigationStreamAborted?: boolean }
          return new Response(new ReadableStream<Uint8Array>({
            start(controller) {
              transport.navigationStreamStarted = true
              args[1]?.signal?.addEventListener('abort', () => {
                transport.navigationStreamAborted = true
                controller.error(new DOMException('Aborted', 'AbortError'))
              }, { once: true })
            },
          }), { headers: { 'Content-Type': 'application/x-ndjson' } })
        }
        return original(...args)
      }
    })
    await page.route('**/api/**', async route => {
      const path = new URL(route.request().url()).pathname
      const patient = { id: 'P001', ageYears: 42, sex: 'female', cancerType: 'Demo cancer',
        stage: { status: 'unknown', value: null, classificationSystem: null }, diseaseState: { status: 'unknown', value: null } }
      const study = { nctId: 'NCT00000001', title: 'Constructed study', recruitmentStatus: 'RECRUITING', studyType: 'INTERVENTIONAL',
        conditions: [], briefSummary: null, minimumAge: null, maximumAge: null, sex: 'ALL', registryUrl: 'https://clinicaltrials.gov/study/NCT00000001' }
      const body = path === '/api/auth/me' ? { username: 'operator', role: 'Operator' }
        : path === '/api/auth/csrf' ? { requestToken: 'navigation-token' }
          : path === '/api/study-imports' ? { version: 'v1', isImporting: false, isExhausted: true, nextAction: 'none', totalImportedStudyCount: 1, attempts: [] }
            : path === '/api/match/batches' ? { configurationError: null, activeOperation: null, batches: [batch] }
              : path === '/api/match/batches/navigation-test' ? batch
                : path === '/api/match/pairs' ? [pair]
                  : path === '/api/patients' ? [patient] : path === '/api/studies' ? [study]
                    : path === '/api/patients/P001' ? patient : study
      await route.fulfill({ json: body })
    })
    await page.goto('/')
    await page.getByRole('button', { name: 'Start match', exact: true }).click()
    await page.waitForFunction(() => (window as Window & { navigationStreamStarted?: boolean }).navigationStreamStarted)
    // A client-side link performs the real protected-route teardown.
    await page.getByRole('link', { name: destination.includes('patients') ? 'Patients' : 'Studies', exact: true }).click()
    await page.waitForFunction(() => (window as Window & { navigationStreamAborted?: boolean }).navigationStreamAborted)
    await page.getByRole('link', { name: destination.includes('patients') ? 'P001' : 'NCT00000001', exact: true }).click()
    await expect(page.getByRole('region', { name: 'Stored assessments', exact: true })).toBeVisible()
    await expect(page.getByRole('button', { name: /Start match|Continue unfinished|Retry failed|Cancel match/ })).toHaveCount(0)
  })
}
