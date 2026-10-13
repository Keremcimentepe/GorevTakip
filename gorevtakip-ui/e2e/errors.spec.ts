import { expect, test } from './fixtures'
import { SERVER_ERROR_BODY } from './fake-api'

const TODO = 'Yapılacak'
const DOING = 'Devam ediyor'

// Mutlu yol kolay; asıl kalite, işler ters gidince arayüzün ne yaptığında anlaşılır.
test.describe('Hata durumları', () => {
  test('400: sunucunun alan hataları birleştirilip gösterilir, yazılan metin silinmez', async ({ app, api }) => {
    api.addProject('Web Sitesi')
    api.failNext('POST', /\/tasks$/, 400, {
      title: 'One or more validation errors occurred.',
      status: 400,
      errors: {
        Title: ['Görev başlığı boş olamaz.'],
        Description: ['Açıklama en fazla 1000 karakter olabilir.'],
      },
    })
    await app.goto()
    await app.openProject('Web Sitesi')

    await app.addTask('Sorunlu görev')

    await expect(app.alert).toHaveText('Görev başlığı boş olamaz. Açıklama en fazla 1000 karakter olabilir.')
    await expect(app.taskTitleInput).toHaveValue('Sorunlu görev')
  })

  test('500: taşıma başarısız olursa kart eski kolonuna döner ve hata görünür', async ({ app, api }) => {
    const web = api.addProject('Web Sitesi')
    const task = api.addTask(web.id, 'API bağla')
    api.failNext('PATCH', /\/status$/, 500, SERVER_ERROR_BODY)
    await app.goto()
    await app.openProject('Web Sitesi')

    await app.moveWithButton(TODO, 'API bağla', DOING)

    // İyimser güncelleme geri alınmalı: kart yine "Yapılacak"ta
    await expect(app.alert).toHaveText('Beklenmeyen bir hata oluştu.')
    await expect(app.card(TODO, 'API bağla')).toBeVisible()
    await expect(app.card(DOING, 'API bağla')).toHaveCount(0)
    expect(task.status).toBe('Todo')
  })

  test('hata sonrası bir sonraki başarılı işlem hata mesajını temizler', async ({ app, api }) => {
    const web = api.addProject('Web Sitesi')
    api.addTask(web.id, 'API bağla')
    api.failNext('PATCH', /\/status$/, 500, SERVER_ERROR_BODY)
    await app.goto()
    await app.openProject('Web Sitesi')

    await app.moveWithButton(TODO, 'API bağla', DOING) // bu başarısız olur
    await expect(app.alert).toBeVisible()

    await app.moveWithButton(TODO, 'API bağla', DOING) // bu başarılı olur

    await expect(app.card(DOING, 'API bağla')).toBeVisible()
    await expect(app.alert).toHaveCount(0)
  })

  test('açılan proje sunucuda artık yoksa "Kayıt bulunamadı." gösterilir', async ({ app, api }) => {
    api.addProject('Hayalet Proje')
    await app.goto()
    await expect(app.projectButton('Hayalet Proje')).toBeVisible()

    api.projects = [] // başka bir sekmeden silinmiş gibi
    await app.openProject('Hayalet Proje')

    await expect(app.alert).toHaveText('Kayıt bulunamadı.')
  })

  test('sunucuya hiç ulaşılamazsa anlaşılır bir mesaj gösterilir', async ({ app, api }) => {
    api.addProject('Web Sitesi')
    await app.goto()
    await app.openProject('Web Sitesi')
    // Pano yüklenmeden sunucuyu kapatırsak panonun kendi yükleme isteği düşer ve form hiç çizilmez
    await expect(app.boardTitle('Web Sitesi')).toBeVisible()

    api.goOffline()
    await app.addTask('Sunucu kapalı')

    await expect(app.alert).toContainText("API'ye ulaşılamadı")
  })
})
