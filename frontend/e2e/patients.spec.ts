import { test, expect } from '@playwright/test'

for (const account of [
  { username: 'viewer', password: 'ViewerDemo!1', role: 'Viewer' },
  { username: 'operator', password: 'OperatorDemo!1', role: 'Operator' },
]) {
  test(`${account.role} can login, browse, refresh, and logout`, async ({ page, context }) => {
    await page.goto('/')
    await expect(page.getByLabel('Username')).toBeVisible()
    if (account.role === 'Viewer') await page.screenshot({ path: 'test-results/viewer-login.png', fullPage: true })
    await page.getByLabel('Username').fill(account.username)
    await page.getByLabel('Password', { exact: true }).fill(account.password)
    await page.getByRole('button', { name: 'Log in', exact: true }).click()
    await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()
    await expect(page.getByText(`${account.username} · ${account.role}`)).toBeVisible()
    if (account.role === 'Viewer') await page.screenshot({ path: 'test-results/viewer-dashboard.png', fullPage: true })
    await page.getByRole('link', { name: 'Browse patients' }).click()
    await expect(page.getByRole('row')).toHaveCount(11)
    if (account.role === 'Viewer') await page.screenshot({ path: 'test-results/viewer-patients.png', fullPage: true })
    await page.getByRole('link', { name: 'P005', exact: true }).click()
    await expect(page.getByRole('heading', { name: 'Patient P005' })).toBeVisible()
    await expect(page.getByText('Breast cancer', { exact: true })).toBeVisible()
    if (account.role === 'Viewer') {
      await page.setViewportSize({ width: 390, height: 844 })
      await page.screenshot({ path: 'test-results/viewer-patient-mobile.png', fullPage: true })
      await page.setViewportSize({ width: 1280, height: 720 })
    }
    await page.reload()
    await expect(page.getByRole('heading', { name: 'Patient P005' })).toBeVisible()

    const authCookie = (await context.cookies()).find(cookie => cookie.name === 'OncoMatch.Auth')
    expect(authCookie?.secure).toBe(true)
    expect(authCookie?.httpOnly).toBe(true)
    await page.getByRole('button', { name: 'Log out', exact: true }).click()
    await expect(page.getByRole('heading', { name: 'Log in', exact: true })).toBeVisible()
    await page.goto('/patients/P005')
    await expect(page.getByLabel('Username')).toBeVisible()
    expect((await page.request.get('/api/patients')).status()).toBe(401)
  })
}

test('a protected deep link survives login and another tab detects logout on focus', async ({ page, context }) => {
  await page.goto('/patients/P003')
  await page.getByLabel('Username').fill('viewer')
  await page.getByLabel('Password', { exact: true }).fill('ViewerDemo!1')
  await page.getByRole('button', { name: 'Log in', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Patient P003' })).toBeVisible()
  const second = await context.newPage()
  await second.goto('/patients/P003')
  await expect(second.getByRole('heading', { name: 'Patient P003' })).toBeVisible()
  await page.bringToFront()
  await page.getByRole('button', { name: 'Log out', exact: true }).click()
  await expect(page.getByLabel('Username')).toBeVisible()
  await second.bringToFront()
  await second.evaluate(() => window.dispatchEvent(new Event('focus')))
  await expect(second.getByLabel('Username')).toBeVisible()
})
