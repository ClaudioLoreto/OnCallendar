# OnCallendar

SaaS multi-tenant per la gestione dei turni di guardia medica.

## Stack
- **Backend:** .NET 8 (Clean Architecture, EF Core 8, Identity, JWT, SQL Server)
- **Mobile:** Expo SDK 54 + React Native + TypeScript
- **DB dev:** SQL Server LocalDB

## Requisiti
- .NET 8 SDK
- Node.js 20+
- SQL Server LocalDB (Windows) o SQL Server
- Expo Go (iOS / Android) per testing su dispositivo

## Setup
```powershell
# Backend
cd backend
dotnet restore
dotnet run --project src/OnCallendar.Api
# → http://localhost:5000

# Mobile
cd mobile
npm install --legacy-peer-deps
npx expo start --tunnel
```

## Struttura
- `backend/` — API, Domain, Application, Infrastructure
- `mobile/`  — App Expo
