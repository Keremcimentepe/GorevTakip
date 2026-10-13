import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config' // vite'ın defineConfig'i + `test` ayarları

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  test: {
    // Bileşenler tarayıcı olmadan, jsdom adlı sahte bir DOM içinde sınanır
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    // Yalnızca birim/bileşen testleri. e2e/ klasörü Playwright'ın (*.spec.ts), karışmasın.
    include: ['src/**/*.test.{ts,tsx}'],
  },
})
