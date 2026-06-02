# Email Labeler

A self-hosted Gmail webhook service that automatically labels and archives incoming emails based on configurable rules. It receives Gmail push notifications via Google Cloud Pub/Sub and applies actions (label, archive) defined in `config.yaml`.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker](https://docs.docker.com/get-docker/) and Docker Compose
- [Task](https://taskfile.dev/) (`brew install go-task`)
- [ngrok](https://ngrok.com/) (local dev only — `brew install ngrok/ngrok/ngrok`)

## Quick Start

1. Clone the repository
2. Copy `.env.example` to `.env` and fill in your credentials (see Environment Variables below)
3. Edit `config.yaml` with your labeling rules
4. Run `task dev` to start the full local environment (Docker + ngrok)
5. Copy the ngrok HTTPS URL and configure `https://<YOUR-URL>/labler` as your Pub/Sub push endpoint

Run `task setup:gmail` for a step-by-step setup guide.

### Gmail OAuth Token Setup

Before minting `GMAIL_REFRESH_TOKEN`, open the Google Cloud OAuth consent screen
and set the publishing status to **In production**. Refresh tokens for apps left
in **Testing** expire after about 7 days, which causes Gmail authentication to
fail with `invalid_grant`.

If the app logs `Gmail credentials rejected`, re-mint `GMAIL_REFRESH_TOKEN` after
publishing the consent screen to production, then restart the app with the new
token.

## Environment Variables

| Variable                    | Description                             | Required       |
| --------------------------- | --------------------------------------- | -------------- |
| `GMAIL_CLIENT_ID`           | OAuth 2.0 client ID                     | Yes            |
| `GMAIL_CLIENT_SECRET`       | OAuth 2.0 client secret                 | Yes            |
| `GMAIL_REFRESH_TOKEN`       | OAuth 2.0 refresh token                 | Yes            |
| `GMAIL_USER_EMAIL`          | Gmail address to monitor                | Yes            |
| `PUBSUB_SERVICE_ACCOUNT_EMAIL` | Pub/Sub OIDC service account email   | Yes (prod)     |
| `SENTRY_DSN`                | Sentry project DSN (errors + monitoring) | No (optional)  |
| `NGROK_AUTHTOKEN`           | ngrok authentication token              | Local dev only |

## Available Commands

| Command                 | Description                                       |
| ----------------------- | ------------------------------------------------- |
| `task build`            | Build the project                                 |
| `task run`              | Start the app locally via `dotnet run`            |
| `task dev`              | Start full local dev environment (Docker + ngrok) |
| `task ngrok`            | Start ngrok tunnel on port 80                     |
| `task docker:up`        | Start app + nginx via Docker Compose              |
| `task docker:down`      | Stop all containers                               |
| `task docker:build`     | Build the Docker image                            |
| `task lint`             | Check formatting (fails on diffs)                 |
| `task format`           | Apply formatting fixes                            |
| `task test:unit`        | Run unit tests                                    |
| `task test:integration` | Run integration tests (requires Docker)           |
| `task test:all`         | Run all tests                                     |
| `task test:coverage`    | Run unit tests with coverage (65% threshold)      |
| `task quality`          | Run full quality suite (lint, build, coverage)     |
| `task setup:gmail`      | Print Gmail/Pub/Sub setup instructions            |
| `task cert:issue`       | Issue a Let's Encrypt TLS certificate             |

## Architecture

```
Gmail ──push──> Cloud Pub/Sub ──POST /labler──> nginx ──proxy──> Email Labeler app
                                                 :80/:443          :5000 (internal)

┌──────────────────────────────────────────────────────────────┐
│  Email Labeler                                               │
│                                                              │
│  Endpoints/LablerEndpoints    ← receives Pub/Sub webhooks    │
│       │                                                      │
│       ▼                                                      │
│  Engine/EmailProcessor        ← evaluates rules, runs actions│
│       │                                                      │
│       ├── Engine/RuleEngine   ← matches emails against rules │
│       └── Actions/*           ← label, archive               │
│                                                              │
│  Ports/IEmailRepository       ← provider-agnostic port       │
│       │                                                      │
│       ▼                                                      │
│  Adapters/GmailRepository     ← Gmail API adapter            │
│                                                              │
│  Services/WatchRenewalService ← renews Gmail watch (6 days)  │
└──────────────────────────────────────────────────────────────┘
```

## config.yaml Format

Rules are defined in `config.yaml`. Each rule has a `match` block and a list of `actions`:

```yaml
rules:
  # Label all emails from a domain
  - match:
      from: "@newsletter.com"
    actions:
      - type: label
        label: "Newsletters"

  # Label and archive emails matching a subject pattern
  - match:
      subject: "weekly digest"
    actions:
      - type: label
        label: "Digests"
      - type: archive
```

**Match fields:**

- `from` — matches against the sender address (substring match)
- `subject` — matches against the email subject (substring match)

**Action types:**

- `label` — creates the label if needed and applies it to the email
- `archive` — removes the email from the inbox

## Production Deployment

Production deploys are handled by the GitHub Actions workflow in
`.github/workflows/deploy.yml`.

- Pushes to `main` deploy automatically after code quality, unit coverage, and
  integration tests pass.
- Manual deploys can be started from the workflow's `workflow_dispatch` trigger.
- Required GitHub Secrets are listed in `.github/workflows/README.md`.
- The workflow syncs the repo to `/opt/email-labler`, writes `.env` and
  `config.yaml` from secrets, runs `docker compose up -d --build`, and checks
  `https://${DOMAIN}/health`.

The existing droplet still needs Docker, Docker Compose, nginx/certbot volumes,
and the initial TLS certificate in place until the Ansible/Terraform phases
replace the manual server provisioning.

## Observability

Health and monitoring are layered so each failure mode has a distinct signal:

- **`GET /health`** — deep readiness check. Runs real dependency checks and returns
  `200` (healthy/degraded) or `503` (unhealthy) with a JSON per-check breakdown:
  - `gmail` — verifies Gmail API reachability and credential validity via
    `users.getProfile`.
  - `watch-renewal` — `Degraded` until the first renewal, then `Unhealthy` once the
    last successful renewal is older than `WatchRenewal:IntervalDays` +
    `WatchRenewal:HealthGraceHours` (defaults: 6 days + 12 hours).
- **`GET /health/live`** — liveness check. Runs no dependency checks, so it stays `200`
  whenever the process is serving HTTP. Used by the Docker `HEALTHCHECK` (and mirrored in
  `docker-compose.yml`) so a Gmail outage does not mark the container itself unhealthy.

### Alerting via Sentry (single service)

All external alerting is consolidated into one [Sentry](https://sentry.io) project (free
Developer plan: 1 uptime monitor + 1 cron monitor included). Set the `SENTRY_DSN` secret to
enable it; when unset, Sentry is disabled (no-op), so local and CI runs need no account.

Sentry covers three failure modes:

- **Errors** — the SDK captures `Error`-level logs and unhandled exceptions as events with
  stack traces, surfacing failures in `EmailProcessor` / `WatchRenewalService` that were
  previously only written to stdout. No extra code beyond initialization.
- **Uptime** — create a Sentry **Uptime Monitor** (in the dashboard) pointing at
  `https://${DOMAIN}/health`. Sentry checks it from its own infrastructure every minute and
  alerts after 3 consecutive failures — catching a whole-server/container/nginx outage that
  no in-app check could report.
- **Watch renewal** — the renewal loop sends a Sentry **cron check-in** each cycle (`ok` on
  success, `error` on failure) under the monitor slug `gmail-watch-renewal`, upserting a
  schedule that matches the renewal cadence. If a check-in is missed, Sentry alerts — catching
  a silently stalled renewal loop that uptime checks would miss while the web app stays up.

> Note: detecting a full server outage fundamentally requires an observer *outside* the box,
> which is why uptime monitoring is external (Sentry-hosted) rather than in-app.
