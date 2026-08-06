locals {
  # The full /opt/reetrack/.env rendered with real values (domain, DB creds, secrets). PostgreSQL
  # runs as the `db` container on this box, so the app connects to it over the compose network.
  env_file = templatefile("${path.module}/env.tftpl", {
    domain               = local.domain
    db_name              = var.db_name
    db_username          = var.db_username
    db_password          = var.db_password
    google_client_id     = var.google_client_id
    google_client_secret = var.google_client_secret
    google_admin_email   = var.google_admin_email
    jwt_signing_key      = var.jwt_signing_key
    jira_site_url        = var.jira_site_url
    jira_email           = var.jira_email
    jira_api_token       = var.jira_api_token
    jira_webhook_secret  = var.jira_webhook_secret
    llm_api_key          = var.llm_api_key
    slack_bot_token      = var.slack_bot_token
    smtp_host            = var.smtp_host
    smtp_port            = var.smtp_port
    smtp_username        = var.smtp_username
    smtp_password        = var.smtp_password
    smtp_from            = var.smtp_from
    invitation_domain_0  = var.invitation_domain_0
    invitation_domain_1  = var.invitation_domain_1
  })
}

# SSH key pair for admin + CI deploys. You supply the public key (ssh_public_key); AWS installs it
# for the default `ubuntu` user on the box. Keep the matching private key safe — CI needs it too.
resource "aws_key_pair" "this" {
  key_name   = "${var.project}-key"
  public_key = var.ssh_public_key
}

resource "aws_instance" "this" {
  ami                         = data.aws_ami.ubuntu.id
  instance_type               = var.instance_type
  subnet_id                   = data.aws_subnets.default.ids[0]
  vpc_security_group_ids      = [aws_security_group.ec2.id]
  key_name                    = aws_key_pair.this.key_name
  associate_public_ip_address = true

  metadata_options {
    http_tokens = "required" # enforce IMDSv2
  }

  root_block_device {
    volume_type = "gp3"
    volume_size = 20
    encrypted   = true
  }

  user_data = templatefile("${path.module}/user_data.sh.tftpl", {
    compose_file      = file("${path.module}/../../deploy/docker-compose.yml")
    caddyfile         = file("${path.module}/../../deploy/Caddyfile")
    env_file          = local.env_file
    ghcr_username     = var.ghcr_username
    ghcr_read_token   = var.ghcr_read_token
    duckdns_subdomain = var.duckdns_subdomain
    duckdns_token     = var.duckdns_token
    elastic_ip        = aws_eip.this.public_ip
  })

  # Re-provision the box when the bootstrap (compose/Caddyfile/env/user_data) changes.
  user_data_replace_on_change = true

  tags = { Name = "${var.project}-app" }
}

resource "aws_eip" "this" {
  domain = "vpc"
  tags   = { Name = "${var.project}-app" }
}

resource "aws_eip_association" "this" {
  instance_id   = aws_instance.this.id
  allocation_id = aws_eip.this.id
}
