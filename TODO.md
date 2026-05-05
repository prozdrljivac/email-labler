# Deployment Prerequisites Checklist

Complete these steps before implementing the CI/CD deployment code.

## 1. Domain & DNS

- [ ] Have a domain (or subdomain) to use (e.g., `email.yourdomain.com`)
- [ ] Create a DNS **A record** pointing the domain to your droplet's IP address
- [ ] Wait for DNS propagation (can check with `dig yourdomain.com` or https://dnschecker.org)

## 2. Ansible Droplet Provisioning

Create an Ansible playbook at `infra/ansible/` to automate droplet setup:

- [ ] `inventory.yml` — Droplet IP + SSH connection config
- [ ] `playbook.yml` with tasks:
  - [ ] UFW firewall — allow SSH (22), HTTP (80), HTTPS (443)
  - [ ] Install go-task to `/usr/local/bin`
  - [ ] Create deploy directory (`/opt/email-labeler`)
  - [ ] Clone repo into deploy directory
  - [ ] Template `nginx.conf` with domain variable
  - [ ] Add GitHub Actions deploy SSH key to `authorized_keys`
  - [ ] Run `docker compose up -d --build`
  - [ ] Issue TLS certificate via certbot
  - [ ] Restart nginx with TLS config

## 3. SSH Key for GitHub Actions

- [ ] Generate a dedicated key pair:
  ```
  ssh-keygen -t ed25519 -C "github-actions" -f ~/.ssh/github-actions
  ```
- [ ] Add the **public key** to the droplet's `~/.ssh/authorized_keys`
- [ ] Save the **private key** contents — you'll paste it into GitHub Secrets later

## 4. Push Repository to GitHub

- [ ] Create a GitHub repository (if not already done)
- [ ] Push the codebase to GitHub

## 5. Configure GitHub Secrets

Go to **Repository → Settings → Secrets and variables → Actions** and add:

| Secret | Where to get it |
|--------|-----------------|
| `DROPLET_HOST` | Droplet IP from DigitalOcean dashboard |
| `DROPLET_USER` | `root` (or your deploy user) |
| `DROPLET_SSH_KEY` | Contents of `~/.ssh/github-actions` (private key from step 2) |
| `DOMAIN` | Your domain from step 1 |
| `LETSENCRYPT_EMAIL` | Your email for Let's Encrypt certificate notifications |
| `GMAIL_CLIENT_ID` | From your `.env` file (Google Cloud Console) |
| `GMAIL_CLIENT_SECRET` | From your `.env` file (Google Cloud Console) |
| `GMAIL_REFRESH_TOKEN` | From your `.env` file (OAuth flow) |
| `GMAIL_USER_EMAIL` | From your `.env` file |
| `PUBSUB_SERVICE_ACCOUNT_EMAIL` | From your `.env` file |
| `PUBSUB_TOPIC_NAME` | From your `.env` file (e.g., `projects/my-project/topics/gmail-labeler`) |
| `CONFIG_YAML` | Full contents of your `config.yaml` file (copy-paste the entire file) |

## 6. Update Google Cloud Pub/Sub

- [ ] Update your Pub/Sub **push subscription** endpoint URL from the dev/ngrok URL to:
  ```
  https://yourdomain.com/labler
  ```
  (Do this after the first successful deploy and TLS cert issuance)
