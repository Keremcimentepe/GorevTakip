import type { Page, Route } from '@playwright/test'

// Gerçek .NET API'nin yerine geçen, bellekte çalışan sahte backend.
// Playwright, tarayıcının "/api/..." isteklerini ağa çıkmadan yakalar ve bu sınıfa cevaplatır.
// Avantajı: .NET API'yi, veritabanını ya da CORS'u ayağa kaldırmadan arayüzü gerçek bir
// tarayıcıda hızlı ve tekrarlanabilir şekilde sınarız; ayrıca "sunucu 500 verirse ne olur?"
// gibi nadir durumları istediğimiz an üretebiliriz.
//
// Giriş de gerçek API gibi davranır: /auth/register ve /auth/login token üretir, diğer bütün
// uç noktalar "Authorization: Bearer ..." ister (yoksa ya da geçersizse boş gövdeli 401 — JwtBearer
// da böyle yapar) ve her kullanıcı yalnızca kendi projelerini görür.
//
// Dikkat: Bu sahte, gerçek API'nin KOPYASI değildir. API'deki doğrulama mesajları ya da
// uç noktalar değişirse burayı da elle güncellemek gerekir. Gerçek API ile uçtan uca
// deneme CI'daki Docker adımında yapılır (.github/workflows/ci.yml → docker-smoke).

export type TaskStatus = 'Todo' | 'InProgress' | 'Done'

export interface FakeProject {
  id: number
  name: string
  description: string | null
  createdAt: string
  ownerEmail: string
}

/** Fixture'ın varsayılan olarak giriş yaptırdığı kullanıcı; seed verisi (addProject) de onun olur. */
export const DEFAULT_EMAIL = 'kullanici@ornek.com'
export const DEFAULT_PASSWORD = 'Sifre1234!'

/** Tarayıcıya yazılan oturumun biçimi (src/api/session.ts ile aynı). */
export interface FakeSession {
  token: string
  email: string
  expiresAt: string
}

export interface FakeTask {
  id: number
  title: string
  description: string | null
  status: TaskStatus
  createdAt: string
  projectId: number
}

interface PlannedFailure {
  method: string
  path: RegExp
  status: number
  body: unknown
}

const STATUSES: TaskStatus[] = ['Todo', 'InProgress', 'Done']

export class FakeApi {
  projects: FakeProject[] = []
  tasks: FakeTask[] = []
  /** Gelen isteklerin kaydı: ["GET /projects", "PATCH /tasks/3/status", ...] */
  requests: string[] = []

  private nextProjectId = 1
  private nextTaskId = 1
  private nextTokenId = 1
  private clock = 0
  private failures: PlannedFailure[] = []
  private offline = false
  /** e-posta (küçük harf) → şifre */
  private users = new Map<string, string>()
  /** geçerli token → e-posta */
  private tokens = new Map<string, string>()

  // ---------- Test hazırlığı (seed) ----------

  addProject(name: string, description: string | null = null, ownerEmail = DEFAULT_EMAIL): FakeProject {
    const project = {
      id: this.nextProjectId++,
      name,
      description,
      createdAt: this.timestamp(),
      ownerEmail,
    }
    this.projects.push(project)
    return project
  }

  /** Kayıtlı bir kullanıcı ekler (kayıt ekranından geçmeden). */
  addUser(email: string, password = DEFAULT_PASSWORD) {
    this.users.set(email.toLowerCase(), password)
  }

  /** Kullanıcıya (yoksa oluşturup) geçerli bir token verir ve tarayıcıya yazılacak oturumu döner. */
  signIn(email = DEFAULT_EMAIL): FakeSession {
    if (!this.users.has(email.toLowerCase())) this.addUser(email)
    return this.issueSession(email.toLowerCase())
  }

  /** Sunucu bütün token'ları geçersiz sayar (süresi dolmuş / anahtar değişmiş gibi). */
  revokeTokens() {
    this.tokens.clear()
  }

  addTask(projectId: number, title: string, status: TaskStatus = 'Todo'): FakeTask {
    const task: FakeTask = {
      id: this.nextTaskId++,
      title,
      description: null,
      status,
      createdAt: this.timestamp(),
      projectId,
    }
    this.tasks.push(task)
    return task
  }

  /** Eşleşen BİR sonraki isteğe, normal cevap yerine verilen hata cevabını döner. body verilmezse gövdesiz. */
  failNext(method: string, path: RegExp, status: number, body?: unknown) {
    this.failures.push({ method, path, status, body })
  }

  /** Sunucu kapanmış gibi davran: bağlantı reddedilir, fetch hata fırlatır. */
  goOffline() {
    this.offline = true
  }

  // ---------- Playwright'a bağlama ----------

  async install(page: Page) {
    // Tam olarak "/api/" ile başlayan yolları yakala. (Vite geliştirme sunucusu "/src/api/client.ts"
    // gibi dosyalar da sunar; "**/api/**" gibi geniş bir desen onları da bozardı.)
    await page.route((url) => url.pathname.startsWith('/api/'), (route) => this.handle(route))
  }

  private timestamp() {
    // Her kayıt bir öncekinden 1 saniye sonra: sıralama testlerde rastlantıya kalmaz
    return new Date(Date.UTC(2026, 0, 1, 0, 0, this.clock++)).toISOString()
  }

  private async handle(route: Route) {
    const request = route.request()
    const method = request.method()
    const path = new URL(request.url()).pathname.replace(/^\/api/, '')
    this.requests.push(`${method} ${path}`)

    if (this.offline) return route.abort('connectionrefused')

    const plannedIndex = this.failures.findIndex((f) => f.method === method && f.path.test(path))
    if (plannedIndex >= 0) {
      const [planned] = this.failures.splice(plannedIndex, 1)
      if (planned.body === undefined) return route.fulfill({ status: planned.status })
      return this.json(route, planned.status, planned.body)
    }

    const body = (request.postDataJSON() ?? {}) as Record<string, unknown>
    const segments = path.split('/').filter(Boolean)

    // ----- /auth/register, /auth/login (token gerekmez) -----
    if (segments[0] === 'auth' && segments.length === 2 && method === 'POST') {
      if (segments[1] === 'register') return this.register(route, body)
      if (segments[1] === 'login') return this.login(route, body)
    }

    // ----- Bundan sonrası giriş ister -----
    const email = this.tokens.get(this.bearerToken(request.headers()['authorization']))
    if (!email) return route.fulfill({ status: 401 })

    // ----- /projects -----
    if (segments[0] === 'projects' && segments.length === 1) {
      if (method === 'GET') return this.json(route, 200, this.listProjects(email))
      if (method === 'POST') return this.createProject(route, body, email)
    }

    // ----- /projects/{id} -----
    // Başkasının projesi "yok" gibi davranılır (gerçek API de 403 değil 404 döner)
    if (segments[0] === 'projects' && segments.length === 2) {
      const project = this.ownedProject(Number(segments[1]), email)
      if (method === 'GET') {
        if (!project) return route.fulfill({ status: 404 })
        return this.json(route, 200, { ...this.publicFields(project), tasks: this.tasksOf(project.id) })
      }
      if (method === 'DELETE') {
        if (!project) return route.fulfill({ status: 404 })
        this.projects = this.projects.filter((p) => p.id !== project.id)
        this.tasks = this.tasks.filter((t) => t.projectId !== project.id) // cascade
        return route.fulfill({ status: 204 })
      }
    }

    // ----- /projects/{id}/tasks -----
    if (segments[0] === 'projects' && segments[2] === 'tasks' && method === 'POST') {
      return this.createTask(route, Number(segments[1]), body, email)
    }

    // ----- /tasks/{id}/status -----
    if (segments[0] === 'tasks' && segments[2] === 'status' && method === 'PATCH') {
      return this.moveTask(route, Number(segments[1]), body, email)
    }

    // ----- /tasks/{id} -----
    if (segments[0] === 'tasks' && segments.length === 2 && method === 'DELETE') {
      const task = this.ownedTask(Number(segments[1]), email)
      if (!task) return route.fulfill({ status: 404 })
      this.tasks = this.tasks.filter((t) => t.id !== task.id)
      return route.fulfill({ status: 204 })
    }

    return route.fulfill({ status: 404 })
  }

  // ---------- Uç noktalar ----------

  private listProjects(email: string) {
    return this.projects
      .filter((p) => p.ownerEmail === email)
      .reverse() // en yeni proje en üstte (gerçek API de CreatedAt'e göre azalan sıralar)
      .map((p) => ({ ...this.publicFields(p), taskCount: this.tasksOf(p.id).length }))
  }

  // Gerçek API sahip bilgisini (ownerEmail) cevapta döndürmez
  private publicFields(p: FakeProject) {
    return { id: p.id, name: p.name, description: p.description, createdAt: p.createdAt }
  }

  private tasksOf(projectId: number) {
    return this.tasks.filter((t) => t.projectId === projectId)
  }

  private ownedProject(id: number, email: string) {
    return this.projects.find((p) => p.id === id && p.ownerEmail === email)
  }

  private ownedTask(id: number, email: string) {
    const task = this.tasks.find((t) => t.id === id)
    return task && this.ownedProject(task.projectId, email) ? task : undefined
  }

  private createProject(route: Route, body: Record<string, unknown>, email: string) {
    const name = typeof body.name === 'string' ? body.name : ''
    const errors: Record<string, string[]> = {}
    if (name.trim() === '') errors.Name = ['Proje adı boş olamaz.']
    else if (name.length > 100) errors.Name = ['Proje adı en fazla 100 karakter olabilir.']
    if (Object.keys(errors).length > 0) return this.validationProblem(route, errors)

    const project = this.addProject(name.trim(), (body.description as string | null) ?? null, email)
    return this.json(route, 201, { ...this.publicFields(project), taskCount: 0 })
  }

  private createTask(route: Route, projectId: number, body: Record<string, unknown>, email: string) {
    const title = typeof body.title === 'string' ? body.title : ''
    const errors: Record<string, string[]> = {}
    if (title.trim() === '') errors.Title = ['Görev başlığı boş olamaz.']
    else if (title.length > 200) errors.Title = ['Görev başlığı en fazla 200 karakter olabilir.']
    // Gerçek API'de doğrulama filtresi endpoint'ten önce çalışır: proje yoksa bile önce 400 döner
    if (Object.keys(errors).length > 0) return this.validationProblem(route, errors)

    if (!this.ownedProject(projectId, email)) return route.fulfill({ status: 404 })

    const task = this.addTask(projectId, title.trim())
    return this.json(route, 201, task)
  }

  private moveTask(route: Route, taskId: number, body: Record<string, unknown>, email: string) {
    if (!STATUSES.includes(body.status as TaskStatus)) {
      return this.validationProblem(route, { Status: ['Geçersiz görev durumu.'] })
    }
    const task = this.ownedTask(taskId, email)
    if (!task) return route.fulfill({ status: 404 })

    task.status = body.status as TaskStatus
    return this.json(route, 200, task)
  }

  // ---------- Giriş ----------

  private register(route: Route, body: Record<string, unknown>) {
    const email = typeof body.email === 'string' ? body.email.trim().toLowerCase() : ''
    const password = typeof body.password === 'string' ? body.password : ''

    // API'deki RegisterRequestValidator ile aynı kurallar ve mesajlar
    const errors: Record<string, string[]> = {}
    if (email === '') errors.Email = ['E-posta boş olamaz.']
    else if (!/^[^@\s]+@[^@\s]+$/.test(email)) errors.Email = ['Geçerli bir e-posta adresi girin.']
    if (password === '') errors.Password = ['Şifre boş olamaz.']
    else if (password.length < 8) errors.Password = ['Şifre en az 8 karakter olmalı.']
    if (Object.keys(errors).length > 0) return this.validationProblem(route, errors)

    if (this.users.has(email)) {
      return this.json(route, 409, { status: 409, title: 'Bu e-posta adresiyle zaten bir hesap var.' })
    }

    this.users.set(email, password)
    return this.json(route, 201, this.issueSession(email))
  }

  private login(route: Route, body: Record<string, unknown>) {
    const email = typeof body.email === 'string' ? body.email.trim().toLowerCase() : ''
    const password = typeof body.password === 'string' ? body.password : ''

    // Yanlış şifre ile bilinmeyen e-posta aynı cevabı alır (hangi e-postaların kayıtlı olduğu sızmaz)
    if (this.users.get(email) !== password || !this.users.has(email)) {
      return this.json(route, 401, { status: 401, title: 'E-posta veya şifre hatalı.' })
    }
    return this.json(route, 200, this.issueSession(email))
  }

  private issueSession(email: string): FakeSession {
    const token = `sahte-token-${this.nextTokenId++}`
    this.tokens.set(token, email)
    return { token, email, expiresAt: new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString() }
  }

  private bearerToken(header: string | undefined) {
    return header?.startsWith('Bearer ') ? header.slice('Bearer '.length) : ''
  }

  // ---------- Cevap üreticileri ----------

  private json(route: Route, status: number, body: unknown) {
    return route.fulfill({ status, contentType: 'application/json', body: JSON.stringify(body) })
  }

  private validationProblem(route: Route, errors: Record<string, string[]>) {
    return this.json(route, 400, {
      type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1',
      title: 'One or more validation errors occurred.',
      status: 400,
      errors,
    })
  }
}

/** API'nin 500 cevabı (GlobalExceptionHandler'ın ürettiği ProblemDetails). */
export const SERVER_ERROR_BODY = {
  status: 500,
  title: 'Beklenmeyen bir hata oluştu.',
  detail: 'İsteğiniz işlenirken sunucuda bir sorun çıktı.',
}
