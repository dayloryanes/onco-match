import { test, expect } from '@playwright/test'
import type { Page } from '@playwright/test'

async function dashboard(page: Page, { extraCards = false, renewSession = false, role = 'Viewer' } = {}) {
  let updated = false
  let failImports = false
  let failInspection = false
  let gates: Record<string, { wait: Promise<void>; release: () => void }> | null = null
  let reached!: () => void
  let requested = Promise.resolve()
  const seen = new Set<string>()
  const completions: Promise<void>[] = []
  const attempt = (id: string) => ({ id, status: 'completed', startedAt: '2026-11-03T12:00:00Z', finishedAt: '2026-11-03T12:01:00Z',
    retryOfAttemptId: null, fetchedCount: 10, importedCount: 10, duplicateCount: 0, rejectedCount: 0,
    importedNctIds: ['NCT00000001'], duplicateNctIds: [], rejections: [], failureCode: null })
  const pair = { batchId: 'original', patientId: 'P001', nctId: 'NCT00000001', state: 'completed', pairSummary: 'conditionLabelAlignmentUndetermined', structuredChecks: null, latestAttempt: null }
  const batch = (id: string) => ({ batchId: id, version: 'v1', status: 'completed', totalPairCount: 10, completedPairCount: 10,
    failedPairCount: 0, unfinishedPairCount: 0, activePairCount: 0, activeOperation: null, availableActions: [], pairs: [pair] })
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname
    const read = path === '/api/study-imports' ? 'imports' : path === '/api/match/batches' ? 'batches' : path === '/api/match/batches/original' ? 'detail' : null
    let finish = () => {}
    if (gates && read) {
      completions.push(new Promise<void>(resolve => { finish = resolve }))
      seen.add(read)
      if (seen.size === 3) reached()
      await gates[read].wait
    }
    try {
      if (route.request().failure()) return
      if (path === '/api/study-imports' && failImports) { await route.fulfill({ status: 503, json: { code: 'unavailable' } }); return }
      if (path.startsWith('/api/match/pairs/') && failInspection) { await route.fulfill({ status: 503, json: { code: 'unavailable' } }); return }
      const original = extraCards ? ['original', 'older', 'last'] : ['original']
      const ids = updated ? ['newest', 'another', ...original] : original
      const now = new Date()
      const timing = { serverTime: now.toISOString(), expiresAt: new Date(+now + 30 * 60_000).toISOString() }
      const body = path === '/api/auth/me' ? { username: role.toLowerCase(), role, ...(renewSession ? { serverTime: timing.serverTime, expiresAt: new Date(+now + 4 * 60_000).toISOString() } : {}) }
        : path === '/api/patients' ? Array.from({ length: 25 }, (_, i) => ({ id: `P${i}`, ageYears: 42, sex: 'female', cancerType: 'Cancer' }))
        : path === '/api/study-imports' ? { version: updated ? 'v2' : 'v1', isImporting: false, isExhausted: updated, nextAction: 'next', totalImportedStudyCount: ids.length * 10, attempts: ids.map(attempt) }
        : path === '/api/match/batches' ? { configurationError: null, activeOperation: null, batches: ids.map(batch) }
        : path === '/api/match/batches/original' ? batch('original')
        : { ...pair, conditionLabels: [], skipReason: 'Retained assessment.', assessmentLimits: 'Limited assessment.', inputSnapshot: {
          patient: { ageYears: 42, sex: 'female', cancerType: 'Breast cancer', stage: { status: 'known', value: 'II', classificationSystem: 'TNM' }, diseaseState: { status: 'known', value: 'recurrent' } },
          study: { minimumAge: '18 Years', maximumAge: '65 Years', sex: 'ALL', conditions: ['Breast cancer'] },
        } }
      const headers = renewSession && updated && path === '/api/study-imports'
        ? { 'X-Session-Expires-At': timing.expiresAt, 'X-Server-Time': timing.serverTime, 'X-Session-Renewed': 'true' } : undefined
      await route.fulfill({ json: body, headers })
    } finally { finish() }
  })
  await page.goto('/')
  await page.getByRole('button', { name: 'View batch 1', exact: true }).click()
  await page.getByRole('button', { name: 'Inspect P001 NCT00000001', exact: true }).click()
  const inspection = page.getByRole('region', { name: 'Assessment P001 NCT00000001', exact: true })
  await inspection.getByText('Exact input snapshot', { exact: true }).click()
  const heading = inspection.getByRole('heading', { name: 'Assessment: P001 · NCT00000001', exact: true })
  await heading.evaluate(element => window.scrollTo({ top: window.scrollY + element.getBoundingClientRect().top - 100, behavior: 'instant' }))
  return {
    inspection, heading,
    update() { updated = true },
    failImports(value: boolean) { failImports = value },
    failInspection(value: boolean) { failInspection = value },
    hold() {
      seen.clear()
      requested = new Promise<void>(resolve => { reached = resolve })
      gates = Object.fromEntries(['imports', 'batches', 'detail'].map(name => {
        let release!: () => void
        const wait = new Promise<void>(resolve => { release = resolve })
        return [name, { wait, release }]
      }))
    },
    requested() { return requested },
    release(name: string) { gates![name].release() },
    releaseAll() { Object.values(gates ?? {}).forEach(gate => gate.release()) },
    async finished() { await Promise.all(completions) },
  }
}

for (const width of [1280, 390]) {
  test(`Refresh preserves the visible assessment without native anchoring at ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 800 })
    const fixture = await dashboard(page)
    // Make the application's guarantee independent of native scroll anchoring.
    await page.addStyleTag({ content: 'html { overflow-anchor: none; }' })
    const before = (await fixture.heading.boundingBox())!.y
    fixture.update()
    await page.getByRole('button', { name: 'Refresh', exact: true }).evaluate((button: HTMLButtonElement) => button.click())
    await expect(page.getByText('30 studies', { exact: true })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Batch 1 · Completed', exact: true })).toBeVisible()
    await expect.poll(async () => Math.abs((await fixture.heading.boundingBox())!.y - before)).toBeLessThan(1)
    await expect(fixture.inspection.locator('details')).toHaveAttribute('open', '')
  })

  test(`Refresh keeps the top controls in view at ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 800 })
    const fixture = await dashboard(page)
    await page.evaluate(() => window.scrollTo({ top: 0, behavior: 'instant' }))
    fixture.update()
    await page.getByRole('button', { name: 'Refresh', exact: true }).click()
    await expect(page.getByText('30 studies', { exact: true })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Batch 1 · Completed', exact: true })).toBeVisible()
    await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(0)
    await expect(fixture.inspection.locator('details')).toHaveAttribute('open', '')
  })

  test(`Refresh follows deliberate scrolling between delayed updates at ${width}px`, async ({ page }) => {
    await page.setViewportSize({ width, height: 800 })
    const fixture = await dashboard(page)
    fixture.update()
    fixture.hold()
    try {
      await page.getByRole('button', { name: 'Refresh', exact: true }).evaluate((button: HTMLButtonElement) => button.click())
      await fixture.requested()
      const initialScroll = await page.evaluate(() => window.scrollY)
      await page.mouse.move(width / 2, 400)
      await page.mouse.wheel(0, 120)
      await expect.poll(() => page.evaluate(() => window.scrollY)).toBeGreaterThan(initialScroll + 80)
      const beforeImports = (await fixture.heading.boundingBox())!.y
      fixture.release('imports')
      await expect(page.getByText('30 studies', { exact: true })).toBeVisible()
      await expect.poll(async () => Math.abs((await fixture.heading.boundingBox())!.y - beforeImports)).toBeLessThan(1)
      const afterImports = await page.evaluate(() => window.scrollY)
      await page.mouse.wheel(0, -80)
      await expect.poll(() => page.evaluate(() => window.scrollY)).toBeLessThan(afterImports - 50)
      const beforeBatches = (await fixture.heading.boundingBox())!.y
      fixture.release('batches'); fixture.release('detail')
      await expect(page.getByRole('heading', { name: 'Batch 1 · Completed', exact: true })).toBeVisible()
      await expect.poll(async () => Math.abs((await fixture.heading.boundingBox())!.y - beforeBatches)).toBeLessThan(1)
    } finally { fixture.releaseAll() }
  })
}

test('Import progress and completion preserve the visible inspection without native anchoring', async ({ page }) => {
  const fixture = await dashboard(page, { role: 'Operator' })
  await page.addStyleTag({ content: 'html { overflow-anchor: none; }' })
  let release!: () => void
  let reached!: () => void
  const held = new Promise<void>(resolve => { release = resolve })
  const requested = new Promise<void>(resolve => { reached = resolve })
  await page.route('**/api/study-imports', async route => {
    if (route.request().method() !== 'POST') { await route.fallback(); return }
    reached(); await held; fixture.update()
    await route.fulfill({ json: { version: 'v2', isImporting: false, isExhausted: true, nextAction: 'none', totalImportedStudyCount: 30, attempts: [] } })
  })
  const before = (await fixture.heading.boundingBox())!.y
  try {
    await page.getByRole('button', { name: 'Load Next 10', exact: true }).evaluate((button: HTMLButtonElement) => button.click())
    await requested
    await expect.poll(async () => (await fixture.heading.boundingBox())!.y).toBeCloseTo(before, 0)
    release()
    await expect(page.getByText('30 studies', { exact: true })).toBeAttached()
    await expect(page.getByRole('button', { name: 'Close batch 3', exact: true })).toBeAttached()
    await expect(fixture.inspection.locator('details')).toHaveAttribute('open', '')
    await expect.poll(async () => Math.abs((await fixture.heading.boundingBox())!.y - before)).toBeLessThan(1)
  } finally { release() }
})

test('A failed inspection refresh retains the open snapshot and its reading position', async ({ page }) => {
  const fixture = await dashboard(page)
  await page.addStyleTag({ content: 'html { overflow-anchor: none; }' })
  await fixture.inspection.evaluate(element => element.setAttribute('data-retained', 'true'))
  const before = (await fixture.heading.boundingBox())!.y
  fixture.failInspection(true)
  await page.getByRole('button', { name: 'Refresh', exact: true }).evaluate((button: HTMLButtonElement) => button.click())
  await expect(page.getByText(/Unable to load this assessment/)).toBeAttached()
  await expect(fixture.inspection).toHaveAttribute('data-retained', 'true')
  await expect(fixture.inspection.locator('details')).toHaveAttribute('open', '')
  await expect.poll(async () => (await fixture.heading.boundingBox())!.y).toBeCloseTo(before, 0)
})

test('Refresh preserves reading position with native anchoring enabled', async ({ page }) => {
  const fixture = await dashboard(page)
  const before = (await fixture.heading.boundingBox())!.y
  fixture.update()
  await page.getByRole('button', { name: 'Refresh', exact: true }).evaluate((button: HTMLButtonElement) => button.click())
  await expect(page.getByText('30 studies', { exact: true })).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Batch 1 · Completed', exact: true })).toBeVisible()
  await expect.poll(async () => Math.abs((await fixture.heading.boundingBox())!.y - before)).toBeLessThan(1)
  expect(await page.evaluate(() => document.documentElement.style.overflowAnchor)).toBe('')
})

test('Refresh errors and a successful retry retain the current reading position', async ({ page }) => {
  const fixture = await dashboard(page)
  await page.addStyleTag({ content: 'html { overflow-anchor: none; }' })
  const before = (await fixture.heading.boundingBox())!.y
  fixture.failImports(true)
  await page.getByRole('button', { name: 'Refresh', exact: true }).evaluate((button: HTMLButtonElement) => button.click())
  await expect(page.getByRole('alert')).toHaveText('Unable to load import history.')
  await expect.poll(async () => Math.abs((await fixture.heading.boundingBox())!.y - before)).toBeLessThan(1)
  fixture.failImports(false); fixture.update()
  await page.getByRole('button', { name: 'Refresh', exact: true }).evaluate((button: HTMLButtonElement) => button.click())
  await expect(page.getByText('30 studies', { exact: true })).toBeVisible()
  await expect(page.getByRole('alert')).toHaveCount(0)
  await expect.poll(async () => Math.abs((await fixture.heading.boundingBox())!.y - before)).toBeLessThan(1)
})

test('Operator cannot import after a failed history Refresh until an explicit retry succeeds', async ({ page }) => {
  const fixture = await dashboard(page, { role: 'Operator' })
  const posts: string[] = []
  page.on('request', request => { if (request.method() === 'POST' && new URL(request.url()).pathname === '/api/study-imports') posts.push(request.url()) })
  await page.evaluate(() => window.scrollTo({ top: 0, behavior: 'instant' }))
  await expect(page.getByRole('button', { name: 'Load Next 10', exact: true })).toBeEnabled()
  fixture.failImports(true)
  await page.getByRole('button', { name: 'Refresh', exact: true }).click()
  await expect(page.getByText('Unable to load import history.', { exact: true })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Load Next 10', exact: true })).toBeDisabled()
  await expect(fixture.inspection).toBeAttached()
  expect(posts).toEqual([])
  fixture.failImports(false)
  await page.getByRole('button', { name: 'Refresh', exact: true }).click()
  await expect(page.getByRole('button', { name: 'Load Next 10', exact: true })).toBeEnabled()
  await expect(page.getByText('Unable to load import history.', { exact: true })).toHaveCount(0)
  expect(posts).toEqual([])
})

test('Leaving Dashboard while Refresh is pending does not move the destination page', async ({ page }) => {
  const fixture = await dashboard(page)
  fixture.update(); fixture.hold()
  try {
    await page.getByRole('button', { name: 'Refresh', exact: true }).evaluate((button: HTMLButtonElement) => button.click())
    await fixture.requested()
    await page.evaluate(() => window.scrollTo({ top: 0, behavior: 'instant' }))
    await page.getByRole('link', { name: 'Patients', exact: true }).click()
    await expect(page.getByRole('link', { name: 'P24', exact: true })).toBeVisible()
    await page.evaluate(() => window.scrollTo({ top: 300, behavior: 'instant' }))
    fixture.releaseAll()
    await fixture.finished()
    await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(300)
    expect(await page.evaluate(() => document.documentElement.style.overflowAnchor)).toBe('')
  } finally { fixture.releaseAll() }
})

test('Refresh anchors a visible card when the viewport center is between cards', async ({ page }) => {
  const fixture = await dashboard(page, { extraCards: true })
  await page.getByRole('button', { name: 'Close batch 1', exact: true }).click()
  await page.addStyleTag({ content: 'html { overflow-anchor: none; }' })
  const oldCard = page.getByRole('listitem', { name: 'Completed import', exact: true }).filter({ hasText: 'Attempt: original' })
  await oldCard.evaluate(card => {
    const middle = (card.getBoundingClientRect().bottom + card.nextElementSibling!.getBoundingClientRect().top) / 2
    window.scrollTo({ top: window.scrollY + middle - window.innerHeight / 2, behavior: 'instant' })
  })
  expect(await page.evaluate(() => document.elementFromPoint(window.innerWidth / 2, window.innerHeight / 2)?.className)).toBe('import-history')
  const before = (await oldCard.boundingBox())!.y
  fixture.update()
  await page.getByRole('button', { name: 'Refresh', exact: true }).evaluate((button: HTMLButtonElement) => button.click())
  await expect(page.getByText('50 studies', { exact: true })).toBeVisible()
  await expect.poll(async () => Math.abs((await oldCard.boundingBox())!.y - before)).toBeLessThan(1)
})

test('Refresh preserves visible content when session renewal removes the warning above it', async ({ page }) => {
  const fixture = await dashboard(page, { renewSession: true })
  await expect(page.getByRole('button', { name: 'Stay signed in', exact: true })).toBeVisible()
  await page.addStyleTag({ content: 'html { overflow-anchor: none; }' })
  const before = (await fixture.heading.boundingBox())!.y
  fixture.update()
  await page.getByRole('button', { name: 'Refresh', exact: true }).evaluate((button: HTMLButtonElement) => button.click())
  await expect(page.getByText('30 studies', { exact: true })).toBeVisible()
  await expect(page.getByRole('button', { name: 'Stay signed in', exact: true })).toHaveCount(0)
  await expect.poll(async () => Math.abs((await fixture.heading.boundingBox())!.y - before)).toBeLessThan(1)
})

test('A late renewing import response preserves inspection position after matching rows refresh', async ({ page }) => {
  const fixture = await dashboard(page, { renewSession: true })
  await expect(page.getByRole('button', { name: 'Stay signed in', exact: true })).toBeVisible()
  await page.addStyleTag({ content: 'html { overflow-anchor: none; }' })
  const before = (await fixture.heading.boundingBox())!.y
  fixture.update(); fixture.hold()
  try {
    await page.getByRole('button', { name: 'Refresh', exact: true }).evaluate((button: HTMLButtonElement) => button.click())
    await fixture.requested()
    fixture.release('batches'); fixture.release('detail')
    await expect(page.getByRole('button', { name: 'Close batch 3', exact: true })).toBeVisible()
    fixture.release('imports')
    await expect(page.getByRole('button', { name: 'Stay signed in', exact: true })).toHaveCount(0)
    await expect(page.getByText('30 studies', { exact: true })).toBeVisible()
    await expect.poll(async () => Math.abs((await fixture.heading.boundingBox())!.y - before)).toBeLessThan(1)
  } finally { fixture.releaseAll() }
})
