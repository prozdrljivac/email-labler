# Email Labeler Roadmap

## TLS Certificate Renewal

**TODO**: reload nginx after Certbot renews a certificate. The current Certbot loop updates the certificate files in the shared volume, but nginx continues serving the previously loaded certificate until it is restarted or reloaded. Also fix `task cert:issue` so it overrides the Certbot service's looping entrypoint when running one-off commands.

## Ansible Server Provisioning

**Goal**: idempotent playbook that converges a fresh Ubuntu droplet into a running production server. Replaces the manual setup steps in the README.

## Terraform: DigitalOcean

**Goal**: reproducible droplet + firewall + DNS.

## Terraform: GCP

**Goal**: reproducible Pub/Sub + IAM. Tear down current GCP resources, re-create via TF.
