import { expect, test } from './fixtures'

test.describe('Projeler', () => {
  test('hiç proje yokken boş durum mesajlarını gösterir', async ({ app, page }) => {
    await app.goto()

    await expect(page.getByText('Henüz proje yok.')).toBeVisible()
    await expect(page.getByText('Soldan bir proje seç ya da yeni bir proje oluştur.')).toBeVisible()
  })

  test('mevcut projeleri görev sayılarıyla listeler', async ({ app, api }) => {
    const web = api.addProject('Web Sitesi')
    api.addTask(web.id, 'Tasarımı çiz')
    api.addTask(web.id, 'API bağla')
    api.addProject('Mobil Uygulama')

    await app.goto()

    await expect(app.projectCount('Web Sitesi')).toHaveText('2')
    await expect(app.projectCount('Mobil Uygulama')).toHaveText('0')
  })

  test('ad boşken "Ekle" butonu kapalıdır, yazınca açılır', async ({ app }) => {
    await app.goto()

    await expect(app.addProjectButton).toBeDisabled()
    await app.projectNameInput.fill('   ')
    await expect(app.addProjectButton).toBeDisabled() // sadece boşluk da boş sayılır
    await app.projectNameInput.fill('Web Sitesi')
    await expect(app.addProjectButton).toBeEnabled()
  })

  test('yeni proje eklenir, otomatik açılır ve giriş kutusu temizlenir', async ({ app, api, page }) => {
    await app.goto()

    await app.addProject('Web Sitesi')

    await expect(app.boardTitle('Web Sitesi')).toBeVisible()
    await expect(app.projectNameInput).toHaveValue('')
    await expect(page.getByRole('region')).toHaveCount(3) // üç kolon
    await expect(app.projectButton('Web Sitesi')).toHaveAttribute('aria-current', 'true')
    expect(api.projects.map((p) => p.name)).toEqual(['Web Sitesi'])
  })

  test('sunucu proje eklemeyi reddederse hata gösterilir, yazılan ad silinmez', async ({ app, api }) => {
    api.failNext('POST', /^\/projects$/, 400, {
      title: 'One or more validation errors occurred.',
      status: 400,
      errors: { Name: ['Proje adı en fazla 100 karakter olabilir.'] },
    })
    await app.goto()

    await app.addProject('Web Sitesi')

    await expect(app.alert).toHaveText('Proje adı en fazla 100 karakter olabilir.')
    await expect(app.projectNameInput).toHaveValue('Web Sitesi')
  })

  test('silmeden önce onay ister; vazgeçilirse proje silinmez', async ({ app, api, page }) => {
    const web = api.addProject('Web Sitesi')
    api.addTask(web.id, 'Tasarımı çiz')
    await app.goto()

    let message = ''
    page.once('dialog', async (dialog) => {
      message = dialog.message()
      await dialog.dismiss()
    })
    await app.deleteProjectButton('Web Sitesi').click()

    expect(message).toBe('"Web Sitesi" projesi ve içindeki 1 görev silinsin mi?')
    await expect(app.projectButton('Web Sitesi')).toBeVisible()
    expect(api.projects).toHaveLength(1)
    expect(api.requests).not.toContain(`DELETE /projects/${web.id}`)
  })

  test('onaylanırsa proje ve görevleri silinir; açık proje silinince ana alan boşalır', async ({
    app,
    api,
    page,
  }) => {
    const web = api.addProject('Web Sitesi')
    api.addTask(web.id, 'Tasarımı çiz')
    await app.goto()
    await app.openProject('Web Sitesi')
    await expect(app.boardTitle('Web Sitesi')).toBeVisible()

    page.once('dialog', (dialog) => dialog.accept())
    await app.deleteProjectButton('Web Sitesi').click()

    await expect(page.getByText('Soldan bir proje seç ya da yeni bir proje oluştur.')).toBeVisible()
    await expect(page.getByText('Henüz proje yok.')).toBeVisible()
    expect(api.projects).toHaveLength(0)
    expect(api.tasks).toHaveLength(0) // görevler de gitti
  })
})
