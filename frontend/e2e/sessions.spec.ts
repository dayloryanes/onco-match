import { test, expect } from '@playwright/test'
import type { Page } from '@playwright/test'

async function login(page: Page) {
  await page.goto('/')
  await page.getByLabel('Username').fill('viewer')
  await page.getByLabel('Password', { exact: true }).fill('ViewerDemo1!')
  await page.getByRole('button', { name: 'Log in', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()
  await expect(page.locator('.session-controls time')).toBeVisible()
}

test('expiry warning offers explicit extension and retry without keepalive or leaving the page', async ({ page }) => {
  await page.clock.install()
  let sessionChecks = 0
  page.on('request', request => { if (request.url().endsWith('/api/auth/me')) sessionChecks++ })
  await login(page)
  const checksBefore = sessionChecks
  await page.clock.fastForward(25 * 60_000 + 1000)
  await expect(page.getByRole('button', { name: 'Stay signed in' })).toBeVisible()
  expect(sessionChecks).toBe(checksBefore)
  await page.route('**/api/auth/extend-session', route => route.abort())
  await page.getByRole('button', { name: 'Stay signed in' }).click()
  await expect(page.getByText('Unable to extend your session.', { exact: false })).toBeVisible()
  await page.screenshot({ path: 'test-results/session-warning.png', fullPage: true })
  const expiryBefore = await page.locator('.session-controls time').getAttribute('datetime')
  await page.unroute('**/api/auth/extend-session')
  await page.getByRole('button', { name: 'Retry session extension' }).click()
  await expect(page.locator('.session-warning')).toHaveCount(0)
  await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()
  expect(await page.locator('.session-controls time').getAttribute('datetime')).not.toBe(expiryBefore)
  await page.screenshot({ path: 'test-results/session-renewed.png', fullPage: true })
})

test('shared tabs observe renewal and logout while an independent login stays signed in', async ({ page, context, browser }) => {
  await login(page)
  const second = await context.newPage()
  await second.goto('/patients/P005')
  await expect(second.getByRole('heading', { name: 'Patient P005' })).toBeVisible()
  const independent = await browser.newContext({ ignoreHTTPSErrors: true })
  try {
    const third = await independent.newPage()
    await login(third)
    const independentExpiry = await third.locator('.session-controls time').getAttribute('datetime')
    const previous = await second.locator('.session-controls time').getAttribute('datetime')
    // Ticket times are stored with whole-second precision. Wait on observable
    // server time rather than assuming an immediate extension changes that value.
    await expect.poll(async () => {
      const response = await page.request.get('/api/auth/me')
      return Date.parse((await response.json()).serverTime)
    }).toBeGreaterThan(Date.parse(previous!) - 30 * 60_000 + 1000)
    await page.clock.install()
    await page.clock.fastForward(25 * 60_000 + 1000)
    await page.getByRole('button', { name: 'Stay signed in' }).click()
    await expect(second.locator('.session-controls time')).not.toHaveAttribute('datetime', previous!)
    await expect(second.getByRole('heading', { name: 'Patient P005' })).toBeVisible()
    expect(await third.locator('.session-controls time').getAttribute('datetime')).toBe(independentExpiry)
    await page.getByRole('button', { name: 'Log out', exact: true }).click()
    await expect(second.getByLabel('Username')).toBeVisible()
    await third.evaluate(() => window.dispatchEvent(new Event('focus')))
    await expect(third.getByRole('heading', { name: 'Dashboard' })).toBeVisible()
    expect((await third.request.get('/api/patients')).status()).toBe(200)
  } finally { await independent.close() }
})
