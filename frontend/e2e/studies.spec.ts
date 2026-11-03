import { test, expect } from '@playwright/test'
import type { Page } from '@playwright/test'

async function login(page: Page, username: string, password: string) {
  await page.getByLabel('Username').fill(username)
  await page.getByLabel('Password', { exact: true }).fill(password)
  await page.getByRole('button', { name: 'Log in', exact: true }).click()
}

// A single workflow owns mutable import state. Patient regressions may run in
// parallel because they neither import nor depend on study/history contents.
test('Operator imports mixed records and retries a failure while Viewer browses shared studies', async ({ page, browser }) => {
  test.setTimeout(90000)
  await page.goto('/')
  await login(page, 'operator', 'OperatorDemo1!')
  await page.getByRole('button', { name: 'Import first 10', exact: true }).click()
  await expect(page.getByText('Fetched: 3 · Imported: 1 · Duplicates: 1 · Rejected: 1')).toBeVisible()
  await expect(page.getByText('Record 3: Record must be an object.')).toBeVisible()
  // The real API and Vite proxy must deliver headers/progress before held fake
  // inference completes. The first answer waits 16 seconds to exercise heartbeat.
  await page.evaluate(() => {
    const original = window.fetch
    const evidence = window as Window & { matchEvidence?: Promise<string> }
    window.fetch = async (...args) => {
      const response = await original(...args)
      if (response.headers.get('Content-Type')?.includes('application/x-ndjson')) evidence.matchEvidence = response.clone().text()
      return response
    }
  })
  const progress = page.waitForResponse(response => response.url().includes('/operations') && response.request().method() === 'POST')
  await page.getByRole('button', { name: 'Start match', exact: true }).click()
  const streaming = await progress
  expect(streaming.headers()['content-type']).toContain('application/x-ndjson')
  await expect(page.getByRole('button', { name: 'Cancel match', exact: true })).toBeVisible()
  await expect(page.getByText('4/10 completed', { exact: true })).toBeVisible()
  // Focus checks preserve Dashboard and the initiating stream.
  await page.evaluate(() => window.dispatchEvent(new Event('focus')))
  await expect(page.getByRole('button', { name: 'Cancel match', exact: true })).toBeVisible()
  await expect(page.getByText('10/10 completed', { exact: true })).toBeVisible({ timeout: 25000 })
  const streamEvidence = await page.evaluate(() => (window as Window & { matchEvidence?: Promise<string> }).matchEvidence)
  expect(streamEvidence).toContain('heartbeat')
  expect(streamEvidence).toContain('operationFinished')
  await page.getByRole('button', { name: 'Inspect P005 NCT00000001', exact: true }).click()
  await expect(page.getByRole('region', { name: 'Assessment P005 NCT00000001', exact: true })).toBeVisible()
  await expect(page).toHaveURL('https://localhost:5173/')
  await page.screenshot({ path: 'test-results/dashboard-assessment.png', fullPage: true })
  await page.screenshot({ path: 'test-results/operator-import-history.png', fullPage: true })
  await page.getByRole('link', { name: 'Studies', exact: true }).click()
  await page.getByRole('link', { name: 'NCT00000001', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Study NCT00000001' })).toBeVisible()
  await expect(page.getByText('Controlled cancer study', { exact: true })).toBeVisible()
  await expect(page.getByRole('link', { name: 'View on ClinicalTrials.gov' })).toHaveAttribute('href', 'https://clinicaltrials.gov/study/NCT00000001')
  await page.reload()
  await expect(page.getByRole('heading', { name: 'Study NCT00000001' })).toBeVisible()
  await page.setViewportSize({ width: 390, height: 844 })
  await page.screenshot({ path: 'test-results/study-mobile.png', fullPage: true })
  await page.setViewportSize({ width: 1280, height: 720 })

  const viewerContext = await browser.newContext({ ignoreHTTPSErrors: true })
  const viewer = await viewerContext.newPage()
  await viewer.goto('https://localhost:5173/studies/NCT00000001')
  await login(viewer, 'viewer', 'ViewerDemo1!')
  await expect(viewer.getByRole('heading', { name: 'Study NCT00000001' })).toBeVisible()
  await viewer.getByRole('link', { name: 'Dashboard', exact: true }).click()
  await expect(viewer.getByText('Fetched: 3 · Imported: 1 · Duplicates: 1 · Rejected: 1')).toBeVisible()
  await expect(viewer.getByRole('button', { name: /Import first|Load Next|Retry import/ })).toHaveCount(0)
  await expect(viewer.getByRole('button', { name: 'Start match', exact: true })).toHaveCount(0)
  await viewer.getByRole('button', { name: 'View batch 1', exact: true }).click()
  await viewer.getByRole('button', { name: 'Inspect P005 NCT00000001', exact: true }).click()
  await expect(viewer.getByRole('region', { name: 'Assessment P005 NCT00000001', exact: true })).toBeVisible()

  await page.getByRole('link', { name: 'Dashboard', exact: true }).click()
  await page.getByRole('button', { name: 'Load Next 10', exact: true }).click()
  await expect(page.getByRole('alert')).toContainText('registry is unavailable')
  await expect(page.getByText('Failed import', { exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Retry import', exact: true }).click()
  await expect(page.getByText('No more registry pages.')).toBeVisible()
  await expect(page.getByRole('button', { name: 'Load Next 10', exact: true })).toBeDisabled()
  await expect(page.getByText('Completed import', { exact: true })).toHaveCount(2)
  await page.request.get('http://127.0.0.1:7133/test/inference?mode=hold')
  await page.getByRole('button', { name: 'Start match', exact: true }).click()
  await expect(page.getByRole('button', { name: 'Cancel match', exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Cancel match', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Batch assessments · Unfinished', exact: true })).toBeVisible()
  await expect(page.getByRole('button', { name: /Continue unfinished|Retry failed/ })).toHaveCount(0)
  await viewer.getByRole('button', { name: 'Refresh', exact: true }).click()
  await expect(viewer.getByText('2 imported studies · Shared until server restart', { exact: true })).toBeVisible()
  await viewer.getByRole('link', { name: 'Studies', exact: true }).click()
  await expect(viewer.getByRole('link', { name: 'NCT00000002', exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Log out', exact: true }).click()
  await expect(page.getByLabel('Username')).toBeVisible()
  await viewer.reload()
  await expect(viewer.getByRole('link', { name: 'NCT00000001', exact: true })).toBeVisible()
  await viewerContext.close()
})
