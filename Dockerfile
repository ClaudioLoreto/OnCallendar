# syntax=docker/dockerfile:1
# ---------- web (Expo SPA → static bundle) ----------
# La WebApp riusa la codebase di `mobile/` via react-native-web.
# Output finale: /web/dist (poi copiato in wwwroot/ del runtime .NET).
FROM node:20-alpine AS web
WORKDIR /work
COPY mobile/package.json mobile/package-lock.json ./mobile/
RUN cd mobile && npm ci --no-audit --no-fund
COPY mobile/ ./mobile/
RUN cd mobile && npx expo export -p web --output-dir ../web/dist

# ---------- build .NET ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia le csproj prima per cache dei restore
COPY backend/OnCallendar.sln ./
COPY backend/src/OnCallendar.Domain/OnCallendar.Domain.csproj         src/OnCallendar.Domain/
COPY backend/src/OnCallendar.Application/OnCallendar.Application.csproj src/OnCallendar.Application/
COPY backend/src/OnCallendar.Infrastructure/OnCallendar.Infrastructure.csproj src/OnCallendar.Infrastructure/
COPY backend/src/OnCallendar.Api/OnCallendar.Api.csproj               src/OnCallendar.Api/

RUN dotnet restore src/OnCallendar.Api/OnCallendar.Api.csproj

# Copia il resto e pubblica
COPY backend/ .
RUN dotnet publish src/OnCallendar.Api/OnCallendar.Api.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
# Web SPA copiata direttamente in wwwroot (servita da UseStaticFiles)
COPY --from=web /work/web/dist ./wwwroot/
# I file statici del backend (favicon, error page, ecc.) vengono copiati
# DOPO l'export Expo, così non vengono sovrascritti.
COPY --from=build /app/publish/wwwroot/favicon.png ./wwwroot/
COPY --from=build /app/publish/wwwroot/favicon.ico ./wwwroot/
COPY --from=build /app/publish/wwwroot/error.html  ./wwwroot/
COPY --from=build /app/publish/wwwroot/errore.png  ./wwwroot/

# Railway inietta $PORT (di solito 8080); l'app legge env e usa quella
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080

ENTRYPOINT ["dotnet", "OnCallendar.Api.dll"]
