# ReeTrack — AWS deployment (Terraform)

Provisions the demo stack in one region:

- **EC2** `t3.small` (Ubuntu 24.04) + **Elastic IP** — runs Docker Compose (Caddy + web + api +
  **db** (PostgreSQL) + one-shot migrate) behind auto-HTTPS.
- **Security group** — 80/443 in, plus **SSH (22)** for admin and CI-over-SSH deploys.
- **SSH key pair** — you supply the public key; the private key is used by you and by CI.
- **DuckDNS** record pointed once at the (static) Elastic IP via `user_data` (no polling).
- `user_data` installs Docker, writes `/opt/reetrack/{docker-compose.yml,Caddyfile,.env}`, logs in to
  GHCR, drops `/opt/reetrack/update.sh`, and runs it once to bring the stack up.

No RDS, no IAM roles, no SSM — chosen so it runs under a restricted (non-IAM) AWS permission set.
PostgreSQL lives in a container on the box, with data in the `pgdata` Docker volume.

The compose file and Caddyfile come from `../../deploy/` (single source of truth).

## Order of operations

1. Generate an SSH keypair: `ssh-keygen -t ed25519 -f reetrack_deploy -C reetrack`
   (keep `reetrack_deploy`; paste `reetrack_deploy.pub` into tfvars).
2. `cp terraform.tfvars.example terraform.tfvars` and fill it in (incl. `ssh_public_key`).
3. `terraform init && terraform apply`
4. Note the outputs: `app_url`, `elastic_ip`, `instance_id`, `ssh_hint`, `google_redirect_uris`.
5. **In each repo** (backend + frontend), add under Settings → Secrets and variables → Actions:
   - **Variable** `DEPLOY_HOST` = the `elastic_ip` output
   - **Secret** `DEPLOY_SSH_KEY` = the contents of the **private** key (`reetrack_deploy`)
6. **Merge both repos to `master`.** CI builds/pushes the images, then SSHes in and deploys.

## Deploys (CI over SSH)

Pushing to `master` builds and pushes new `:latest` images to GHCR, then the workflow's `deploy` job
SSHes into the box (`ubuntu@$DEPLOY_HOST`, key auth) and runs `/opt/reetrack/update.sh`
(`docker compose pull` + `up -d`). Only containers whose image changed restart; the one-shot `migrate`
service re-runs (new EF migrations) before the api starts. `update.sh` uses `set -euo pipefail`, so a
failed deploy fails the Actions job. To deploy by hand:
```
ssh ubuntu@<elastic-ip> 'sudo /opt/reetrack/update.sh'
```

## Manual steps Terraform can't do

- **Google Cloud Console** → add the two `google_redirect_uris` output values as Authorized redirect
  URIs, add `https://<domain>` to Authorized JavaScript origins, and keep the consent screen in
  **Testing** with your teammates as test users.

## First-cert tip

DuckDNS may take a few minutes to converge to the Elastic IP on first boot; Caddy retries ACME
automatically until DNS resolves. To validate without risking Let's Encrypt rate limits, temporarily
uncomment the `acme_ca` staging line in `../../deploy/Caddyfile`, then switch back for a trusted cert.

## Admin access

SSH: `ssh ubuntu@<elastic-ip>`. Useful once on the box:
`cd /opt/reetrack && docker compose ps && docker compose logs -f caddy`.
Inspect the DB: `docker compose exec db psql -U <db_username> -d <db_name>`.

## Teardown

```
terraform destroy
```

Removes EC2, Elastic IP, security group, and key pair — stops all charges. **This also destroys the
database** (it lives in a Docker volume on the box). Back up first if you need the data:
`ssh ubuntu@<ip> 'cd /opt/reetrack && docker compose exec -T db pg_dump -U <user> <db>' > backup.sql`.

## Notes

- **Database durability:** PostgreSQL runs on the box with data in the `pgdata` volume. There are no
  managed backups or failover. Fine for a demo; take `pg_dump` snapshots (or an EBS snapshot) if the
  data matters.
- **SSH is open to the internet** (`0.0.0.0/0`) because GitHub-hosted runners have no fixed IPs.
  Access is key-only. If you switch to manual deploys, narrow `ssh_ingress_cidr` to your IP.
- Local state contains the DB password and secrets — it's gitignored. For a real project use an
  encrypted S3 backend (`backend "s3"` in `main.tf`).
- Secrets also land in the instance's `user_data`; that's inherent to bootstrapping a plaintext
  `.env`. The production-grade alternative is SSM Parameter Store / Secrets Manager fetched on boot.
