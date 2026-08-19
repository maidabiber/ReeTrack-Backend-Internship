## About

ReeTrack is a time-tracking platform: people record what they work on,
their hours flow through a weekly review cycle, and the data turns into
reports, invoices and insights. This repo is the **backend** — an ASP.NET
Core API that owns all the data and business rules. The browser UI lives in
the companion `ReeTrack-Frontend-Internship` repo.

### What the API does

- **Identity** — sign in with Google; the first user becomes admin, everyone
  else joins via invite. Users hold one of three roles — **Admin**,
  **Project Manager** or **Member** — and each role brings its own set of
  permissions.
- **Time entries** — a running timer, manual entries, duration-based entries,
  and entries created by dragging Google Calendar events onto the timesheet.
- **Timesheet flow** — entries lock into weekly timesheets; members submit or
  withdraw them, and the week-lock guard prevents edits to already-approved
  periods. Admins get a review queue with approve / reject / send-back
  actions and decision emails.
- **AI assistant** — a chat assistant (LLM-backed, streaming) that turns a
  plain-language description of work into a draft time entry — or a whole
  week — resolving projects, tasks and tags along the way. Drafts stay drafts:
  the user confirms them in the UI before anything is saved. A second mode
  drafts new projects from a description.
- **NLP: smart parsing** — part of the NLP layer, a single free-form line of
  text gets parsed into structured entry fields (duration, project, task,
  tags, billable, times, date) with a confidence score, ready to drop into an
  entry form.
- **Workspace model** — clients, projects (with tasks, cost tracking and
  budget thresholds), tags and team members.
- **Rates & billing** — per-member billable rates with configurable
  multipliers, holiday handling, and invoice generation.
- **Reports** — portfolio summary, detailed, and workload/profitability
  reports with filtering and CSV/Excel/PDF export.
- **Custom reports** — a builder for user-defined reports, saved definitions,
  period comparison, AI-generated insights, and shareable report links.
- **Integrations** — Google Calendar sync and Jira (issue import plus
  webhook-driven updates).
- **Realtime** — SignalR hubs push updates to the UI as data changes.
- **Audit & safety** — full write/read history and soft delete on core
  entities, so nothing is ever permanently lost.

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
| `/api/integrations/jira` | Jira import / pull sync |
| `/api/webhooks/jira/events` | Inbound Jira webhooks (anonymous, HMAC) |
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
