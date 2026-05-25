# Deployment Workflow

`deploy.yml` deploys production from `main` and can also be run manually with
`workflow_dispatch`.

## Required Secrets

| Secret | Purpose |
| --- | --- |
| `DROPLET_HOST` | Public host or IP address for the production droplet. |
| `DROPLET_USER` | SSH user with write access to `/opt/email-labler` and Docker permissions. |
| `DROPLET_SSH_KEY` | Private SSH key for the deploy user. |
| `DOMAIN` | Production domain used by the post-deploy health check. |
| `LETSENCRYPT_EMAIL` | Contact email for certificate automation. Kept in the inventory for Phase 3. |
| `GMAIL_CLIENT_ID` | Gmail OAuth client ID. |
| `GMAIL_CLIENT_SECRET` | Gmail OAuth client secret. |
| `GMAIL_REFRESH_TOKEN` | Gmail OAuth refresh token. |
| `GMAIL_USER_EMAIL` | Gmail mailbox address being watched. |
| `PUBSUB_SERVICE_ACCOUNT_EMAIL` | Pub/Sub push service account email expected in OIDC tokens. |
| `PUBSUB_TOPIC_NAME` | Gmail watch Pub/Sub topic name. |
| `CONFIG_YAML` | Full production `config.yaml` contents. |

## SSH Key Format

`DROPLET_SSH_KEY` must be an unencrypted private key for the deploy user. The
workflow accepts either raw multiline key text or a base64-encoded key.

To generate a dedicated deploy key:

```bash
ssh-keygen -t ed25519 -C "github-actions email-labler" -f email-labeler-deploy -N ""
```

Add `email-labeler-deploy.pub` to the droplet deploy user's
`~/.ssh/authorized_keys`.

For the GitHub environment secret, prefer a single-line base64 value:

```bash
base64 -i email-labeler-deploy | pbcopy
```

Paste that value into the `PROD` environment secret named `DROPLET_SSH_KEY`.

## Flow

1. Run the `code-quality` job:
   - restore packages
   - verify formatting with `dotnet format --verify-no-changes`
   - build with warnings-as-errors
2. Run the `tests` job:
   - restore packages and local tools
   - run unit tests with coverage
   - enforce the 65% line coverage threshold
   - verify Docker is available
   - run integration tests
3. Start the `deploy` job only after both previous jobs pass.
4. Configure SSH from `DROPLET_SSH_KEY`.
5. Sync the repo to `/opt/email-labler` with `rsync`.
6. Render `.env` and `config.yaml` from GitHub Secrets.
7. Run `docker compose up -d --build` on the droplet.
8. Poll `https://${DOMAIN}/health` for up to 60 seconds.

Deployments are serialized with the `deploy-prod` concurrency group, so two
production deploys never run at the same time.
