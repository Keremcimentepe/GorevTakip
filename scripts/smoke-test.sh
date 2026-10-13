#!/usr/bin/env bash
# Çalışan GERÇEK API'ye (gerçek PostgreSQL ile) karşı hızlı duman testi: kayıt, giriş, yetkisiz erişim,
# iki kullanıcının birbirinin verisini görememesi, CORS. Testler yalnızca HTTP ile konuşur.
#
# Kullanım:  scripts/smoke-test.sh [API_ADRESI] [ARAYÜZ_ADRESI]
#   scripts/smoke-test.sh                                  # http://localhost:5291, arayüz http://localhost:8080
#   scripts/smoke-test.sh https://gorevtakip-api.onrender.com https://gorevtakip-ui.onrender.com
#
# Gereken araçlar: bash, curl.
set -euo pipefail

BASE_URL="${1:-http://localhost:5291}"
ORIGIN="${2:-http://localhost:8080}"
BODY_FILE="$(mktemp)"
trap 'rm -f "$BODY_FILE"' EXIT

STATUS=""
BODY=""

ok()   { echo "✓ $*"; }
fail() { echo "✗ $*" >&2; exit 1; }

# call METOT YOL [TOKEN] [JSON_GÖVDE]  →  STATUS ve BODY değişkenlerini doldurur
call() {
  local method="$1" path="$2" token="${3:-}" data="${4:-}"
  local args=(-sS -o "$BODY_FILE" -w '%{http_code}' -X "$method" -H 'Content-Type: application/json')
  [[ -n "$token" ]] && args+=(-H "Authorization: Bearer $token")
  [[ -n "$data" ]] && args+=(--data "$data")
  STATUS="$(curl "${args[@]}" "$BASE_URL$path")"
  BODY="$(cat "$BODY_FILE")"
}

# expect "açıklama" BEKLENEN_KOD
expect() {
  if [[ "$STATUS" == "$2" ]]; then ok "$1 → $STATUS"; else fail "$1: beklenen $2, gelen $STATUS ($BODY)"; fi
}

# JSON cevabından basit bir alanı çıkarır (jq gerektirmesin diye sed ile)
json_string() { sed -n "s/.*\"$1\":\"\([^\"]*\)\".*/\1/p" <<<"$BODY" | head -n1; }
json_number() { sed -n "s/.*\"$1\":\([0-9][0-9]*\).*/\1/p" <<<"$BODY" | head -n1; }

# --- API ayağa kalkana kadar bekle (Render ücretsiz katmanı uykudan ~1 dk'da uyanır) ---
echo "API bekleniyor: $BASE_URL/health"
for attempt in $(seq 1 60); do
  if [[ "$(curl -s -o /dev/null -w '%{http_code}' "$BASE_URL/health" || true)" == "200" ]]; then break; fi
  [[ "$attempt" == "60" ]] && fail "API 2 dakika içinde sağlıklı hale gelmedi"
  sleep 2
done

PASSWORD='Sifre1234!x'
SUFFIX="$(date +%s)-$RANDOM"
EMAIL_A="duman-a-$SUFFIX@example.com"
EMAIL_B="duman-b-$SUFFIX@example.com"

call GET /health;                                   expect "sağlık kontrolü" 200
call GET /projects;                                 expect "token olmadan /projects" 401

# --- Kayıt ve giriş ---
call POST /auth/register "" "{\"email\":\"$EMAIL_A\",\"password\":\"$PASSWORD\"}"
expect "A kullanıcısı kayıt" 201
TOKEN_A="$(json_string token)"
[[ -n "$TOKEN_A" ]] || fail "kayıt cevabında token yok: $BODY"

call POST /auth/register "" "{\"email\":\"$EMAIL_A\",\"password\":\"$PASSWORD\"}"
expect "aynı e-postayla ikinci kayıt" 409

call POST /auth/register "" "{\"email\":\"$EMAIL_A\",\"password\":\"kisa\"}"
expect "kısa şifreyle kayıt (doğrulama)" 400

call POST /auth/login "" "{\"email\":\"$EMAIL_A\",\"password\":\"YanlisSifre99\"}"
expect "yanlış şifreyle giriş" 401

call POST /auth/login "" "{\"email\":\"$EMAIL_A\",\"password\":\"$PASSWORD\"}"
expect "doğru şifreyle giriş" 200

# --- Bozuk token ---
# İçeriği (payload) değiştirilmiş token: imza artık tutmaz. (İmzanın SON karakterini değiştirmek yetmez:
# base64'te son karakterin bazı bitleri boşluktur, farklı harf aynı imzayı verebilir.)
IFS='.' read -r JWT_HEADER JWT_PAYLOAD JWT_SIGNATURE <<<"$TOKEN_A"
swap="A"; [[ "${JWT_PAYLOAD:10:1}" == "A" ]] && swap="B"
TAMPERED="$JWT_HEADER.${JWT_PAYLOAD:0:10}$swap${JWT_PAYLOAD:11}.$JWT_SIGNATURE"
call GET /projects "$TAMPERED";                     expect "içeriği değiştirilmiş token ile /projects" 401

# --- A kullanıcısı proje ve görev oluşturur ---
call POST /projects "$TOKEN_A" '{"name":"Duman projesi","description":null}'
expect "proje oluştur" 201
PROJECT_ID="$(json_number id)"
[[ -n "$PROJECT_ID" ]] || fail "proje cevabında id yok: $BODY"

call POST "/projects/$PROJECT_ID/tasks" "$TOKEN_A" '{"title":"Duman görevi","description":null}'
expect "görev oluştur" 201
TASK_ID="$(json_number id)"

call GET "/projects/$PROJECT_ID" "$TOKEN_A"
expect "proje detayı" 200
[[ "$BODY" == *"Duman görevi"* ]] || fail "proje detayında görev yok: $BODY"

# --- B kullanıcısı A'nın verisini göremez ---
call POST /auth/register "" "{\"email\":\"$EMAIL_B\",\"password\":\"$PASSWORD\"}"
expect "B kullanıcısı kayıt" 201
TOKEN_B="$(json_string token)"

call GET /projects "$TOKEN_B"
expect "B'nin proje listesi" 200
[[ "$BODY" != *"Duman projesi"* ]] || fail "B, A'nın projesini listede görüyor: $BODY"
call GET "/projects/$PROJECT_ID" "$TOKEN_B";        expect "B → A'nın projesi" 404
call DELETE "/projects/$PROJECT_ID" "$TOKEN_B";     expect "B → A'nın projesini silme" 404
call GET "/tasks/$TASK_ID" "$TOKEN_B";              expect "B → A'nın görevi" 404

call GET "/projects/$PROJECT_ID" "$TOKEN_A";        expect "A'nın projesi hâlâ duruyor" 200

# --- CORS: tarayıcı Authorization başlıklı isteklerden önce "preflight" sorar ---
PREFLIGHT="$(curl -sS -i -X OPTIONS \
  -H "Origin: $ORIGIN" \
  -H 'Access-Control-Request-Method: POST' \
  -H 'Access-Control-Request-Headers: authorization,content-type' \
  "$BASE_URL/projects")"
if grep -qi "^access-control-allow-origin: $ORIGIN" <<<"$PREFLIGHT"; then
  ok "CORS preflight $ORIGIN adresine izin veriyor"
else
  fail "CORS preflight $ORIGIN adresine izin vermiyor (Cors__AllowedOrigins__0 ayarını kontrol et):
$PREFLIGHT"
fi

# --- Temizlik ---
call DELETE "/projects/$PROJECT_ID" "$TOKEN_A";     expect "A'nın projesini sil" 204

echo
echo "Duman testi başarılı."
