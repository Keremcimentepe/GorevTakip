import { test as base, expect } from '@playwright/test'
import { AppPage } from './app-page'
import { DEFAULT_EMAIL, FakeApi } from './fake-api'

interface Fixtures {
  api: FakeApi
  app: AppPage
  seedSession: void
}

interface Options {
  /** false: test giriş yapmamış bir ziyaretçiyle başlar (giriş ekranı testleri için). Varsayılan true. */
  signedIn: boolean
}

// Tarayıcıdaki oturum kaydı (src/api/session.ts → STORAGE_KEY)
export const SESSION_KEY = 'gorevtakip.session'

// Fixture: testlerin ihtiyaç duyduğu hazır nesneleri her teste otomatik verir.
// Her test kendi sahte API'sini (temiz bir "veritabanıyla") ve kendi sayfasını alır;
// testler birbirini etkilemediği için paralel koşabilir.
export const test = base.extend<Fixtures & Options>({
  signedIn: [true, { option: true }],

  api: [
    // İkinci parametrenin adı Playwright dokümanlarında "use" geçer; React'in "use" kancasıyla
    // karışıp lint uyarısı vermesin diye burada "provide" dedik. İşlevi aynı.
    async ({ page }, provide) => {
      const api = new FakeApi()
      await api.install(page) // sayfa açılmadan ÖNCE hazır olmalı
      await provide(api)
    },
    { auto: true }, // teste yazılmasa bile her testte çalışır
  ],

  // Varsayılan olarak her test giriş yapmış bir kullanıcıyla başlar; böylece proje/pano testleri
  // giriş adımlarını tekrarlamaz. Oturum, sayfa açılmadan önce localStorage'a yazılır.
  seedSession: [
    async ({ page, api, signedIn }, provide) => {
      if (signedIn) {
        const session = api.signIn(DEFAULT_EMAIL)
        // Bu betik sayfanın her yüklenişinde çalışır; "bu sekmede bir kez" kuralı olmazsa
        // çıkış yapıp sayfayı yenilediğimizde oturumu geri yazar ve çıkışı sınayamayız.
        await page.addInitScript(
          ({ key, value }) => {
            if (sessionStorage.getItem('e2e-seeded')) return
            sessionStorage.setItem('e2e-seeded', '1')
            localStorage.setItem(key, value)
          },
          { key: SESSION_KEY, value: JSON.stringify(session) },
        )
      }
      await provide()
    },
    { auto: true },
  ],

  app: async ({ page }, provide) => {
    await provide(new AppPage(page))
  },
})

export { expect }
