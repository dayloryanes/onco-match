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
  const firstImport = page.getByRole('listitem', { name: 'Completed import', exact: true })
  const importHeading = firstImport.getByRole('heading', { name: 'Completed import', exact: true })
  const batchHeading = firstImport.getByRole('heading', { name: 'Batch 1 · Not started', exact: true })
  await expect(batchHeading).toBeVisible()
  const desktopImport = (await importHeading.boundingBox())!
  const desktopBatch = (await batchHeading.boundingBox())!
  expect(desktopBatch.x).toBeGreaterThan(desktopImport.x + 200)
  await page.screenshot({ path: 'test-results/combined-import-desktop.png', fullPage: true })
  await page.setViewportSize({ width: 390, height: 844 })
  const mobileImport = (await importHeading.boundingBox())!
  const mobileBatch = (await batchHeading.boundingBox())!
  expect(Math.abs(mobileBatch.x - mobileImport.x)).toBeLessThan(2)
  expect(mobileBatch.y).toBeGreaterThan(mobileImport.y)
  await page.screenshot({ path: 'test-results/combined-import-mobile.png', fullPage: true })
  await page.setViewportSize({ width: 1280, height: 720 })
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
  await firstImport.getByRole('button', { name: 'Close batch 1', exact: true }).click()
  await expect(firstImport.getByRole('region', { name: 'Batch assessments', exact: true })).toHaveCount(0)
  await expect(firstImport.getByRole('button', { name: 'Cancel match', exact: true })).toBeVisible()
  await firstImport.getByRole('button', { name: 'View batch 1', exact: true }).click()
  await expect(firstImport.getByRole('region', { name: 'Batch assessments', exact: true })).toBeVisible()
  // Focus checks preserve Dashboard and the initiating stream.
  await page.evaluate(() => window.dispatchEvent(new Event('focus')))
  await expect(page.getByRole('button', { name: 'Cancel match', exact: true })).toBeVisible()
  await expect(page.getByText('10/10 completed', { exact: true })).toBeVisible({ timeout: 25000 })
  const streamEvidence = await page.evaluate(() => (window as Window & { matchEvidence?: Promise<string> }).matchEvidence)
  expect(streamEvidence).toContain('heartbeat')
  expect(streamEvidence).toContain('operationFinished')
  const resultsTable = firstImport.getByRole('table')
  const collapseControl = firstImport.getByRole('button', { name: 'Collapse results', exact: true })
  const tableBox = (await resultsTable.locator('xpath=..').boundingBox())!
  const collapseBox = (await collapseControl.boundingBox())!
  expect(collapseBox.y - (tableBox.y + tableBox.height)).toBeCloseTo(16, 1)
  await page.getByRole('button', { name: 'Inspect P005 NCT00000001', exact: true }).click()
  const assessment = page.getByRole('region', { name: 'Assessment P005 NCT00000001', exact: true })
  await expect(assessment).toBeVisible()
  const closeAssessment = firstImport.getByRole('button', { name: 'Close assessment', exact: true })
  await expect(assessment.getByRole('button', { name: 'Close assessment', exact: true })).toHaveCount(0)
  const assessmentBox = (await assessment.boundingBox())!
  expect((await closeAssessment.boundingBox())!.y).toBeGreaterThanOrEqual(assessmentBox.y + assessmentBox.height + 12)
  const surroundingRows = await assessment.locator('xpath=ancestor::tr[1]').evaluate(row => ({ before: row.previousElementSibling?.textContent, after: row.nextElementSibling?.textContent }))
  expect(surroundingRows.before).toContain('P005')
  expect(surroundingRows.after).toContain('P006')
  await closeAssessment.click()
  await expect(assessment).toHaveCount(0)
  await expect(page.getByRole('button', { name: 'Inspect P005 NCT00000001', exact: true })).toBeFocused()
  await page.getByRole('button', { name: 'Inspect P005 NCT00000001', exact: true }).click()
  await expect(assessment).toBeVisible()
  await expect(firstImport.locator('p.notice')).toHaveCount(1)
  await expect(assessment.getByRole('heading', { name: 'Condition 1 · Cancer', exact: true })).toBeVisible()
  await expect(assessment.getByRole('heading', { name: 'Requirement summary', exact: true })).toBeVisible()
  await expect(assessment.getByText('Examined: Study condition 1', { exact: true })).toHaveCount(1)
  for (const width of [1280, 390]) {
    await page.setViewportSize({ width, height: 844 })
    const layout = await assessment.evaluate(panel => {
      const content = panel.querySelector('.assessment-content')!
      const condition = content.querySelector('.condition-assessment')!
      const groups = [...condition.children]
      const finding = groups[3]
      const heading = condition.querySelector('h4')!
      const label = finding.querySelector('h5')!
      const explanation = finding.querySelector('p')!
      const evidence = finding.querySelector('ul')!
      const divider = getComputedStyle(condition)
      return {
        headingSize: getComputedStyle(heading).fontSize, headingWeight: getComputedStyle(heading).fontWeight,
        labelSize: getComputedStyle(label).fontSize, labelWeight: getComputedStyle(label).fontWeight,
        bodySize: getComputedStyle(explanation).fontSize, bodyWeight: getComputedStyle(explanation).fontWeight,
        evidenceSize: getComputedStyle(evidence).fontSize, bullets: getComputedStyle(evidence).listStyleType,
        textGap: explanation.getBoundingClientRect().top - label.getBoundingClientRect().bottom,
        groupGap: groups[4].getBoundingClientRect().top - finding.getBoundingClientRect().bottom,
        dividerBefore: divider.marginTop, dividerAfter: divider.paddingTop,
        overflow: panel.scrollWidth > panel.clientWidth,
      }
    })
    expect(layout).toMatchObject({ headingSize: '16px', headingWeight: '700', labelSize: '15.2px', labelWeight: '600',
      bodySize: '15.2px', bodyWeight: '400', evidenceSize: '15.2px', bullets: 'disc', dividerBefore: '12px', dividerAfter: '12px', overflow: false })
    expect(layout.textGap).toBeCloseTo(8, 1)
    expect(layout.groupGap).toBeCloseTo(16, 1)
    await assessment.screenshot({ path: `test-results/assessment-layout-${width}.png` })
  }
  await page.setViewportSize({ width: 1280, height: 720 })
  await assessment.getByText('Exact input snapshot', { exact: true }).click()
  const patientSnapshot = assessment.getByRole('region', { name: 'Patient snapshot', exact: true })
  const studySnapshot = assessment.getByRole('region', { name: 'Study snapshot', exact: true })
  await expect(patientSnapshot.getByText('42', { exact: true })).toBeVisible()
  await expect(patientSnapshot.getByText('Female', { exact: true })).toBeVisible()
  await expect(patientSnapshot.getByText('Stage at diagnosis', { exact: true })).toBeVisible()
  await expect(patientSnapshot.getByText('Classification system', { exact: true })).toBeVisible()
  await expect(studySnapshot.locator('dd > ul')).toHaveCount(1)
  await expect(studySnapshot.locator('dd > ol')).toHaveCount(0)
  await expect(patientSnapshot.getByText('Age bounds', { exact: true })).toHaveCount(0)
  await expect(studySnapshot.getByText('Age bounds', { exact: true })).toBeVisible()
  await expect(studySnapshot.getByText('All', { exact: true })).toBeVisible()
  expect((await studySnapshot.boundingBox())!.y).toBeGreaterThan((await patientSnapshot.boundingBox())!.y + (await patientSnapshot.boundingBox())!.height)
  await expect(page).toHaveURL('https://localhost:5173/')
  await page.screenshot({ path: 'test-results/dashboard-assessment.png', fullPage: true })
  await page.setViewportSize({ width: 390, height: 844 })
  await page.screenshot({ path: 'test-results/inline-assessment-mobile.png', fullPage: true })
  await page.setViewportSize({ width: 1280, height: 720 })
  await firstImport.getByRole('button', { name: 'Collapse results', exact: true }).click()
  await expect(firstImport.getByRole('region', { name: 'Batch assessments', exact: true })).toHaveCount(0)
  await expect(firstImport.getByRole('button', { name: 'View batch 1', exact: true })).toBeFocused()
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
  const cancelledItem = page.getByRole('heading', { name: /^Batch 2 ·/ }).locator('xpath=ancestor::li[1]')
  const cancellationNotice = cancelledItem.locator(':scope > .match-message[role="status"]')
  // This pass changes presentation only; either existing cancellation update
  // may be the latest one depending on response/stream arrival order.
  await expect(cancellationNotice).toHaveText(/^(Cancellation requested\. Waiting for active work to stop\.|Match cancelled\. Saved results are retained\.)$/)
  for (const width of [1280, 390]) {
    await page.setViewportSize({ width, height: 844 })
    const noticeBox = (await cancellationNotice.boundingBox())!
    const cardBox = (await cancelledItem.locator(':scope > .import-attempt').boundingBox())!
    expect(cardBox.y - (noticeBox.y + noticeBox.height)).toBeCloseTo(16, 1)
    expect(Math.abs(cardBox.x - noticeBox.x)).toBeLessThan(1)
    await expect(cancellationNotice).toHaveCSS('font-size', '13.6px')
    await expect(cancellationNotice).toHaveCSS('background-color', 'rgb(255, 248, 232)')
    await expect(cancellationNotice).toHaveCSS('color', 'rgb(80, 59, 22)')
    await cancelledItem.screenshot({ path: `test-results/batch-notice-${width}.png` })
  }
  await page.setViewportSize({ width: 1280, height: 720 })
  await expect(cancelledItem.getByRole('button', { name: 'Continue unfinished', exact: true })).toBeEnabled()
  // Recover the stopped batch through the real API and proxy. The external
  // model transport fails deterministically, then succeeds after explicit retry.
  await page.request.get('http://127.0.0.1:7133/test/inference?mode=invalid')
  await cancelledItem.getByRole('button', { name: 'Continue unfinished', exact: true }).click()
  await expect(cancelledItem.getByRole('heading', { name: 'Batch 2 · Finished with failures', exact: true })).toBeVisible()
  await expect(cancelledItem.getByRole('button', { name: 'Continue unfinished', exact: true })).toHaveCount(0)
  await cancelledItem.getByRole('button', { name: 'Inspect P005 NCT00000002', exact: true }).click()
  const failedAssessment = cancelledItem.getByRole('region', { name: 'Assessment P005 NCT00000002', exact: true })
  await failedAssessment.getByText('Attempt history', { exact: true }).click()
  await expect(failedAssessment.getByRole('heading', { name: 'Attempt 1: Interrupted · Cancelled', exact: true })).toBeVisible()
  await expect(failedAssessment.getByRole('heading', { name: 'Attempt 2: Failed', exact: true })).toBeVisible()
  await cancelledItem.getByRole('button', { name: 'Retry failed', exact: true }).click()
  await expect(cancelledItem.getByRole('button', { name: 'Retry failed', exact: true })).toBeEnabled()
  await expect(cancelledItem.getByRole('heading', { name: 'Batch 2 · Finished with failures', exact: true })).toBeVisible()
  await cancelledItem.getByRole('button', { name: 'Inspect P005 NCT00000002', exact: true }).click()
  await failedAssessment.getByText('Attempt history', { exact: true }).click()
  await expect(failedAssessment.getByRole('heading', { name: 'Attempt 3: Failed', exact: true })).toBeVisible()
  const recoveryViewer = await viewerContext.newPage()
  await recoveryViewer.goto('https://localhost:5173/')
  await recoveryViewer.getByRole('button', { name: 'View batch 2', exact: true }).click()
  await recoveryViewer.getByRole('button', { name: 'Inspect P005 NCT00000002', exact: true }).click()
  const sharedInspection = recoveryViewer.getByRole('region', { name: 'Assessment P005 NCT00000002', exact: true })
  await sharedInspection.getByText('Attempt history', { exact: true }).click()
  await expect(sharedInspection.getByRole('heading', { name: 'Attempt 3: Failed', exact: true })).toBeVisible()
  await sharedInspection.evaluate(element => { element.dataset.retained = 'true' })
  await expect(recoveryViewer.getByRole('button', { name: /Continue unfinished|Retry failed/ })).toHaveCount(0)
  await page.request.get('http://127.0.0.1:7133/test/inference?mode=valid')
  await cancelledItem.getByRole('button', { name: 'Retry failed', exact: true }).click()
  await expect(cancelledItem.getByRole('heading', { name: 'Batch 2 · Completed', exact: true })).toBeVisible()
  await expect(cancelledItem.getByRole('button', { name: /Continue unfinished|Retry failed/ })).toHaveCount(0)
  await cancelledItem.getByRole('button', { name: 'Inspect P005 NCT00000002', exact: true }).click()
  await failedAssessment.getByText('Attempt history', { exact: true }).click()
  await expect(failedAssessment.getByRole('heading', { name: 'Attempt 4: Completed', exact: true })).toBeVisible()
  await recoveryViewer.getByRole('button', { name: 'Refresh', exact: true }).click()
  await expect(sharedInspection.getByRole('heading', { name: 'Attempt 4: Completed', exact: true })).toBeVisible()
  await expect(sharedInspection).toHaveAttribute('data-retained', 'true')
  await recoveryViewer.close()
  for (const width of [1280, 390]) {
    await page.setViewportSize({ width, height: 844 })
    await failedAssessment.screenshot({ path: `test-results/recovery-history-${width}.png` })
    expect(await failedAssessment.evaluate(panel => panel.scrollWidth > panel.clientWidth)).toBe(false)
  }
  await page.setViewportSize({ width: 1280, height: 720 })
  const viewerAssessment = viewer.getByRole('region', { name: 'Assessment P005 NCT00000001', exact: true })
  await viewerAssessment.getByText('Exact input snapshot', { exact: true }).click()
  await viewerAssessment.getByRole('heading', { name: 'Assessment: P005 · NCT00000001', exact: true }).scrollIntoViewIfNeeded()
  await viewerAssessment.evaluate(element => { element.dataset.retained = 'true' })
  await expect(viewer.getByRole('button', { name: 'Refresh assessments', exact: true })).toHaveCount(0)
  // Refresh is at the top of Dashboard. Follow the normal user flow and stay
  // near those controls when newly imported cards appear below them.
  await viewer.evaluate(() => window.scrollTo({ top: 0, left: 0, behavior: 'instant' }))
  await expect.poll(() => viewer.evaluate(() => window.scrollY)).toBe(0)
  await viewer.getByRole('button', { name: 'Refresh', exact: true }).click()
  // The new batch proves saved matching state was applied, not merely requested.
  await expect(viewer.getByRole('heading', { name: 'Batch 2', exact: false })).toBeVisible()
  await expect(viewerAssessment).toHaveAttribute('data-retained', 'true')
  await expect(viewerAssessment.locator('details').filter({ has: viewer.getByText('Exact input snapshot', { exact: true }) })).toHaveAttribute('open', '')
  await expect(viewer.getByRole('button', { name: 'Close batch 1', exact: true })).toHaveAttribute('aria-expanded', 'true')
  await expect.poll(() => viewer.evaluate(() => window.scrollY)).toBe(0)
  const newCard = viewer.getByRole('heading', { name: 'Batch 2', exact: false }).locator('xpath=ancestor::li[1]')
  const placement = await newCard.evaluate(card => {
    const controls = document.querySelector('.import-actions')!
    const existingCard = document.querySelector('.assessment-detail')!.closest('li')!
    return {
      belowControlsAndCount: card.getBoundingClientRect().top >= controls.nextElementSibling!.getBoundingClientRect().bottom,
      beforeExistingCard: !!(card.compareDocumentPosition(existingCard) & Node.DOCUMENT_POSITION_FOLLOWING),
    }
  })
  expect(placement).toEqual({ belowControlsAndCount: true, beforeExistingCard: true })
  await expect(viewer.getByText('2 studies', { exact: true })).toBeVisible()
  await viewer.getByRole('link', { name: 'Studies', exact: true }).click()
  await expect(viewer.getByRole('link', { name: 'NCT00000002', exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Log out', exact: true }).click()
  await expect(page.getByLabel('Username')).toBeVisible()
  await viewer.reload()
  await expect(viewer.getByRole('link', { name: 'NCT00000001', exact: true })).toBeVisible()
  await viewerContext.close()
})
