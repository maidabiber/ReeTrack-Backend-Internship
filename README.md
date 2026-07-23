# ReeTrack API

ASP.NET Core backend for **ReeTrack** — a single-tenant time tracking app (still under active development). This repo is the API + data layer. The SPA lives in [`reetrack-frontend`](https://github.com/reeinvent/reetrack-frontend).

---

## Stack

- **.NET 10** (`net10.0`)
- **ASP.NET Core** Web API + JWT Bearer auth
- **EF Core** + **PostgreSQL 16**
- **Scalar** OpenAPI UI (Development only)
- Solution layout: `Domain` → `Application` → `Infrastructure` → `Api`

```
backend/
├── ReeTrack.sln
├── .env.example          # copy → .env
├── src/
│   ├── docker-compose.yml   # Postgres only
│   ├── ReeTrack.Api/
│   ├── ReeTrack.Application/
│   ├── ReeTrack.Domain/
│   └── ReeTrack.Infrastructure/
└── tests/
    ├── ReeTrack.UnitTests/
    └── ReeTrack.IntegrationTests/
```

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://docs.docker.com/get-docker/) (for local Postgres)
- Google Cloud OAuth client (for real sign-in); optional SMTP for invite emails

---

## Quick start

### 1. Configure environment

```bash
cp .env.example .env
```

Edit `.env` at minimum:

- `ConnectionStrings__Default` — matches docker-compose defaults out of the box
- `Jwt__SigningKey` — long random secret (≥ 32 characters)
- `Google__ClientId` / `Google__ClientSecret` — from Google Cloud Console
- `Google__RedirectUri` — must match an Authorized redirect URI (dev default: `http://localhost:5173/api/auth/google/callback`)
- `Frontend__Origin` — SPA origin (default `http://localhost:5173`)

The API walks up from its bin folder looking for `.env` and loads it on startup (shell env vars still win).

### 2. Start Postgres

From the **repo root** (`reetrack-backend/`):

```bash
docker compose --env-file .env -f src/docker-compose.yml up -d
```

### 3. Run the API

```bash
cd src/ReeTrack.Api
dotnet run --launch-profile https
```

| Profile | URLs |
|---------|------|
| `https` (recommended with the SPA) | `https://localhost:7231` and `http://localhost:5042` |
| `http` | `http://localhost:5042` only |

On Development startup the API **applies EF migrations automatically**.

### 4. Smoke-check

```bash
curl -sk https://localhost:7231/api/health
# → {"status":"ok"}
```

- OpenAPI / Scalar (Development): `https://localhost:7231/scalar`
- Pair with the frontend: `npm run dev` in `reetrack-frontend` (Vite proxies `/api` → `https://localhost:7231`)

---

## Auth model (short)

1. **First run** — `GET /api/setup/status` reports first-run; the first Google user who signs in becomes Admin (optionally restricted by `Google__AdminEmail`).
2. **Invite-only after that** — Admins create invitations (`/api/invitations`); invitees sign in with Google.
3. **Allowed domains** — `Invitation__AllowedDomains__*` should match the Google Workspace domains you allow.
4. **Session** — JWT is stored in the `rt.session` cookie; the JWT Bearer handler also reads that cookie for browser requests.

Without Google credentials configured, sign-in will not work end-to-end. For local API probing you can mint a JWT matching `Jwt__*` settings and send `Authorization: Bearer <token>` (or set the `rt.session` cookie).

---

## Common API surfaces

All business routes are under `/api/...` and most require `[Authorize]`. Admin-only examples: members, invitations (mutations), audit logs.

| Prefix | Purpose |
|--------|---------|
| `/api/auth` | Google OAuth, `me`, logout |
| `/api/setup` | First-run status |
| `/api/time-entries` | Timer, manual/duration entries, sharing & approvals |
| `/api/clients` | Client directory |
| `/api/projects` | Projects (+ nested `/tasks`) |
| `/api/tags` | Workspace tags |
| `/api/members` | Team members (Admin) |
| `/api/invitations` | Invite / revoke (Admin) |
| `/api/teammates` | Teammate picker for sharing |
| `/api/calendar` | Calendar view / events |
| `/api/integrations/calendar` | Google Calendar OAuth + sync |
| `/api/audit-logs` | Audit trail (Admin) |
| `/api/health` | Liveness |

---

## Configuration reference

See [`.env.example`](.env.example) for the full list. Notable groups:

| Prefix | Used for |
|--------|----------|
| `ConnectionStrings__Default` | Npgsql |
| `Frontend__Origin` | CORS (credentials allowed) |
| `Google__*` | Sign-in + Calendar OAuth redirect URIs |
| `Jwt__*` | Token issuer / audience / signing / expiry |
| `Email__*` | SMTP for invites; empty `Email__SmtpHost` → log invite links to console |
| `Invitation__*` | Expiry + allowed email domains |
| `CalendarSync__*` | Background sync interval / lookback / lookahead |
| `App__Name` | Display name in emails / preview |

---

## Tests

```bash
# from repo root
dotnet test ReeTrack.sln
```

Integration tests use the `Testing` environment and an in-memory EF database.

---

## EF migrations

Migrations live in `src/ReeTrack.Infrastructure/Persistence/Migrations`.

Development applies them on API startup. To add a new migration:

```bash
dotnet ef migrations add <Name> \
  --project src/ReeTrack.Infrastructure \
  --startup-project src/ReeTrack.Api \
  --output-dir Persistence/Migrations
```

---

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| Build fails with MSB3021 (file locked) | Stop a stale API: `Get-Process ReeTrack.Api \| Stop-Process` (PowerShell) |
| Frontend API calls fail / CORS | Ensure API runs with `--launch-profile https` and `Frontend__Origin` matches the SPA |
| Postgres connection refused | Confirm compose is up and `POSTGRES_PORT` matches the connection string |
| Google redirect mismatch | Redirect URI in `.env` must exactly match Google Cloud Console |
| Invites created but user can't sign in | Invite email domain not in `Invitation__AllowedDomains__*` |

---

## Related

- Frontend: companion SPA (Vite + React) — run on `http://localhost:5173`
- Design / product notes live with the frontend (`design.md`) and the product backlog; this API implements the server side of those stories.
