import { expect, test } from './fixtures'

test.describe('Dar ekran (telefon)', () => {
  test.use({ viewport: { width: 390, height: 800 } })

  test('yatay kaydırma çıkmaz ve kolonlar alt alta dizilir', async ({ app, api, page }) => {
    const web = api.addProject('Web Sitesi')
    api.addTask(web.id, 'Mobilde dene')
    await app.goto()

    await app.openProject('Web Sitesi')
    await expect(app.card('Yapılacak', 'Mobilde dene')).toBeVisible()

    const overflows = await page.evaluate(
      () => document.documentElement.scrollWidth > window.innerWidth,
    )
    expect(overflows).toBe(false)

    const first = await app.column('Yapılacak').boundingBox()
    const second = await app.column('Devam ediyor').boundingBox()
    expect(first).not.toBeNull()
    expect(second).not.toBeNull()
    // Aynı hizada (yan yana) değil, ikincisi birincinin altında
    expect(second!.y).toBeGreaterThan(first!.y + first!.height - 1)
    expect(Math.abs(second!.x - first!.x)).toBeLessThan(2)
  })
})
