import type { Locator, Page } from '@playwright/test'

// Page Object: sayfadaki öğeleri ve sık yapılan işlemleri tek yerde toplar.
// Testler "nasıl bulunur / nasıl tıklanır" ayrıntısını bilmez, sadece niyeti anlatır:
//   await app.addTask('Rapor yaz')
// Arayüzde bir etiket değişirse düzeltilecek tek yer burasıdır.
//
// Öğeleri CSS sınıfıyla değil, kullanıcının gördüğü rol/etiket/metinle buluruz
// (getByRole, getByLabel). Böylece testler hem erişilebilirliği de sınamış olur
// hem de stil değişikliklerinden etkilenmez.
export class AppPage {
  readonly page: Page
  readonly projectNameInput: Locator
  readonly addProjectButton: Locator
  readonly taskTitleInput: Locator
  readonly addTaskButton: Locator
  readonly alert: Locator
  readonly notice: Locator
  readonly emailInput: Locator
  readonly passwordInput: Locator
  readonly loginButton: Locator
  readonly registerButton: Locator
  readonly logoutButton: Locator

  constructor(page: Page) {
    this.page = page
    this.projectNameInput = page.getByLabel('Yeni proje adı')
    this.addProjectButton = page.getByRole('button', { name: 'Ekle', exact: true })
    this.taskTitleInput = page.getByLabel('Yeni görev başlığı')
    this.addTaskButton = page.getByRole('button', { name: 'Görev ekle' })
    this.alert = page.getByRole('alert')
    this.notice = page.getByRole('status') // giriş ekranındaki bilgi (ör. "Oturumun süresi doldu")
    this.emailInput = page.getByLabel('E-posta')
    this.passwordInput = page.getByLabel('Şifre')
    // exact: "Hesabın yok mu? Kayıt ol" gibi geçiş butonlarıyla karışmasın
    this.loginButton = page.getByRole('button', { name: 'Giriş yap', exact: true })
    this.registerButton = page.getByRole('button', { name: 'Kayıt ol', exact: true })
    this.logoutButton = page.getByRole('button', { name: 'Çıkış yap' })
  }

  async goto() {
    await this.page.goto('/')
  }

  // ---------- Giriş / kayıt ----------

  async login(email: string, password: string) {
    await this.emailInput.fill(email)
    await this.passwordInput.fill(password)
    await this.loginButton.click()
  }

  /** Giriş formundan kayıt formuna geçer ve hesabı oluşturur. */
  async register(email: string, password: string) {
    await this.page.getByRole('button', { name: 'Hesabın yok mu? Kayıt ol' }).click()
    await this.emailInput.fill(email)
    await this.passwordInput.fill(password)
    await this.registerButton.click()
  }

  /** Giriş ekranının başlığı (giriş yapılmamışken görünür). */
  get loginHeading() {
    return this.page.getByRole('heading', { level: 1, name: 'Giriş yap' })
  }

  /** Kenar çubuğunun altında görünen, giriş yapmış hesabın e-postası. */
  accountEmail(email: string) {
    return this.page.getByText(email, { exact: true })
  }

  // ---------- Projeler ----------

  async addProject(name: string) {
    await this.projectNameInput.fill(name)
    await this.addProjectButton.click()
  }

  projectItem(name: string) {
    return this.page.getByRole('listitem').filter({ hasText: name })
  }

  /** Proje satırındaki ilk buton "projeyi aç" butonudur (ikincisi silme butonu). */
  projectButton(name: string) {
    return this.projectItem(name).getByRole('button').first()
  }

  /** Proje satırındaki görev sayacı rozeti. */
  projectCount(name: string) {
    return this.projectItem(name).getByText(/^\d+$/)
  }

  deleteProjectButton(name: string) {
    return this.page.getByRole('button', { name: `${name} projesini sil` })
  }

  async openProject(name: string) {
    await this.projectButton(name).click()
  }

  boardTitle(name: string) {
    return this.page.getByRole('heading', { level: 1, name })
  }

  // ---------- Pano ----------

  async addTask(title: string) {
    await this.taskTitleInput.fill(title)
    await this.addTaskButton.click()
  }

  column(title: string) {
    return this.page.getByRole('region', { name: title })
  }

  card(columnTitle: string, taskTitle: string) {
    return this.column(columnTitle)
      .getByRole('article')
      .filter({ has: this.page.getByRole('heading', { name: taskTitle, exact: true }) })
  }

  moveButton(card: Locator, targetColumnTitle: string) {
    return card.getByRole('button', { name: `"${targetColumnTitle}" kolonuna taşı` })
  }

  async moveWithButton(fromColumn: string, taskTitle: string, toColumn: string) {
    await this.moveButton(this.card(fromColumn, taskTitle), toColumn).click()
  }

  async dragTask(fromColumn: string, taskTitle: string, toColumn: string) {
    await this.card(fromColumn, taskTitle).dragTo(this.column(toColumn))
  }

  async deleteTask(columnTitle: string, taskTitle: string) {
    await this.card(columnTitle, taskTitle)
      .getByRole('button', { name: `"${taskTitle}" görevini sil` })
      .click()
  }
}
