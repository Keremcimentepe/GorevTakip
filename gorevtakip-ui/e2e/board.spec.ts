import { expect, test } from './fixtures'

const TODO = 'Yapılacak'
const DOING = 'Devam ediyor'
const DONE = 'Tamamlandı'

test.describe('Pano', () => {
  test('projeyi açınca görevleri durumlarına göre kolonlara dizer', async ({ app, api }) => {
    const web = api.addProject('Web Sitesi')
    api.addTask(web.id, 'Tasarımı çiz', 'Todo')
    api.addTask(web.id, 'API bağla', 'InProgress')
    api.addTask(web.id, 'Domain al', 'Done')
    await app.goto()

    await app.openProject('Web Sitesi')

    await expect(app.card(TODO, 'Tasarımı çiz')).toBeVisible()
    await expect(app.card(DOING, 'API bağla')).toBeVisible()
    await expect(app.card(DONE, 'Domain al')).toBeVisible()
  })

  test('boş kolonda "Buraya kart sürükle" yazar', async ({ app, api }) => {
    api.addProject('Web Sitesi')
    await app.goto()

    await app.openProject('Web Sitesi')

    await expect(app.column(DOING).getByText('Buraya kart sürükle')).toBeVisible()
  })

  test('görev ekler: "Yapılacak" kolonunda görünür, kutu temizlenir, sol paneldeki sayaç artar', async ({
    app,
    api,
  }) => {
    api.addProject('Web Sitesi')
    await app.goto()
    await app.openProject('Web Sitesi')

    await app.addTask('Tasarımı çiz')
    await app.addTask('API bağla')

    await expect(app.card(TODO, 'Tasarımı çiz')).toBeVisible()
    await expect(app.card(TODO, 'API bağla')).toBeVisible()
    await expect(app.taskTitleInput).toHaveValue('')
    await expect(app.projectCount('Web Sitesi')).toHaveText('2')
    expect(api.tasks.map((t) => t.title)).toEqual(['Tasarımı çiz', 'API bağla'])
  })

  test('→ ve ← butonları kartı komşu kolona taşır, durum sunucuya yazılır', async ({ app, api }) => {
    const web = api.addProject('Web Sitesi')
    const task = api.addTask(web.id, 'Tasarımı çiz')
    await app.goto()
    await app.openProject('Web Sitesi')

    await app.moveWithButton(TODO, 'Tasarımı çiz', DOING)
    await expect(app.card(DOING, 'Tasarımı çiz')).toBeVisible()
    await expect.poll(() => task.status).toBe('InProgress')

    await app.moveWithButton(DOING, 'Tasarımı çiz', DONE)
    await expect(app.card(DONE, 'Tasarımı çiz')).toBeVisible()
    await expect.poll(() => task.status).toBe('Done')

    await app.moveWithButton(DONE, 'Tasarımı çiz', DOING) // geri de taşınabilir
    await expect(app.card(DOING, 'Tasarımı çiz')).toBeVisible()
    await expect.poll(() => task.status).toBe('InProgress')
  })

  test('en soldaki kolonda ← butonu, en sağdakinde → butonu kapalıdır', async ({ app, api }) => {
    const web = api.addProject('Web Sitesi')
    api.addTask(web.id, 'Başlangıç', 'Todo')
    api.addTask(web.id, 'Bitiş', 'Done')
    await app.goto()
    await app.openProject('Web Sitesi')

    await expect(app.card(TODO, 'Başlangıç').getByRole('button', { name: 'Sola taşınamaz' })).toBeDisabled()
    await expect(app.card(DONE, 'Bitiş').getByRole('button', { name: 'Sağa taşınamaz' })).toBeDisabled()
  })

  // Sürükle-bırak, jsdom gibi sahte ortamlarda güvenilir sınanamaz; asıl değeri gerçek tarayıcıdadır.
  test('kartı sürükleyip başka kolona bırakınca taşınır', async ({ app, api }) => {
    const web = api.addProject('Web Sitesi')
    const task = api.addTask(web.id, 'Tasarımı çiz')
    await app.goto()
    await app.openProject('Web Sitesi')

    await app.dragTask(TODO, 'Tasarımı çiz', DONE)

    await expect(app.card(DONE, 'Tasarımı çiz')).toBeVisible()
    await expect(app.card(TODO, 'Tasarımı çiz')).toHaveCount(0)
    await expect.poll(() => task.status).toBe('Done')
  })

  test('kolon sayaçları kartlarla birlikte güncellenir', async ({ app, api }) => {
    const web = api.addProject('Web Sitesi')
    api.addTask(web.id, 'Bir')
    api.addTask(web.id, 'İki')
    await app.goto()
    await app.openProject('Web Sitesi')

    await expect(app.column(TODO).getByText(/^2$/)).toBeVisible()

    await app.moveWithButton(TODO, 'Bir', DOING)

    await expect(app.column(TODO).getByText(/^1$/)).toBeVisible()
    await expect(app.column(DOING).getByText(/^1$/)).toBeVisible()
    await expect(app.column(DONE).getByText(/^0$/)).toBeVisible()
  })

  test('görevi siler: kart kalkar, sayaç düşer, sunucudan da silinir', async ({ app, api }) => {
    const web = api.addProject('Web Sitesi')
    api.addTask(web.id, 'Tasarımı çiz')
    api.addTask(web.id, 'API bağla')
    await app.goto()
    await app.openProject('Web Sitesi')

    await app.deleteTask(TODO, 'Tasarımı çiz')

    await expect(app.card(TODO, 'Tasarımı çiz')).toHaveCount(0)
    await expect(app.card(TODO, 'API bağla')).toBeVisible()
    await expect(app.projectCount('Web Sitesi')).toHaveText('1')
    expect(api.tasks.map((t) => t.title)).toEqual(['API bağla'])
  })

  test('projeler arasında geçince her projenin kendi görevleri görünür', async ({ app, api }) => {
    const web = api.addProject('Web Sitesi')
    api.addTask(web.id, 'Tasarımı çiz')
    api.addProject('Mobil Uygulama')
    await app.goto()

    await app.openProject('Web Sitesi')
    await expect(app.card(TODO, 'Tasarımı çiz')).toBeVisible()

    await app.openProject('Mobil Uygulama')
    await expect(app.boardTitle('Mobil Uygulama')).toBeVisible()
    await expect(app.page.getByRole('article')).toHaveCount(0) // öncekinin kartları sızmamalı

    await app.openProject('Web Sitesi')
    await expect(app.card(TODO, 'Tasarımı çiz')).toBeVisible()
  })
})
