import { useState } from 'react'
import type { FormEvent } from 'react'

interface Props {
  placeholder: string
  buttonLabel: string
  maxLength: number
  // true dönerse ekleme başarılı sayılır ve kutu temizlenir
  onSubmit: (value: string) => Promise<boolean>
}

// Hem proje hem görev eklemede kullanılan tek satırlık form.
export function AddForm({ placeholder, buttonLabel, maxLength, onSubmit }: Props) {
  const [value, setValue] = useState('')
  const [busy, setBusy] = useState(false)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault() // Sayfanın yeniden yüklenmesini engelle
    const trimmed = value.trim()
    if (!trimmed || busy) return

    setBusy(true)
    const ok = await onSubmit(trimmed)
    setBusy(false)
    if (ok) setValue('')
  }

  return (
    <form className="add-form" onSubmit={handleSubmit}>
      <input
        value={value}
        onChange={(e) => setValue(e.target.value)}
        placeholder={placeholder}
        aria-label={placeholder}
        maxLength={maxLength}
      />
      <button type="submit" disabled={busy || value.trim() === ''}>
        {buttonLabel}
      </button>
    </form>
  )
}
