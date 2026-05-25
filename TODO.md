# Email Labeler Roadmap

In-repo automation work, listed in priority order. Each phase is self-contained — pick one up at a time. Execution order may differ from priority where dependencies require it (noted per phase).

## Phase 1 — Pre-push Git Hook for Code Quality

**Goal**: every `git push` runs format check, build (warnings-as-errors), unit tests, and a coverage gate **locally**. Bad code never reaches the remote.

**Deliverables**:
- [x] `.editorconfig` at repo root — .NET style rules consistent with `TreatWarningsAsErrors=true`
- [x] `coverlet.runsettings` — XPlat Code Coverage config
- [x] Add `coverlet.collector` package to both test projects
- [x] `.config/dotnet-tools.json` — register Husky.NET and ReportGenerator as local tools
- [x] `Directory.Build.targets` — auto-install hooks on first build
- [x] `.husky/task-runner.json` — tasks bound to the `pre-push` event
- [x] `.husky/pre-push` — bootstrap script that invokes Husky.NET
- [x] Update `Taskfile.yml`: `task lint`, `task format`, `task test:coverage`, `task quality` (full local repro of the hook)

**Hook tasks (in order)**:
1. `dotnet format --verify-no-changes` — fail on diffs
2. `dotnet build --no-restore` — `TreatWarningsAsErrors=true` already enforced
3. `dotnet test tests/EmailLabeler.Unit.Tests` with coverage collection
4. Coverage threshold: minimum **65%** line coverage on the `EmailLabeler` project (excluding `Program.cs`). Tunable.

**Design notes**:
- Unit tests only in the hook. Integration tests (Testcontainers + WireMock) are too slow for pre-push (30s–2min); they run in Phase 2's deploy workflow as a pre-deploy gate.
- Husky.NET auto-installs via `dotnet tool restore` — contributors don't need extra setup.
- Hook is bypassable with `git push --no-verify` (escape hatch).

**Verification**:
- Misformat a file → `git push` blocked at format step
- Introduce a warning → blocked at build step
- Drop coverage below threshold → blocked at coverage step
- `task quality` reproduces all three failures locally

## Phase 2 — CI/CD Deployment Workflow

**Goal**: push to `main` deploys to production automatically.

**Deliverables**:
- [x] `.github/workflows/deploy.yml` — triggers: `push` to `main` + `workflow_dispatch`
- [x] `.github/workflows/README.md` — list of required secrets

**Workflow steps**:
1. Checkout
2. Setup .NET 10
3. Run integration tests — pre-deploy gate
4. Setup SSH using `DROPLET_SSH_KEY` secret
5. rsync repo to `/opt/email-labler` on droplet
6. Write `.env` on droplet from secrets
7. `docker compose up -d --build` over SSH
8. Health check: poll `https://${DOMAIN}/health` with retries; fail workflow if not green within 60s

**Design notes**:
- `concurrency: { group: deploy-prod, cancel-in-progress: false }` — never overlap deploys.
- Targets the existing manually-provisioned droplet initially. No workflow changes needed once Phases 3+4 replace the droplet.
- Required secrets (already configured): `DROPLET_HOST`, `DROPLET_USER`, `DROPLET_SSH_KEY`, `DOMAIN`, `LETSENCRYPT_EMAIL`, `GMAIL_*`, `PUBSUB_*`, `CONFIG_YAML`.

**Verification**:
- `workflow_dispatch` on a no-op commit succeeds with green health check
- A real change pushed to `main` shows up live within ~2 min
- A broken integration test correctly fails the workflow before deploy

## Phase 3 — Ansible Server Provisioning

**Goal**: idempotent playbook that converges a fresh Ubuntu droplet into a running production server. Replaces the manual setup steps in the README.

**Dependencies**: best paired with Phase 4 (a fresh droplet to converge against).

**Deliverables**:
- `infra/ansible/inventory.yml` — droplet IP, SSH user, key path (IP from Phase 4 TF output)
- `infra/ansible/group_vars/all.yml` — `domain`, `letsencrypt_email`, paths
- `infra/ansible/group_vars/all/vault.yml` — Ansible Vault-encrypted secrets (Gmail, Pub/Sub, CONFIG_YAML)
- `infra/ansible/playbook.yml`:
  1. `apt update` + base packages
  2. UFW: allow 22, 80, 443; enable
  3. Install Docker + Docker Compose plugin
  4. Install go-task to `/usr/local/bin`
  5. Create `/opt/email-labeler`
  6. Sync repo to `/opt/email-labeler`
  7. Render `.env` from vault secrets
  8. Add GH Actions deploy public key to `~/.ssh/authorized_keys`
  9. `docker compose up -d --build`
  10. Issue Let's Encrypt cert via the existing certbot service flow
  11. Reload nginx with TLS config
- `infra/ansible/README.md` — bootstrap usage
- Update `docker/nginx.conf` to use nginx's built-in `${DOMAIN}` env-var substitution (`/etc/nginx/templates/`). Drops the hardcoded `yourdomain.com`. No Ansible-side templating of nginx needed.
- Update `Taskfile.yml`: `task ansible:provision`

**Design notes**:
- Idempotent — re-running on a healthy server is a no-op.
- Secrets via Ansible Vault — encrypted file committed; vault password lives only in your password manager.
- Docker install in Ansible (not Terraform) — keeps TF about infrastructure, Ansible about software state.

**Verification**:
- `ansible-playbook playbook.yml --check` shows expected diff on a fresh droplet, no diff on a converged one
- After first apply: `curl https://${DOMAIN}/health` returns 200
- Re-running playbook produces `ok=N changed=0`

## Phase 4 — Terraform: DigitalOcean

**Goal**: reproducible droplet + firewall + DNS.

**Deliverables**:
- `infra/terraform/digitalocean/main.tf` — provider, droplet (size + region from vars), project assignment
- `infra/terraform/digitalocean/firewall.tf` — allow 22/80/443 inbound; all outbound
- `infra/terraform/digitalocean/dns.tf` — managed A record (flag-gated by `var.manage_dns`)
- `infra/terraform/digitalocean/variables.tf` — `region`, `size`, `ssh_key_fingerprints`, `domain`, `manage_dns`
- `infra/terraform/digitalocean/outputs.tf` — `droplet_ipv4` (feeds Ansible inventory)
- `infra/terraform/digitalocean/terraform.tfvars.example`
- `infra/terraform/digitalocean/README.md`

**Design notes**:
- Base Ubuntu image only — all software install happens in Ansible (Phase 3), not TF.
- SSH keys pre-uploaded to your DO account; TF references their fingerprints.
- State backend: local for v1, `.tfstate` in `.gitignore`. README documents future migration to DO Spaces (S3-compatible) backend.
- Cutover: TF apply → DNS update → Ansible provision → Phase 2 deploys land on the new droplet.

**Verification**:
- `terraform plan` shows droplet + firewall (+ DNS if enabled)
- `terraform apply` creates the droplet; `ssh root@<output_ip>` succeeds with your key
- DNS resolves the domain to the new IP

## Phase 5 — Terraform: GCP

**Goal**: reproducible Pub/Sub + IAM. Tear down current GCP resources, re-create via TF.

**Deliverables**:
- `infra/terraform/gcp/main.tf` — provider config
- `infra/terraform/gcp/pubsub.tf` — topic + push subscription (endpoint = `https://${var.domain}/labler`)
- `infra/terraform/gcp/iam.tf` — service account + `roles/pubsub.publisher` for Gmail's push identity
- `infra/terraform/gcp/variables.tf` — `project_id`, `domain`, `region`
- `infra/terraform/gcp/outputs.tf` — `service_account_email`, `topic_name`, `subscription_name`
- `infra/terraform/gcp/terraform.tfvars.example`
- `infra/terraform/gcp/README.md` — bootstrap, cutover sequence

**Design notes**:
- State backend: local for v1; future: GCS bucket backend.
- Cutover sequence: pause Gmail watch → `terraform destroy` old → `terraform apply` new → re-issue Gmail watch with new topic name.
- Push subscription endpoint uses `var.domain` — same domain as Phase 4.

**Verification**:
- `terraform plan` shows expected resources
- `terraform apply` succeeds; outputs populated
- `gcloud pubsub topics list` shows the new topic
- App receives push notifications end-to-end after cutover

## Phase 6 — Observability & Alerting

**Goal**: get notified when the app is down, when background jobs stop running, or when errors occur. Currently there is zero visibility into production health beyond SSH + `docker logs`.

**Problem areas to address**:
- The `/health` endpoint always returns 200 regardless of actual system state (e.g. expired Gmail credentials, unreachable API)
- `WatchRenewalService` runs every 6 days; if it fails silently, the Gmail watch expires after 7 days and the app stops receiving push notifications with no indication
- Errors in `EmailProcessor` and `WatchRenewalService` are caught and logged to stdout but nobody is watching stdout
- No Docker-level health checking — container can be stuck but Docker reports it as running
- No external monitoring — if the server, container, or nginx goes down, nobody knows

**Areas to investigate**:
- Replace the static `/health` with ASP.NET Core health checks that verify real dependencies (Gmail API connectivity, watch renewal recency)
- External uptime monitoring (e.g. UptimeRobot free tier) to ping `/health` and alert via email when it goes down
- Heartbeat/cron monitoring (e.g. Healthchecks.io free tier) for `WatchRenewalService` — the app pings out after each successful renewal; alert if the ping stops arriving
- Docker `HEALTHCHECK` in Dockerfile and `docker-compose.yml` so container health is visible in `docker ps`
- Whether error-level tracking (e.g. Sentry) is worth adding, or if uptime monitoring alone is sufficient

**Design notes**:
- Keep it proportionate to project scale — prefer free tiers and minimal new dependencies.
- This should be done before or alongside Phase 2, since the CI/CD health check polling (`curl /health`) is meaningless with the current static endpoint.
- Any new secrets (monitoring service URLs/tokens) should be added to the cross-cutting secrets inventory.

## Cross-cutting

- **Secrets**: production secrets live in (a) GitHub Secrets for the deploy workflow, (b) Ansible Vault for the droplet `.env`. Single source of truth: `infra/SECRETS.md` listing every secret + where it lives.
- **Docs**: each phase ships its own README. Top-level README gets an "Operations" section pointing at `infra/`.
- **Legacy cleanup**: drop the hardcoded domain in `docker/nginx.conf` in Phase 3; remove the manual deploy section of the README in Phase 2.

## Future Design — Pluggable email providers (one instance, one provider)

**Vision**: the labeler becomes provider-agnostic. A client picks a provider via an
`EMAIL_PROVIDER` env var (e.g. `gmail`), supplies that provider's env vars + rules
config, and gets a working labeler on a single stable endpoint `/labler`. To also
run Outlook, they start a **separate instance** with `EMAIL_PROVIDER=outlook` and
the Outlook env vars. One deployment = one provider; no runtime multiplexing, no
per-provider routes, no dispatcher.

**Problem with today's design**: the abstraction is half-leaky. Mailbox operations
(`GetEmailAsync`, `ApplyLabelAsync`, `ArchiveAsync`, `EnsureLabelExistsAsync`) are
genuinely portable, but `IEmailRepository` also carries Gmail-specific concerns —
`GetNewMessageIdsAsync(ulong historyId)` (Gmail's History cursor) and
`RenewWatchAsync()` (Gmail's "watch"). The inbound `/labler` handler is also welded
to Gmail: it model-binds `PubSubPushEnvelope`, its auth filter is hardcoded to
`PubSubTokenValidator` (OIDC bearer), and the provider is hardcoded via
`AddGmailIntegration()` in `Program.cs`. An Outlook adapter can't honestly implement
any of this (no `historyId`; Graph delivers the message id in the notification, uses
`clientState` auth + a validation handshake, and renews subscriptions via `PATCH`).

**Target shape**: keep `/labler` thin and provider-neutral; put everything
provider-specific behind three interfaces selected together at startup.

```
POST /labler  (single, stable, provider-agnostic)
  → INotificationGateway        (selected by EMAIL_PROVIDER)
       HandleAsync(HttpRequest) → authenticate + parse + resolve → messageIds
                                  (Gmail's historyId logic lives in here)
  → for each messageId: EmailProcessor.ProcessAsync(id)   ← shared core
       ├─ IEmailRepository.GetEmailAsync(id)               ← mailbox port (portable)
       ├─ RuleEngine.Evaluate(...)                          ← shared
       └─ IEmailAction.ExecuteAsync(...)                    ← shared
```

| Interface (provider-selected) | Responsibility | Gmail | Outlook (Graph) |
|---|---|---|---|
| `IEmailRepository` | get / label / archive / ensure-label | Gmail SDK | Graph SDK |
| `INotificationGateway` | inbound auth + parse → messageIds | Pub/Sub + OIDC + `history.list` | Graph notif + `clientState` + `resourceData.id` |
| `IPushSubscriptionManager` | `RenewAsync()` + `RenewInterval` | `users.watch()`, 7d | `PATCH expirationDateTime`, ~3d |

Shared/provider-agnostic: the `/labler` route, `EmailProcessor`, `RuleEngine`,
`IEmailAction` strategies, rules config, and `WatchRenewalService`.

**Deliverables**:
- [ ] Shrink `IEmailRepository` to the four portable mailbox operations; move
      `GetNewMessageIdsAsync` / `historyId` into the Gmail gateway as a private detail.
- [ ] New `INotificationGateway` owning inbound auth + payload parsing + resolution to
      message IDs. Move the OIDC-bearer filter logic into the Gmail implementation.
- [ ] `/labler` handler takes the raw `HttpRequest` (no `PubSubPushEnvelope` binding)
      and delegates to `INotificationGateway`.
- [ ] New `IPushSubscriptionManager` (`RenewAsync()` + `RenewInterval`);
      `WatchRenewalService` drives it at the provider's cadence (also retires the
      hardcoded 6-day interval).
- [ ] `AddEmailProvider(configuration)` reads `EMAIL_PROVIDER` and `switch`es to
      register the one provider's three impls + its config object. No keyed services
      (only one set is ever registered). `Program.cs` calls this instead of
      `AddGmailIntegration()`.
- [ ] Make config validation conditional on the selected provider — today
      `GmailConfigValidator` runs unconditionally at startup, so an Outlook deployment
      would refuse to boot on missing `GMAIL_*` vars.

**Design notes**:
- **Outlook validation handshake**: Graph echoes a `?validationToken=...` on
  subscription creation that must be returned as plain text within ~10s, and ongoing
  notifications carry a `clientState`. So `INotificationGateway.HandleAsync` must be
  able to return *either* a direct HTTP response (validation echo / 401) *or* a set of
  message IDs — e.g. a small `NotificationResult { IResult? ImmediateResponse;
  IEnumerable<string> MessageIds }`. Designing for this now means `/labler` won't need
  reshaping when Outlook lands.
- **Rule of three**: the current seam was shaped by a single implementation, so it
  looks generic but is molded to Gmail. Validate the redesign by actually stubbing the
  Outlook adapter — a second concrete provider is what reveals whether the boundary is
  right.
- Recommended first step (if/when picked up): refactor the existing app into this
  shape **Gmail-only** (keeping Gmail fully working), then add Outlook as a clean
  second provider.
