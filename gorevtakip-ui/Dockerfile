# syntax=docker/dockerfile:1
# Arayüz imajı. Derleme bağlamı gorevtakip-ui/ klasörüdür:
#   docker build --build-arg VITE_API_URL=http://localhost:5291 -t gorevtakip-ui ./gorevtakip-ui

# ---- 1) Derleme aşaması: Node ile statik dosyaları üret ----
FROM node:22-alpine AS build
WORKDIR /app

# Önce yalnızca paket dosyaları: kaynak değişince `npm ci` katmanı önbellekten gelir
COPY package.json package-lock.json ./
RUN npm ci

COPY . .

# Vite, API adresini derleme sırasında koda GÖMER (çalışırken değiştirilemez).
# Adres verilmezse sessizce yanlış çalışmak yerine derlemeyi durduruyoruz.
ARG VITE_API_URL
ENV VITE_API_URL=$VITE_API_URL
RUN test -n "$VITE_API_URL" || (echo "HATA: --build-arg VITE_API_URL=<API adresi> verilmeli" && exit 1)
RUN npm run build

# ---- 2) Çalışma aşaması: sadece nginx + derlenmiş dosyalar ----
# nginx-unprivileged: root olmadan, 8080 portunda çalışır
FROM nginxinc/nginx-unprivileged:stable-alpine AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 8080
