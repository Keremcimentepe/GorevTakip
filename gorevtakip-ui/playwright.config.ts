import { defineConfig, devices } from '@playwright/test'

// Uçtan uca testler gerçek bir tarayıcıda (Chromium) koşar.
// API'yi ayağa kaldırmaya gerek yok: e2e/fake-api.ts tarayıcının "/api/..." isteklerini yakalayıp
// bellekteki sahte bir backend'e cevaplatır.
const PORT = 5199 // geliştirme sunucunla (5173) çakışmasın diye ayrı port

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI, // CI'da unutulmuş test.only varsa hata ver
  retries: process.env.CI ? 1 : 0,
  reporter: [['list'], ['html', { open: 'never' }]],

  use: {
    baseURL: `http://localhost:${PORT}`,
    trace: 'on-first-retry', // test ilk kez başarısız olursa ayrıntılı kayıt (trace) al
  },

  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],

  // Testler başlamadan önce Vite geliştirme sunucusunu başlatır (zaten çalışıyorsa onu kullanır).
  webServer: {
    command: `npm run dev -- --port ${PORT} --strictPort`,
    url: `http://localhost:${PORT}`,
    reuseExistingServer: !process.env.CI,
    // Arayüz API'yi aynı adresin "/api" yolundan çağırır; testte bu istekleri Playwright yakalar.
    // (Ortam değişkeni .env dosyasındaki değerden önceliklidir.)
    env: { VITE_API_URL: `http://localhost:${PORT}/api` },
  },
})
