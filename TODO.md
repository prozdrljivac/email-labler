# Email Labeler Roadmap

In-repo automation work, listed in priority order. Each phase is self-contained — pick one up at a time. Execution order may differ from priority where dependencies require it (noted per phase).

## Phase 1 — Pre-push Git Hook for Code Quality

**Goal**: every `git push` runs format check, build (warnings-as-errors), unit tests, and a coverage gate **locally**. Bad code never reaches the remote.

**Deliverables**:
- `.config/dotnet-tools.json` — register Husky.NET as a local tool
- `.husky/task-runner.json` — tasks bound to the `pre-push` event
- `.husky/pre-push` — bootstrap script that invokes Husky.NET
- `.editorconfig` at repo root — .NET style rules consistent with `TreatWarningsAsErrors=true`
- `coverlet.runsettings` — XPlat Code Coverage config
- Add `coverlet.collector` package to both test projects
- Update `Taskfile.yml`: `task lint`, `task format`, `task test:coverage`, `task quality` (full local repro of the hook)

**Hook tasks (in order)**:
1. `dotnet format --verify-no-changes` — fail on diffs
2. `dotnet build --no-restore` — `TreatWarningsAsErrors=true` already enforced
3. `dotnet test tests/EmailLabeler.Unit.Tests` with coverage collection
4. Coverage threshold: minimum **70%** line coverage on the `EmailLabeler` project (excluding `Program.cs`). Tunable.

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
- `.github/workflows/deploy.yml` — triggers: `push` to `main` + `workflow_dispatch`
- `.github/workflows/README.md` — list of required secrets

**Workflow steps**:
1. Checkout
2. Setup .NET 10
3. Run integration tests — pre-deploy gate
4. Setup SSH using `DROPLET_SSH_KEY` secret
5. rsync repo to `/opt/email-labeler` on droplet
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

## Cross-cutting

- **Secrets**: production secrets live in (a) GitHub Secrets for the deploy workflow, (b) Ansible Vault for the droplet `.env`. Single source of truth: `infra/SECRETS.md` listing every secret + where it lives.
- **Docs**: each phase ships its own README. Top-level README gets an "Operations" section pointing at `infra/`.
- **Legacy cleanup**: drop the hardcoded domain in `docker/nginx.conf` in Phase 3; remove the manual deploy section of the README in Phase 2.
