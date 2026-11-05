import { test, expect } from '@playwright/test'

test('Hyphenated condition continuations use remaining space in automatic table columns', async ({ page }) => {
  const letters = 'Abcdefghijklmnopqrstuvwxyz';
  const conditions = [11, 15, 19, 25].flatMap(length => [4, 6, 8].map(next => `${letters.slice(0, length)} ${letters.slice(0, next)}`))
  await page.route('**/api/**', route => route.fulfill({ json: new URL(route.request().url()).pathname === '/api/auth/me'
    ? { username: 'viewer', role: 'Viewer' }
    : conditions.map((condition, index) => ({ nctId: `NCT0000000${index + 1}`,
      title: 'A deliberately lengthy synthetic title with several ordinary words to exercise automatic column allocation across different available widths',
      recruitmentStatus: 'NOT_YET_RECRUITING', studyType: 'INTERVENTIONAL', conditions: [condition] })) }))
  await page.goto('/studies')
  await expect(page.getByText('Not yet recruiting').first()).toBeVisible()
  let opportunities = 0
  for (const width of [1440, 1280, 1100, 1024, 900, 768, 390]) {
    await page.setViewportSize({ width, height: 844 })
    await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))))
    const result = await page.locator('.condition-text').evaluateAll(items => {
      let opportunities = 0
      const failures: string[] = []
      const canvas = document.createElement('canvas').getContext('2d')!
      for (const item of items) {
        canvas.font = getComputedStyle(item).font
        const words = [...item.children]
        for (let i = 0; i < words.length - 1; i++) {
          const current = words[i], next = words[i + 1]
          const whole = document.createRange(); whole.selectNodeContents(current)
          const lines = new Set([...whole.getClientRects()].filter(rect => rect.width > 0).map(rect => Math.round(rect.top)))
          if (lines.size < 2) continue
          const last = document.createRange(), first = document.createRange()
          last.setStart(current.firstChild!, current.textContent!.length - 1); last.setEnd(current.firstChild!, current.textContent!.length)
          first.setStart(next.firstChild!, 0); first.setEnd(next.firstChild!, 1)
          const tail = last.getBoundingClientRect(), head = first.getBoundingClientRect()
          const needed = canvas.measureText(' ' + next.textContent!.replaceAll('\u00ad', '')).width
          if (item.getBoundingClientRect().right - tail.right >= needed + 1) {
            opportunities++
            if (Math.abs(tail.top - head.top) > 1) failures.push(item.textContent!.replaceAll('\u00ad', ''))
          }
        }
      }
      return { opportunities, failures }
    })
    opportunities += result.opportunities
    expect(result.failures, `Automatic columns at ${width}px`).toEqual([])
  }
  expect(opportunities).toBeGreaterThan(0)
})

for (const width of [1280, 390]) {
  test(`Conditions keep whole words and hyphenate overlong words without widening the table at ${width}px`, async ({ page }) => {
    const pageErrors: string[] = []
    page.on('pageerror', error => pageErrors.push(error.message))
    let long = false
    const longWord = 'Abcdefghijklmnopqrstuvwxyz'.repeat(3)
    await page.setViewportSize({ width, height: 844 })
    await page.route('**/api/**', async route => {
      const path = new URL(route.request().url()).pathname
      await route.fulfill({ json: path === '/api/auth/me' ? { username: 'viewer', role: 'Viewer' } : [
        { nctId: 'NCT00000001', title: 'Original study title unchanged', recruitmentStatus: 'NOT_YET_RECRUITING', studyType: 'INTERVENTIONAL', conditions: ['Ordinary Words'] },
        { nctId: 'NCT00000002', title: 'Second study title', recruitmentStatus: 'RECRUITING', studyType: 'OBSERVATIONAL', conditions: [long ? longWord : 'Short'] },
        { nctId: 'NCT00000003', title: 'Continuation example', recruitmentStatus: 'RECRUITING', studyType: 'OBSERVATIONAL', conditions: ['Continuationword Follow'] },
      ] })
    })
    await page.goto('/studies')
    await expect(page.getByText('Original study title unchanged')).toBeVisible()
    // A narrow content area exercises the exact whole-word/overlong-word
    // distinction, independently of the viewport's automatic column allocation.
    await page.addStyleTag({ content: '.study-conditions > ul { inline-size: 12ch; max-inline-size: 100%; }' })
    const baselineWidth = await page.getByRole('table').evaluate(table => table.scrollWidth)
    const first = page.getByRole('row').filter({ hasText: 'NCT00000001' }).locator('.study-conditions')
    const normal = await first.locator('.condition-word').evaluateAll(words => words.map(word => {
      const range = document.createRange(); range.selectNodeContents(word)
      return { text: word.textContent?.replaceAll('\u00ad', ''), top: word.getBoundingClientRect().top,
        lines: new Set([...range.getClientRects()].filter(rect => rect.width > 0).map(rect => Math.round(rect.top))).size }
    }))
    expect(normal.map(word => word.text)).toEqual(['Ordinary', 'Words'])
    expect(normal[1].top).toBeGreaterThan(normal[0].top)
    normal.forEach(word => expect(word.lines).toBe(1))
    const continuation = page.getByRole('row').filter({ hasText: 'NCT00000003' }).locator('.study-conditions')
    await continuation.locator('ul').evaluate(list => { list.style.inlineSize = '14ch' })
    await expect.poll(() => continuation.locator('.condition-word').evaluateAll(words => {
      const last = document.createRange()
      const text = words[0].firstChild!
      last.setStart(text, text.textContent!.length - 1); last.setEnd(text, text.textContent!.length)
      const first = document.createRange(); first.setStart(words[1].firstChild!, 0); first.setEnd(words[1].firstChild!, 1)
      return Math.abs(last.getBoundingClientRect().top - first.getBoundingClientRect().top)
    })).toBeLessThan(1)
    const copied = await continuation.evaluate(cell => {
      const range = document.createRange(); range.selectNodeContents(cell.querySelector('.condition-text')!)
      const selection = window.getSelection()!; selection.removeAllRanges(); selection.addRange(range)
      const clipboard = new DataTransfer()
      cell.dispatchEvent(new ClipboardEvent('copy', { bubbles: true, cancelable: true, clipboardData: clipboard }))
      selection.removeAllRanges()
      return clipboard.getData('text/plain')
    })
    expect(copied).toBe('Continuationword Follow')
    long = true
    await page.getByRole('button', { name: 'Refresh', exact: true }).click()
    const last = page.getByRole('row').filter({ hasText: 'NCT00000002' }).locator('.study-conditions')
    await expect(last.locator('.condition-word')).toHaveAttribute('aria-label', longWord)
    const wrapped = await last.locator('.condition-word').evaluate(word => {
      const text = word.firstChild!
      const hyphens = []
      for (let i = 0; i < (text.textContent?.length ?? 0); i++) {
        if (text.textContent![i] !== '\u00ad') continue
        const range = document.createRange()
        range.setStart(text, i); range.setEnd(text, i + 1)
        if ([...range.getClientRects()].some(rect => rect.width > 1)) hyphens.push(i)
      }
      const range = document.createRange(); range.selectNodeContents(word)
      return { lines: new Set([...range.getClientRects()].filter(rect => rect.width > 0).map(rect => Math.round(rect.top))).size,
        hyphens, text: word.textContent?.replaceAll('\u00ad', ''),
        overflow: word.scrollWidth > word.clientWidth }
    })
    expect(wrapped.text).toBe(longWord)
    expect(wrapped.lines).toBeGreaterThan(1)
    expect(wrapped.hyphens.length).toBeGreaterThan(0)
    expect(wrapped.overflow).toBe(false)
    expect(await page.getByRole('table').evaluate(table => table.scrollWidth)).toBeLessThanOrEqual(baselineWidth + 1)
    await expect(page.getByText('Original study title unchanged')).toBeVisible()
    await continuation.locator('ul').evaluate(list => { list.style.inlineSize = '14ch' })
    expect(pageErrors).toEqual([])
    await page.screenshot({ path: `test-results/study-conditions-${width}.png`, fullPage: true })
  })
}
