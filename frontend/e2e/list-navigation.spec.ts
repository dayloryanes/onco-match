import { test, expect } from '@playwright/test'
import type { Page } from '@playwright/test'

// These frontend navigation checks control application HTTP responses so that
// list size/loading are deterministic. Import integration remains in studies.spec.
async function directory(page: Page, kind: 'patients' | 'studies', sessionMinutes?: number) {
  const records = Array.from({ length: kind === 'patients' ? 10 : 80 }, (_, index) => kind === 'patients' ? {
    id: `P${String(index + 1).padStart(3, '0')}`, ageYears: 42, sex: 'female', cancerType: 'Controlled cancer type',
    stage: { status: 'unknown', value: null, classificationSystem: null }, diseaseState: { status: 'unknown', value: null },
  } : {
    nctId: `NCT${String(index + 1).padStart(8, '0')}`, title: `Controlled study ${index + 1}`,
    recruitmentStatus: 'RECRUITING', studyType: 'INTERVENTIONAL', conditions: ['Cancer'],
    briefSummary: 'Controlled source information.', minimumAge: null, maximumAge: null, sex: null,
    registryUrl: `https://clinicaltrials.gov/study/NCT${String(index + 1).padStart(8, '0')}`,
  })
  let signedIn = true
  let listReads = 0
  let releaseList = () => {}
  let delayList = false
  let delaySession = false
  let sessionReads = 0
  let releaseSessions: (() => void)[] = []
  await page.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname
    let body: unknown
    if (path === '/api/auth/csrf') body = { requestToken: 'navigation-test' }
    else if (path === '/api/auth/logout') { signedIn = false; await route.fulfill({ status: 204 }); return }
    else if (path === '/api/auth/login') { signedIn = true; body = { username: 'viewer', role: 'Viewer' } }
    else if (!signedIn) { await route.fulfill({ status: 401, json: { code: 'authentication_required' } }); return }
    else if (path === '/api/auth/me') {
      sessionReads++
      const username = `viewer-check-${sessionReads}`
      if (delaySession) await new Promise<void>(resolve => { releaseSessions.push(resolve) })
      const now = new Date()
      body = { username, role: 'Viewer', ...(sessionMinutes ? { serverTime: now.toISOString(), expiresAt: new Date(+now + sessionMinutes * 60_000).toISOString() } : {}) }
    }
    else if (path === '/api/study-imports') body = { version: 'v1', isImporting: false, isExhausted: false, nextAction: 'initial', totalImportedStudyCount: 0, attempts: [] }
    else if (path === '/api/match/batches') body = { configurationError: null, activeOperation: null, batches: [] }
    else if (path === '/api/match/pairs') body = []
    else if (path === `/api/${kind}`) {
      listReads++
      if (delayList) await new Promise<void>(resolve => { releaseList = resolve })
      body = records
    } else if (path.startsWith(`/api/${kind}/`)) {
      const id = path.split('/').at(-1)
      body = records.find(record => ('id' in record ? record.id : record.nctId) === id)
      if (!body) { await route.fulfill({ status: 404, json: {} }); return }
    } else { await route.fallback(); return }
    await route.fulfill({ json: body })
  })
  return {
    delay: () => { delayList = true }, release: () => { delayList = false; releaseList() }, reads: () => listReads,
    sessionReads: () => sessionReads,
    confirmedUser: () => `viewer-check-${sessionReads} · Viewer`,
    delaySession: () => { delaySession = true }, releaseSession: () => {
      delaySession = false; releaseSessions.forEach(release => release()); releaseSessions = []
    }, expire: () => { signedIn = false },
  }
}

for (const path of ['/patients', '/patients/P001', '/studies', '/studies/NCT00000001']) for (const width of [1280, 390]) {
test(`A countdown warning preserves reading position on ${path} at ${width}px`, async ({ page }) => {
  await page.setViewportSize({ width, height: 450 })
  await page.clock.install()
  await directory(page, path.startsWith('/patients') ? 'patients' : 'studies', 6)
  await page.goto(path)
  await page.locator('tbody tr, .patient-facts > div').first().waitFor()
  await page.addStyleTag({ content: 'html { overflow-anchor: none; }' })
  await page.evaluate(() => window.scrollTo(0, 800))
  const before = await page.evaluate(() => {
    const row = [...document.querySelectorAll('tbody tr, .patient-facts > div')].find(element => {
      const rect = element.getBoundingClientRect()
      return rect.top >= 0 && rect.bottom <= innerHeight
    })!
    row.setAttribute('data-reading-anchor', 'true')
    return row.getBoundingClientRect().top
  })
  await page.clock.fastForward(61_000)
  await expect(page.getByRole('button', { name: 'Stay signed in', exact: true })).toBeAttached()
  // Browser scroll offsets round to physical pixels; tolerate subpixel layout.
  await expect.poll(async () => Math.abs(await page.locator('[data-reading-anchor]').evaluate(element => element.getBoundingClientRect().top) - before)).toBeLessThan(1)
})
}

test('Studies Refresh preserves the current reading position while loading and after completion', async ({ page }) => {
  const fixture = await directory(page, 'studies')
  await page.goto('/studies')
  await expect(page.getByRole('link', { name: 'NCT00000080', exact: true })).toBeAttached()
  await page.addStyleTag({ content: 'html { overflow-anchor: none; }' })
  await page.evaluate(() => window.scrollTo(0, 800))
  const before = await page.evaluate(() => window.scrollY)
  const readsBefore = fixture.reads()
  fixture.delay()
  try {
    await page.getByRole('button', { name: 'Refresh', exact: true }).evaluate((button: HTMLButtonElement) => button.click())
    await expect.poll(fixture.reads).toBeGreaterThan(readsBefore)
    await expect(page.getByRole('link', { name: 'NCT00000080', exact: true })).toBeAttached()
    await expect.poll(() => page.evaluate(() => window.scrollY)).toBeCloseTo(before, 0)
    fixture.release()
    await expect(page.getByRole('button', { name: 'Refresh', exact: true })).toBeEnabled()
    await expect.poll(() => page.evaluate(() => window.scrollY)).toBeCloseTo(before, 0)
  } finally { fixture.release() }
})

test('the first navigation click works while a focus session check is pending', async ({ page }) => {
  const fixture = await directory(page, 'patients')
  await page.goto('/')
  const target = page.getByRole('link', { name: 'Patients', exact: true })
  await expect(target).toBeVisible()
  const bounds = (await target.boundingBox())!
  const checksBefore = fixture.sessionReads()
  fixture.delaySession()
  await page.evaluate(() => window.dispatchEvent(new Event('focus')))
  await expect.poll(fixture.sessionReads).toBeGreaterThan(checksBefore)
  try {
    await page.mouse.click(bounds.x + bounds.width / 2, bounds.y + bounds.height / 2)
    await expect(page.getByRole('heading', { name: 'Patients', exact: true })).toBeVisible()
  } finally { fixture.releaseSession() }
  await expect(page.getByText(fixture.confirmedUser(), { exact: true })).toBeVisible()
  await expect(page).toHaveTitle('Patients — OncoMatch')
})

test('browser titles follow navigation, detail reloads, and session expiry', async ({ page }) => {
  await directory(page, 'patients')
  const fixture = await directory(page, 'studies')
  await page.goto('/')
  await expect(page).toHaveTitle('Dashboard — OncoMatch')
  await page.getByRole('link', { name: 'Patients', exact: true }).click()
  await expect(page).toHaveTitle('Patients — OncoMatch')
  await page.getByRole('link', { name: 'P001', exact: true }).click()
  await expect(page).toHaveTitle('Patient P001 — OncoMatch')
  await page.goBack()
  await expect(page).toHaveTitle('Patients — OncoMatch')
  await page.getByRole('link', { name: 'Studies', exact: true }).click()
  await expect(page).toHaveTitle('Studies — OncoMatch')
  await page.getByRole('link', { name: 'NCT00000001', exact: true }).click()
  await expect(page).toHaveTitle('Study NCT00000001 — OncoMatch')
  await page.reload()
  await expect(page).toHaveTitle('Study NCT00000001 — OncoMatch')
  fixture.expire()
  await page.evaluate(() => window.dispatchEvent(new Event('focus')))
  await expect(page.getByLabel('Username')).toBeVisible()
  await expect(page).toHaveTitle('Log in — OncoMatch')
})

for (const kind of ['patients', 'studies'] as const) {
  for (const view of ['list', 'details'] as const) {
    test(`a focus session check preserves the current scroll position on ${kind} ${view}`, async ({ page }) => {
      await page.setViewportSize({ width: 900, height: 350 })
      const fixture = await directory(page, kind)
      const path = `/${kind}` + (view === 'details' ? kind === 'patients' ? '/P001' : '/NCT00000001' : '')
      const heading = kind === 'patients' ? view === 'list' ? 'Patients' : 'Patient P001' : view === 'list' ? 'Studies' : 'Study NCT00000001'
      await page.goto(path)
      await expect(page.getByRole('heading', { name: heading, exact: true })).toBeVisible()
      await page.evaluate(() => window.scrollTo(0, 400))
      const previousY = await page.evaluate(() => window.scrollY)
      expect(previousY).toBeGreaterThan(100)
      const checksBefore = fixture.sessionReads()
      fixture.delaySession()
      await page.evaluate(() => window.dispatchEvent(new Event('focus')))
      await expect.poll(fixture.sessionReads).toBeGreaterThan(checksBefore)
      await expect(page.getByRole('navigation', { name: 'Main navigation' })).toBeVisible()
      fixture.releaseSession()
      await expect(page.getByText(fixture.confirmedUser(), { exact: true })).toBeVisible()
      await expect(page.getByRole('heading', { name: heading, exact: true })).toBeVisible()
      await expect.poll(() => page.evaluate(() => window.scrollY)).toBeCloseTo(previousY, 0)
    })
  }
}

test('repeated focus checks preserve the latest scroll rather than the last-opened record position', async ({ page }) => {
  await page.setViewportSize({ width: 900, height: 500 })
  const fixture = await directory(page, 'studies')
  await page.goto('/studies')
  const link = page.getByRole('link', { name: 'NCT00000020', exact: true })
  await link.scrollIntoViewIfNeeded()
  const recordY = await page.evaluate(() => window.scrollY)
  await link.click()
  await page.getByRole('link', { name: 'All studies', exact: true }).click()
  await expect(page.getByText('Last viewed', { exact: true })).toBeVisible()
  await page.evaluate(y => window.scrollTo(0, y + 800), recordY)
  const currentY = await page.evaluate(() => window.scrollY)
  expect(currentY).toBeGreaterThan(recordY + 100)
  const checksBefore = fixture.sessionReads()
  fixture.delaySession()
  await page.evaluate(() => window.dispatchEvent(new Event('focus')))
  await expect.poll(fixture.sessionReads).toBeGreaterThan(checksBefore)
  await expect(page.getByRole('navigation', { name: 'Main navigation' })).toBeVisible()
  // Repeated focus checks must preserve the current page and scroll position.
  await page.evaluate(() => window.dispatchEvent(new Event('focus')))
  await expect.poll(fixture.sessionReads).toBeGreaterThan(checksBefore + 1)
  fixture.releaseSession()
  await expect(page.getByText(fixture.confirmedUser(), { exact: true })).toBeVisible()
  await expect(page.getByText('Last viewed', { exact: true })).toBeVisible()
  await expect.poll(() => page.evaluate(() => window.scrollY)).toBeCloseTo(currentY, 0)
})

test('an expired focus check clears protected content and does not restore its old position after login', async ({ page }) => {
  await page.setViewportSize({ width: 900, height: 500 })
  const fixture = await directory(page, 'studies')
  await page.goto('/studies')
  await expect(page.getByRole('row')).toHaveCount(81)
  await page.evaluate(() => window.scrollTo(0, 1400))
  fixture.expire()
  await page.evaluate(() => window.dispatchEvent(new Event('focus')))
  await expect(page.getByLabel('Username')).toBeVisible()
  await expect(page.getByRole('table')).toHaveCount(0)
  await page.getByLabel('Username').fill('viewer')
  await page.getByLabel('Password', { exact: true }).fill('ViewerDemo1!')
  await page.getByRole('button', { name: 'Log in', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Studies', exact: true })).toBeVisible()
  await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(0)
})

test('returning to a loaded list during a background session check restores its position', async ({ page }) => {
  await page.setViewportSize({ width: 900, height: 500 })
  const fixture = await directory(page, 'studies')
  await page.goto('/studies')
  const link = page.getByRole('link', { name: 'NCT00000070', exact: true })
  await link.scrollIntoViewIfNeeded()
  const previousY = await page.evaluate(() => window.scrollY)
  const initialReads = fixture.reads()
  await link.click()
  await expect(page.getByRole('heading', { name: 'Study NCT00000070', exact: true })).toBeVisible()
  const checksBefore = fixture.sessionReads()
  fixture.delaySession()
  await page.evaluate(() => window.dispatchEvent(new Event('focus')))
  await expect.poll(fixture.sessionReads).toBeGreaterThan(checksBefore)
  await expect(page.getByRole('navigation', { name: 'Main navigation' })).toBeVisible()
  await page.goBack()
  await expect.poll(fixture.reads).toBeGreaterThan(initialReads)
  await expect(page.locator('tbody tr')).toHaveCount(80)
  fixture.releaseSession()
  await expect(page.getByText(fixture.confirmedUser(), { exact: true })).toBeVisible()
  await expect(page.getByText('Last viewed', { exact: true })).toBeVisible()
  await expect.poll(() => page.evaluate(() => window.scrollY)).toBeCloseTo(previousY, 0)
})

for (const kind of ['patients', 'studies'] as const) {
  for (const returnBy of ['list link', 'browser Back'] as const) {
    test(`${kind} restore position and mark the last viewed record using ${returnBy}`, async ({ page }) => {
      await page.setViewportSize({ width: 900, height: 500 })
      const fixture = await directory(page, kind)
      const target = kind === 'patients' ? 'P008' : 'NCT00000070'
      await page.goto(`/${kind}`)
      const link = page.getByRole('link', { name: target, exact: true })
      await link.scrollIntoViewIfNeeded()
      const previousY = await page.evaluate(() => window.scrollY)
      const initialReads = fixture.reads()
      expect(previousY).toBeGreaterThan(100)
      await link.click()
      await expect(page.getByRole('heading', { name: `${kind === 'patients' ? 'Patient' : 'Study'} ${target}`, exact: true })).toBeVisible()
      await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(0)
      fixture.delay()
      if (returnBy === 'list link') await page.getByRole('link', { name: `All ${kind}`, exact: true }).click()
      else await page.goBack()
      await expect(page.getByText(`Loading ${kind}…`, { exact: true })).toBeVisible()
      fixture.release()
      const row = page.getByRole('row').filter({ has: page.getByRole('link', { name: target, exact: true }) })
      await expect(row.getByText('Last viewed', { exact: true })).toBeVisible()
      await expect.poll(() => page.evaluate(() => window.scrollY)).toBeCloseTo(previousY, 0)
      const background = await row.evaluate(element => getComputedStyle(element).backgroundColor)
      const otherBackground = await page.getByRole('row').nth(1).evaluate(element => getComputedStyle(element).backgroundColor)
      expect(background).not.toBe(otherBackground)
      if (returnBy === 'list link') await page.screenshot({ path: `test-results/${kind}-last-viewed.png`, fullPage: false })
      expect(fixture.reads()).toBeGreaterThan(initialReads)
    })
  }
}

test('each list keeps its own last-viewed record, changes the marker, and clears it after logout', async ({ page }) => {
  await directory(page, 'patients')
  await directory(page, 'studies')
  await page.goto('/patients')
  await page.getByRole('link', { name: 'P008', exact: true }).click()
  await page.getByRole('link', { name: 'All patients', exact: true }).click()
  const patientRow = (id: string) => page.getByRole('row').filter({ has: page.getByRole('link', { name: id, exact: true }) })
  await expect(patientRow('P008').getByText('Last viewed', { exact: true })).toBeVisible()
  await page.getByRole('link', { name: 'P009', exact: true }).click()
  await page.getByRole('link', { name: 'All patients', exact: true }).click()
  await expect(patientRow('P009').getByText('Last viewed', { exact: true })).toBeVisible()
  await expect(patientRow('P008').getByText('Last viewed', { exact: true })).toHaveCount(0)
  await expect(page.getByText('Last viewed', { exact: true })).toHaveCount(1)
  await page.getByRole('link', { name: 'Studies', exact: true }).click()
  await expect(page.getByText('Last viewed', { exact: true })).toHaveCount(0)
  await page.getByRole('link', { name: 'NCT00000070', exact: true }).click()
  await page.getByRole('link', { name: 'All studies', exact: true }).click()
  await expect(patientRow('NCT00000070').getByText('Last viewed', { exact: true })).toBeVisible()
  await page.getByRole('link', { name: 'Patients', exact: true }).click()
  await expect(patientRow('P009').getByText('Last viewed', { exact: true })).toBeVisible()
  await page.getByRole('button', { name: 'Log out', exact: true }).click()
  await page.getByLabel('Username').fill('viewer')
  await page.getByLabel('Password', { exact: true }).fill('ViewerDemo1!')
  await page.getByRole('button', { name: 'Log in', exact: true }).click()
  await page.getByRole('link', { name: 'Patients', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Patients', exact: true })).toBeVisible()
  await expect(page.getByText('Last viewed', { exact: true })).toHaveCount(0)
  await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(0)
  await page.getByRole('link', { name: 'Studies', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Studies', exact: true })).toBeVisible()
  await expect(page.getByText('Last viewed', { exact: true })).toHaveCount(0)
  await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(0)
})
