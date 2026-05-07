# syntax=docker/dockerfile:1
# ---------- web (Expo SPA → static bundle) ----------
FROM node:20-alpine AS web
WORKDIR /web
COPY mobile/package.json mobile/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY mobile/ ./
RUN npx expo export -p web

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
# Web SPA copiata in wwwroot/app: l'API la serve come fallback su rotte non /api
COPY --from=web /web/dist ./wwwroot/app

# Railway inietta $PORT (di solito 8080); l'app legge env e usa quella
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080

ENTRYPOINT ["dotnet", "OnCallendar.Api.dll"]
