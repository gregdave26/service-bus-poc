# BDD Test Script Usage Guide

Run from the repo root:

```powershell
.\test-scripts\start-bdd-tests.ps1
```

## Default behaviour (no flags)

The script starts only what is missing and never rebuilds running services.

| Service | Port | Behaviour |
|---------|------|-----------|
| Backend (Aspire AppHost, via `scripts\run-aspire.ps1 -NoBrowser -NoBuild`) | - | Started only if no AppHost process for this worktree is running |
| UI server (`ui\server.js`) | 5100 | Started only if the backend is running but 5100 is not responding. Otherwise it is started by `run-aspire.ps1` |
| Vite dev server (`ui\node_modules\vite\bin\vite.js`) | 5173 | Started only if no Vite process for this worktree is running |
| UI build (`ui\public\dist`) | - | Built once, only if missing |

The script then waits for `http://localhost:5100/?tab=digitalSite` and `http://localhost:5173/` to return HTTP 200 (default timeout 300 seconds), and runs `npm run test:bdd`.

Feature and step file changes need no rebuild or restart.

## Fresh restarts (`-Fresh backend | ui | all`)

```powershell
.\test-scripts\start-bdd-tests.ps1 -Fresh backend   # rebuild and restart the backend only
.\test-scripts\start-bdd-tests.ps1 -Fresh ui        # rebuild and restart the UI and Vite only
.\test-scripts\start-bdd-tests.ps1 -Fresh all       # both
```

- `backend`: stops the Aspire AppHost and `run-aspire.ps1` windows for this worktree, runs `dotnet clean` and `dotnet build`, then starts the backend.
- `ui`: stops the UI server (`server.js`) and Vite for this worktree, runs `npm ci` and `npm run build`, then starts them again.
- `all`: runs both.

Services that are not selected keep running.

## Options

| Option | Default | Purpose |
|--------|---------|---------|
| `-Fresh backend\|ui\|all` | off | Stop, clean rebuild, and restart the chosen services |
| `-ReadyTimeoutSeconds <n>` | 300 | Maximum wait for the UI and Vite to respond (30 to 3600) |

## Logs

- Backend: the `run-aspire.ps1` PowerShell window and `logs\`
- UI server: `logs\bdd-ui-server.log` and `logs\bdd-ui-server-error.log`
- Vite: `logs\bdd-vite.log` and `logs\bdd-vite-error.log`

## Running tests only

If the services are already running and you only want the tests:

```powershell
cd ui
npm run test:bdd
```

## Troubleshooting

- **UI did not respond within the timeout:** check the backend window and `logs\bdd-ui-server-error.log`. The UI on 5100 serves the built bundle in `ui\public\dist`. If the UI source has changed, run with `-Fresh ui`.
- **Port already in use:** run with `-Fresh all` to stop this worktree's processes. If a process from another worktree holds the port, stop it manually with `Stop-Process -Id <PID>`.
