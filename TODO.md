# Email Labeler Roadmap

## Port the Service from C# to Python and Integrate with Asgard Infra

**Goal**: replace the current .NET implementation with a production-ready Python service while preserving existing behavior, configuration, Gmail/Pub/Sub integration, health checks, observability, and test coverage. Restructure the repository so application delivery is managed by `asgard-infra`.

**Deliverables**:
- [ ] Document the current behavior and define parity criteria before replacing the C# implementation.
- [ ] Select and document the Python runtime, web framework, dependency manager, linting, formatting, type-checking, and testing tools.
- [ ] Port configuration loading, the rule engine, email actions, Gmail integration, Pub/Sub authentication and webhook handling, watch renewal, health checks, and Sentry integration.
- [ ] Port the unit and integration test suites and retain equivalent coverage and quality gates.
- [ ] Replace the .NET build, local-development commands, Docker image, CI workflow, documentation, and pre-push checks with their Python equivalents.
- [ ] Publish immutable, `linux/amd64`-compatible application images to GHCR; do not deploy production directly from this repository.
- [ ] Define the runtime contract needed by `asgard-infra`: image name and tag, container port, health endpoints, environment variables, secrets, resource needs, and rollout/rollback behavior.
- [ ] Move production deployment ownership to `asgard-infra/apps/email-labler`, including Kubernetes configuration, secrets documentation, Service, Ingress, TLS, probes, and operational instructions.
- [ ] Validate the Python image through `asgard-infra` in a non-production deployment, then verify Gmail labeling, archiving, watch renewal, health reporting, and rollback before cutover.
- [ ] Remove the obsolete C# projects and legacy repository-owned production deployment only after parity and infrastructure-managed deployment are confirmed.

## TLS Certificate Renewal

**TODO**: reload nginx after Certbot renews a certificate. The current Certbot loop updates the certificate files in the shared volume, but nginx continues serving the previously loaded certificate until it is restarted or reloaded. Also fix `task cert:issue` so it overrides the Certbot service's looping entrypoint when running one-off commands.

## Ansible Server Provisioning

**Goal**: idempotent playbook that converges a fresh Ubuntu droplet into a running production server. Replaces the manual setup steps in the README.

## Terraform: DigitalOcean

**Goal**: reproducible droplet + firewall + DNS.

## Terraform: GCP

**Goal**: reproducible Pub/Sub + IAM. Tear down current GCP resources, re-create via TF.
