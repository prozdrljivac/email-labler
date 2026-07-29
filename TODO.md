# Email Labeler Roadmap

## Ansible Server Provisioning

**Goal**: idempotent playbook that converges a fresh Ubuntu droplet into a running production server. Replaces the manual setup steps in the README.

## Terraform: DigitalOcean

**Goal**: reproducible droplet + firewall + DNS.

## Terraform: GCP

**Goal**: reproducible Pub/Sub + IAM. Tear down current GCP resources, re-create via TF.
