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

async function waitForLaterTicketTime(page: Page) {
  const before = await (await page.request.get('/api/auth/me')).json()
  // Ticket timestamps have whole-second precision. Observe the real server
  // clock; the browser's accelerated countdown does not advance backend time.
  await expect.poll(async () => {
    const response = await page.request.get('/api/auth/me')
    return Date.parse((await response.json()).serverTime)
  }).toBeGreaterThan(Date.parse(before.serverTime) + 1000)
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
  await expect(page.getByText('Unable to extend your session with this request.', { exact: false })).toBeVisible()
  await page.screenshot({ path: 'test-results/session-warning.png', fullPage: true })
  const expiryBefore = await page.locator('.session-controls time').getAttribute('datetime')
  await page.unroute('**/api/auth/extend-session')
  await waitForLaterTicketTime(page)
  const extensionResponse = page.waitForResponse(response => response.url().endsWith('/api/auth/extend-session')
    && response.request().method() === 'POST' && response.ok())
  await page.getByRole('button', { name: 'Retry session extension' }).click()
  await extensionResponse
  await expect(page.locator('.session-extension-error')).toHaveCount(0)
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
    await waitForLaterTicketTime(page)
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
