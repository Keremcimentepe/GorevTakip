# syntax=docker/dockerfile:1
# API imajı. Derleme bağlamı (context) GorevTakip/ klasörüdür:
#   docker build -t gorevtakip-api ./GorevTakip

# ---- 1) Derleme aşaması: .NET SDK (büyük imaj; sadece derlerken lazım, canlıya girmez) ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Önce yalnızca .csproj'u kopyalayıp paketleri indiriyoruz. Kaynak kod değişince bu katman
# önbellekten gelir; her derlemede NuGet'ten yeniden indirme yapılmaz.
COPY GorevTakip.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish GorevTakip.csproj -c Release -o /app/publish --no-restore

# ---- 2) Çalışma aşaması: yalnızca ASP.NET çalışma zamanı (küçük imaj) ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# .NET 8'den beri imajlar 8080 portunu dinler; açıkça yazmak okuyana da yardım eder.
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

COPY --from=build /app/publish ./

# root olmayan kullanıcı: konteynerde bir açık çıkarsa saldırgan root olmaz
USER $APP_UID

ENTRYPOINT ["dotnet", "GorevTakip.dll"]
