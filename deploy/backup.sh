#!/usr/bin/env bash
# Veritabanının yedeğini alır: ~/gorevtakip-yedek/gorevtakip-YYYYMMDD-HHMMSS.sql.gz (son 14 yedek tutulur).
# Kullanım (sunucuda):   bash deploy/backup.sh
# Her gece otomatik: bkz. DEPLOY-ORACLE.md → "Yedek".
#
# DİKKAT: Yedek aynı sunucuda durur. Sunucu silinirse yedek de gider; dosyaları ara sıra kendi
# bilgisayarına da indir (scp komutu DEPLOY-ORACLE.md'de).
set -euo pipefail

cd "$(dirname "$0")/.."

DIR="${BACKUP_DIR:-$HOME/gorevtakip-yedek}"
mkdir -p "$DIR"
FILE="$DIR/gorevtakip-$(date +%Y%m%d-%H%M%S).sql.gz"
PARTIAL="$FILE.partial"   # önce geçici dosyaya yazılır; başarısızsa mevcut yedeklere dokunulmaz
trap 'rm -f "$PARTIAL"' EXIT

# Kullanıcı/veritabanı adı konteynerin kendi ortamından okunur (burada tekrar tanımlamayalım)
if ! docker compose -f docker-compose.prod.yml exec -T db \
      sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB"' | gzip > "$PARTIAL"; then
  echo "HATA: yedek alınamadı (konteynerler çalışıyor mu?)" >&2
  exit 1
fi

# Boş/bozuk yedeği gerçek yedek sanma
if [[ "$(gzip -dc "$PARTIAL" | wc -c)" -lt 100 ]]; then
  echo "HATA: yedek dosyası boş çıktı" >&2
  exit 1
fi

mv "$PARTIAL" "$FILE"

# Eskileri sil: en yeni 14 kalsın
ls -1t "$DIR"/gorevtakip-*.sql.gz | tail -n +15 | xargs -r rm --

echo "Yedek alındı: $FILE ($(du -h "$FILE" | cut -f1))"
