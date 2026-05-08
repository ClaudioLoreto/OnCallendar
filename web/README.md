# Web App (OnCallendar Web)

Questa cartella contiene **solo la configurazione e gli script** per buildare la
versione **web** dell'app a partire dalla codebase mobile (`../mobile/`).

## Architettura

```
mobile/   ← unica sorgente React Native (iOS/Android/Web via react-native-web)
web/      ← script di build, output statico, README (questa cartella)
backend/  ← API .NET 8
```

La WebApp **non** è un progetto separato: è la stessa codebase del mobile,
compilata con `react-native-web` tramite `expo export -p web`. Questo significa
che:

- Modifiche fatte in `mobile/src/...` si riflettono **sia** sull'app mobile
  **sia** sulla WebApp.
- Non c'è codice duplicato.
- Alcune feature native (push notification, image picker fotocamera, deeplink)
  funzionano solo su mobile e fanno fallback graceful su web.

## Build locale

```powershell
cd mobile
npx expo export -p web --output-dir ../web/dist
```

L'output finisce in `web/dist/` ed è ciò che il backend serve come SPA da
`wwwroot/` quando deployato su Railway (vedi `Dockerfile` alla root).

## Sviluppo locale (browser)

```powershell
cd mobile
npx expo start --web
```

Apre `http://localhost:8081` con hot-reload.

## Deploy

Il deploy è gestito dal `Dockerfile` alla root del repo: la stage `web` esegue
`expo export -p web` e copia il bundle in `wwwroot/` del container .NET.
