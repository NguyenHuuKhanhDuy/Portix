# Portix

A self-hosted tunnel server, similar in spirit to ngrok: expose a port on your own machine through a public URL served by your own tunnel server, with a live traffic inspector and a CLI that feels like ngrok's.

```
portix http 3000
```

```
Session Status   online
Web Interface    http://127.0.0.1:4040
Forwarding       https://a1b2c3d4.tunnel.example.com -> http://localhost:3000

HTTP Requests
┌──────────┬────────┬───────────────────┬──────────┐
│ Time     │ Method │ Path              │ Status   │
├──────────┼────────┼───────────────────┼──────────┤
│ 14:02:11 │ GET    │ /                 │ 200 OK   │
│ 14:02:12 │ GET    │ /api/health       │ 200 OK   │
└──────────┴────────┴───────────────────┴──────────┘
```

## Architecture

The solution has three .NET projects plus a small React dashboard:

| Project | Role |
|---|---|
| `src/Portix.Server` | The public-facing relay. Accepts client control connections, routes public HTTP/WebSocket traffic to the right tunnel by subdomain, and exposes an admin API for managing users, tokens, and plans. |
| `src/Portix.Client` | The `portix` CLI and local daemon. Registers tunnels with the server, forwards traffic to your local app, and serves a local web dashboard for inspecting traffic. |
| `src/Portix.Shared` | Wire protocol code shared by the Server and Client (framing, message types, header handling). |
| `dashboard/` | The React SPA served by the Client's local API — the traffic inspector UI. |

A tunnel works like this: the Client opens one long-lived control connection to the Server and registers a subdomain. When a public request arrives for that subdomain, the Server signals the Client over the control channel, the Client opens a dedicated data connection back to the Server, and bytes are relayed in both directions — including WebSocket upgrades, once established.

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Node.js + npm (only needed if you're changing the dashboard — its build output isn't committed; see [Dashboard](#dashboard) below)

## Getting started

### 1. Run the Server

The Server is the piece that needs a public IP/domain and needs to keep running. For local development you can just run it on your own machine:

```powershell
cd src/Portix.Server
dotnet run
```

By default it listens on:
- **5100** — control/data channel (HTTP/2, used by clients)
- **8080** — public HTTP listener (where tunneled traffic arrives)
- **5101** — admin API + Swagger UI (only opened if `Portix:AdminToken` is configured)

Configure it in `src/Portix.Server/appsettings.json` (or via `Portix__<Key>` environment variables):

| Key | Meaning |
|---|---|
| `Portix:ControlPort` | Port clients connect to for the control channel. |
| `Portix:PublicPort` | Port public tunnel traffic arrives on. |
| `Portix:AdminPort` | Port for the admin API (see below). |
| `Portix:PublicHostSuffix` | The domain tunnels are assigned under, e.g. `tunnel.example.com` → a tunnel gets `abc123.tunnel.example.com`. |
| `Portix:PublicUrlScheme` / `Portix:PublicUrlPort` | Scheme/port advertised in generated public URLs — set these if a reverse proxy (nginx, Caddy) terminates TLS in front of the Server on a different port than `PublicPort`. |
| `Portix:AdminToken` | Bearer token that gates the admin API. Leave empty to disable the admin API entirely. |
| `Portix:Database:Path` | SQLite file path for users/tokens/plans. |

The Server needs at least one user + API token before a client can connect — see [Admin API](#admin-api).

### 2. Run the Client and open a tunnel

```powershell
cd src/Portix.Client
dotnet run -- login <your-api-token> --server http://your-server:5100
```

Then, from anywhere (the CLI daemon runs itself in the background):

```powershell
dotnet run -- http 3000
```

or, once published (see [Building a standalone executable](#building-a-standalone-executable)):

```powershell
portix http 3000
portix https 3000   # if your local app only serves HTTPS
```

This prints a live status panel and a colored, continuously-updating table of requests as they come through the tunnel — press <kbd>Ctrl+C</kbd> to close it.

## CLI reference

| Command | Description |
|---|---|
| `portix http <port> [--subdomain <name>] [--api <url>]` | Expose a local HTTP port through a public tunnel. |
| `portix https <port> [--subdomain <name>] [--api <url>]` | Expose a local HTTPS port through a public tunnel (certificate validation is skipped for this loopback-only connection). |
| `portix ls [--api <url>]` | List currently open tunnels. |
| `portix rm <id> [--api <url>]` | Close a tunnel by id. |
| `portix login <token> [--server <url>]` | Save a personal API token for this machine. |
| `portix logout` | Remove the saved API token. |
| `portix --help` / `portix -h` | Show usage. Also shown for a bare `portix` or an invalid command — nothing runs without valid syntax. |

`--api` points at an already-running daemon's local API (default `http://127.0.0.1:4040`); omit it and `http`/`https` will start a private, isolated daemon for that invocation instead.

On Windows, double-clicking `portix.exe` (rather than running it from a terminal) opens a persistent console positioned in the executable's own folder, ready to type a command into.

Configure the Client in `src/Portix.Client/appsettings.json` (or `Portix__<Key>` environment variables) — `Portix:ServerUrl`, `Portix:LocalApiPort` (the local dashboard/API port, default 4040), `Portix:Token`. A token saved via `portix login` is persisted to `%APPDATA%/Portix/config.json` (or the OS equivalent) and always takes precedence over `appsettings.json`.

## Dashboard

Each running daemon serves a web dashboard at its local API URL (`http://127.0.0.1:4040` by default) with:

- A tunnel sidebar (status, local endpoint, copy/open the public URL, disconnect).
- A searchable, filterable request list (by method, by status class), color-coded by status and duration.
- A request detail panel: headers as a table (sensitive values like `Authorization`/`Cookie` masked by default, with a reveal toggle and a copy-all action), a JSON body viewer (copy, expand/collapse all, search), and a **Replay** button to resend a captured request to your local app.

To build the dashboard from source:

```powershell
cd dashboard
npm install
npm run build
```

The build output goes to `src/Portix.Client/wwwroot`, which the Client serves (and, for a published single-file executable, embeds directly into the binary — see below).

## Admin API

The Server's admin API (`/admin/*`, only enabled when `Portix:AdminToken` is set) manages the accounts that are allowed to connect. Every request needs `Authorization: Bearer <AdminToken>`. When enabled, Swagger UI is available at `http://<server>:<AdminPort>/swagger`.

| Method & path | Purpose |
|---|---|
| `POST /admin/users` | Create a user (and issue their first token). |
| `GET /admin/users` | List all users, with plan, disabled status, and active tunnel count. |
| `GET /admin/users/{id}` | Look up one user. |
| `PUT /admin/users/{id}/status` | Enable/disable a user. |
| `PUT /admin/users/{id}/plan` | Change a user's plan (governs max concurrent tunnels). |
| `DELETE /admin/users/{id}` | Delete a user (and their tokens; disconnects any active session). |
| `POST /admin/users/{id}/tokens` | Issue an additional token for a user. |
| `GET /admin/users/{id}/tokens` | List a user's tokens (metadata only — never the raw value). |
| `DELETE /admin/tokens/{id}` | Revoke a token. |
| `GET /admin/plans` | List available plans. |
| `GET /admin/sessions` | List currently connected sessions and their tunnels. |
| `DELETE /admin/sessions/{id}` | Force-disconnect a session. |

A fresh database seeds two plans: **Free** (1 concurrent tunnel) and **Pro** (5).

## Building a standalone executable

```powershell
./scripts/publish-portix-client.ps1
```

Publishes the Client as a single, self-contained executable for Windows, macOS (Intel), and macOS (Apple Silicon) — no .NET runtime install required on the target machine, and the dashboard's assets are compiled into the binary itself. Output goes to `publish/portix-client-<rid>/`.

```powershell
./scripts/publish-portix-client.ps1 -Rid win-x64        # just one platform
./scripts/publish-portix-client.ps1 -Rid osx-arm64
```

Cross-compiling (e.g. building the macOS binaries from Windows) works out of the box — no native toolchain needed.

## Project layout

```
src/
  Portix.Server/   the public relay + admin API
  Portix.Client/    the CLI + local daemon + dashboard host
  Portix.Shared/    wire protocol shared by both
dashboard/          the React traffic-inspector SPA
scripts/            publish/tooling scripts
openspec/           change proposals (this project uses a spec-driven workflow for changes)
```
