// Her test dosyasından önce otomatik çalışır (vite.config.ts → test.setupFiles).

// toBeInTheDocument(), toBeDisabled(), toHaveValue() gibi okunaklı DOM doğrulamalarını ekler
import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'

// Vitest'te "globals" kapalı olduğundan React Testing Library kendiliğinden temizlik yapamaz.
// Her testten sonra ekrana çizilen bileşenleri kaldırıyoruz ki testler birbirini etkilemesin.
afterEach(() => {
  cleanup()
  // Oturum (token) localStorage'da durur; bir testin girişi sonrakine sızmasın
  localStorage.clear()
})
