import type { Page } from '@playwright/test'
import { DEFAULT_EMAIL, DEFAULT_PASSWORD } from './fake-api'
import { expect, SESSION_KEY, test } from './fixtures'

// Bu dosyadaki testler giriş yapmamış bir ziyaretçiyle başlar (diğer dosyalarda varsayılan: girişli).
test.use({ signedIn: false })

// Tarayıcının localStorage'ındaki ham oturum kaydı (yoksa null)
const storedSession = (page: Page) => page.evaluate((key) => localStorage.getItem(key), SESSION_KEY)

test.describe('Giriş ve kayıt', () => {
  test('giriş yapılmamışsa giriş formu görünür ve API\'den veri istenmez', async ({ app, api, page }) => {
    await app.goto()

    await expect(app.loginHeading).toBeVisible()
    await expect(page.getByLabel('Yeni proje adı')).toHaveCount(0)
    expect(api.requests).toEqual([]) // token yokken korumalı uç noktalara gidilmez
  })

  test('doğru bilgilerle girince kullanıcının projeleri açılır', async ({ app, api }) => {
    api.addUser(DEFAULT_EMAIL, DEFAULT_PASSWORD)
    api.addProject('Web Sitesi')
    await app.goto()

    await app.login(DEFAULT_EMAIL, DEFAULT_PASSWORD)

    await expect(app.projectItem('Web Sitesi')).toBeVisible()
    await expect(app.accountEmail(DEFAULT_EMAIL)).toBeVisible()
    await expect(app.loginHeading).toHaveCount(0)
  })

  test('yanlış şifre: hata gösterilir, giriş formu kalır, oturum kaydedilmez', async ({ app, api, page }) => {
    api.addUser(DEFAULT_EMAIL, DEFAULT_PASSWORD)
    await app.goto()

    await app.login(DEFAULT_EMAIL, 'YanlisSifre99')

    await expect(app.alert).toHaveText('E-posta veya şifre hatalı.')
    await expect(app.loginHeading).toBeVisible()
    expect(await storedSession(page)).toBeNull()
  })

  test('yanlış şifreden sonra doğrusunu yazınca girilir ve hata kalkar', async ({ app, api }) => {
    api.addUser(DEFAULT_EMAIL, DEFAULT_PASSWORD)
    await app.goto()
    await app.login(DEFAULT_EMAIL, 'YanlisSifre99')
    await expect(app.alert).toBeVisible()

    await app.passwordInput.fill(DEFAULT_PASSWORD)
    await app.loginButton.click()

    await expect(app.accountEmail(DEFAULT_EMAIL)).toBeVisible()
    await expect(app.alert).toHaveCount(0)
  })

  test('giriş ile kayıt formu arasında geçince eski hata mesajı kalkar', async ({ app, api, page }) => {
    api.addUser(DEFAULT_EMAIL, DEFAULT_PASSWORD)
    await app.goto()
    await app.login(DEFAULT_EMAIL, 'YanlisSifre99')
    await expect(app.alert).toBeVisible()

    await page.getByRole('button', { name: 'Hesabın yok mu? Kayıt ol' }).click()

    await expect(page.getByRole('heading', { level: 1, name: 'Hesap oluştur' })).toBeVisible()
    await expect(app.alert).toHaveCount(0)
    await expect(app.emailInput).toHaveValue(DEFAULT_EMAIL) // yazılan e-posta korunur
  })

  test('yeni hesap oluşturunca doğrudan içeri girilir; liste boştur', async ({ app, page }) => {
    await app.goto()

    await app.register('yeni@ornek.com', 'Sifre1234!')

    await expect(app.accountEmail('yeni@ornek.com')).toBeVisible()
    await expect(page.getByText('Henüz proje yok.')).toBeVisible()
  })

  test('kısa şifre: sunucunun doğrulama mesajı gösterilir', async ({ app }) => {
    await app.goto()

    await app.register('yeni@ornek.com', 'kisa')

    await expect(app.alert).toHaveText('Şifre en az 8 karakter olmalı.')
    await expect(app.registerButton).toBeVisible() // kayıt formunda kalır
  })

  test('geçersiz e-posta: sunucunun doğrulama mesajı gösterilir', async ({ app }) => {
    await app.goto()

    await app.register('eposta-degil', 'Sifre1234!')

    await expect(app.alert).toHaveText('Geçerli bir e-posta adresi girin.')
  })

  test('zaten kayıtlı e-postayla kayıt: 409 mesajı gösterilir', async ({ app, api }) => {
    api.addUser(DEFAULT_EMAIL, DEFAULT_PASSWORD)
    await app.goto()

    await app.register(DEFAULT_EMAIL, 'BaskaSifre99')

    await expect(app.alert).toHaveText('Bu e-posta adresiyle zaten bir hesap var.')
  })

  test('çok fazla deneme (429): anlaşılır bir mesaj gösterilir', async ({ app, api }) => {
    api.failNext('POST', /\/auth\/login$/, 429)
    await app.goto()

    await app.login(DEFAULT_EMAIL, DEFAULT_PASSWORD)

    await expect(app.alert).toHaveText('Çok fazla deneme yaptın. Biraz bekleyip tekrar dene.')
  })

  test('sunucuya ulaşılamazsa giriş formunda anlaşılır hata görünür', async ({ app, api }) => {
    api.goOffline()
    await app.goto()

    await app.login(DEFAULT_EMAIL, DEFAULT_PASSWORD)

    await expect(app.alert).toContainText("API'ye ulaşılamadı")
    await expect(app.loginHeading).toBeVisible()
  })

  test('alanlar boşken giriş butonu kapalıdır', async ({ app }) => {
    await app.goto()

    await expect(app.loginButton).toBeDisabled()
    await app.emailInput.fill('ali@ornek.com')
    await expect(app.loginButton).toBeDisabled()
    await app.passwordInput.fill('x')
    await expect(app.loginButton).toBeEnabled()
  })
})

test.describe('Oturum', () => {
  test('sayfayı yenileyince oturum sürer', async ({ app, api, page }) => {
    api.addUser(DEFAULT_EMAIL, DEFAULT_PASSWORD)
    api.addProject('Web Sitesi')
    await app.goto()
    await app.login(DEFAULT_EMAIL, DEFAULT_PASSWORD)
    await expect(app.projectItem('Web Sitesi')).toBeVisible()

    await page.reload()

    await expect(app.projectItem('Web Sitesi')).toBeVisible()
    await expect(app.loginHeading).toHaveCount(0)
  })

  test('çıkış yapınca giriş formu gelir, oturum silinir ve yenileyince de çıkışlı kalır', async ({ app, api, page }) => {
    api.addUser(DEFAULT_EMAIL, DEFAULT_PASSWORD)
    api.addProject('Web Sitesi')
    await app.goto()
    await app.login(DEFAULT_EMAIL, DEFAULT_PASSWORD)
    await expect(app.projectItem('Web Sitesi')).toBeVisible()

    await app.logoutButton.click()

    await expect(app.loginHeading).toBeVisible()
    await expect(app.projectItem('Web Sitesi')).toHaveCount(0)
    expect(await storedSession(page)).toBeNull()

    await page.reload()
    await expect(app.loginHeading).toBeVisible()
  })

  test('süresi dolmuş kayıtlı oturum kullanılmaz: giriş formu açılır, API\'ye gidilmez', async ({ app, api, page }) => {
    const session = api.signIn(DEFAULT_EMAIL)
    const expired = { ...session, expiresAt: new Date(Date.now() - 60_000).toISOString() }
    await page.addInitScript(
      ({ key, value }) => localStorage.setItem(key, value),
      { key: SESSION_KEY, value: JSON.stringify(expired) },
    )

    await app.goto()

    await expect(app.loginHeading).toBeVisible()
    expect(api.requests).toEqual([])
  })

  test('bozuk kayıtlı oturum uygulamayı çökertmez: giriş formu açılır', async ({ app, page }) => {
    await page.addInitScript((key) => localStorage.setItem(key, '{bozuk json'), SESSION_KEY)

    await app.goto()

    await expect(app.loginHeading).toBeVisible()
  })

  test('sunucu token\'ı reddederse giriş ekranına dönülür ve neden söylenir; yeniden girince devam edilir', async ({ app, api }) => {
    api.addUser(DEFAULT_EMAIL, DEFAULT_PASSWORD)
    api.addProject('Web Sitesi')
    await app.goto()
    await app.login(DEFAULT_EMAIL, DEFAULT_PASSWORD)
    await expect(app.projectItem('Web Sitesi')).toBeVisible()

    api.revokeTokens() // sunucu tarafında oturum bitti
    await app.addProject('Mobil Uygulama')

    await expect(app.loginHeading).toBeVisible()
    await expect(app.notice).toHaveText(
      'Oturumun süresi doldu. Devam etmek için yeniden giriş yap.',
    )

    await app.login(DEFAULT_EMAIL, DEFAULT_PASSWORD)

    await expect(app.projectItem('Web Sitesi')).toBeVisible()
    await expect(app.notice).toHaveCount(0)
  })

  test('yeniden girişten sonra başka sekmede çıkış yapılırsa eski "süre doldu" bilgisi geri gelmez', async ({ app, api, page, context }) => {
    api.addUser(DEFAULT_EMAIL, DEFAULT_PASSWORD)
    await app.goto()
    await app.login(DEFAULT_EMAIL, DEFAULT_PASSWORD)
    await expect(page.getByText('Henüz proje yok.')).toBeVisible() // ilk liste yüklendi
    api.revokeTokens()
    await app.addProject('Mobil Uygulama')
    await expect(app.notice).toBeVisible() // oturum bitti, bilgi gösterildi
    await app.login(DEFAULT_EMAIL, DEFAULT_PASSWORD) // yeniden girildi: bilgi kalkmalı
    await expect(app.accountEmail(DEFAULT_EMAIL)).toBeVisible()

    const second = await context.newPage()
    await api.install(second)
    await second.goto('/')
    await second.getByRole('button', { name: 'Çıkış yap' }).click()

    await expect(app.loginHeading).toBeVisible()
    await expect(app.notice).toHaveCount(0)
  })

  test('başka sekmede çıkış yapılırsa bu sekme de giriş formuna döner', async ({ app, api, context }) => {
    api.addUser(DEFAULT_EMAIL, DEFAULT_PASSWORD)
    api.addProject('Web Sitesi')
    await app.goto()
    await app.login(DEFAULT_EMAIL, DEFAULT_PASSWORD)
    await expect(app.projectItem('Web Sitesi')).toBeVisible()

    const second = await context.newPage()
    await api.install(second)
    await second.goto('/')
    await second.getByRole('button', { name: 'Çıkış yap' }).click()

    await expect(app.loginHeading).toBeVisible()
    await expect(app.notice).toHaveCount(0) // kullanıcı kendi çıkış yaptı: "süre doldu" denmez
  })
})

test.describe('Hesaplar birbirinden ayrı', () => {
  test('başka sekmede başka hesapla girilirse bu sekme yeni hesabın verisini gösterir, eskisini değil', async ({ app, api, page, context }) => {
    api.addUser(DEFAULT_EMAIL, DEFAULT_PASSWORD)
    api.addProject('Ayşe\'nin projesi', null, DEFAULT_EMAIL)
    api.addUser('mehmet@ornek.com')
    api.addProject('Mehmet\'in projesi', null, 'mehmet@ornek.com')
    await app.goto()
    await app.login(DEFAULT_EMAIL, DEFAULT_PASSWORD)
    await expect(app.projectItem('Ayşe\'nin projesi')).toBeVisible()

    // İkinci sekme aynı tarayıcı profilini paylaşır: orada hesap değişince localStorage de değişir
    const mehmet = api.signIn('mehmet@ornek.com')
    const second = await context.newPage()
    await second.goto('/')
    await second.evaluate(
      ({ key, value }) => localStorage.setItem(key, value),
      { key: SESSION_KEY, value: JSON.stringify(mehmet) },
    )

    await expect(app.accountEmail('mehmet@ornek.com')).toBeVisible()
    await expect(app.projectItem('Mehmet\'in projesi')).toBeVisible()
    await expect(app.projectItem('Ayşe\'nin projesi')).toHaveCount(0)
    await expect(page.getByText('Ayşe\'nin projesi')).toHaveCount(0)
  })


  test('ikinci hesap birinci hesabın projelerini görmez; birinciye dönünce kendi verisi durur', async ({ app, page }) => {
    await app.goto()
    await app.register('ayse@ornek.com', 'Sifre1234!')
    await app.addProject('Ayşe\'nin projesi')
    await expect(app.projectItem('Ayşe\'nin projesi')).toBeVisible()

    await app.logoutButton.click()
    await app.register('mehmet@ornek.com', 'Sifre1234!')

    await expect(app.accountEmail('mehmet@ornek.com')).toBeVisible()
    await expect(page.getByText('Henüz proje yok.')).toBeVisible()
    await expect(app.projectItem('Ayşe\'nin projesi')).toHaveCount(0)

    await app.logoutButton.click()
    await app.login('ayse@ornek.com', 'Sifre1234!')

    await expect(app.projectItem('Ayşe\'nin projesi')).toBeVisible()
  })
})

test.describe('Dar ekran (telefon)', () => {
  test.use({ viewport: { width: 360, height: 740 } })

  test('giriş formu yatay kaydırma oluşturmaz', async ({ app, page }) => {
    await app.goto()
    await expect(app.loginHeading).toBeVisible()

    const overflows = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth)

    expect(overflows).toBe(false)
  })
})
