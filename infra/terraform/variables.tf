# --- Core ------------------------------------------------------------------
variable "region" {
  description = "AWS region"
  type        = string
  default     = "eu-central-1"
}

variable "project" {
  description = "Name prefix for all resources"
  type        = string
  default     = "reetrack"
}

variable "instance_type" {
  description = "EC2 instance type for the app box"
  type        = string
  default     = "t3.small"
}

# --- Database (PostgreSQL container on the box) -----------------------------
variable "db_name" {
  description = "PostgreSQL database name"
  type        = string
  default     = "reetrack"
}

variable "db_username" {
  description = "RDS master username"
  type        = string
  default     = "reetrack_admin"
}

variable "db_password" {
  description = "PostgreSQL password (used for the db container and the app connection string)"
  type        = string
  sensitive   = true
}

# --- SSH (admin + CI deploy) -----------------------------------------------
variable "ssh_public_key" {
  description = "SSH public key installed on the box for the ubuntu user. Generate a keypair locally (ssh-keygen) and paste the .pub contents here; CI and you use the matching private key."
  type        = string
}

variable "ssh_ingress_cidr" {
  description = "CIDRs allowed to reach SSH (port 22). Defaults to anywhere because GitHub-hosted runners have no fixed IPs; access is key-only. Narrow to your IP (e.g. [\"1.2.3.4/32\"]) if you deploy manually."
  type        = list(string)
  default     = ["0.0.0.0/0"]
}

# --- DuckDNS ---------------------------------------------------------------
variable "duckdns_subdomain" {
  description = "DuckDNS subdomain (the part before .duckdns.org)"
  type        = string
}

variable "duckdns_token" {
  description = "DuckDNS account token"
  type        = string
  sensitive   = true
}

# --- GHCR (box pulls private images) ---------------------------------------
variable "ghcr_username" {
  description = "GitHub username for GHCR login"
  type        = string
}

variable "ghcr_read_token" {
  description = "GitHub PAT with read:packages"
  type        = string
  sensitive   = true
}

# --- Google OAuth ----------------------------------------------------------
variable "google_client_id" {
  description = "Google OAuth client ID"
  type        = string
}

variable "google_client_secret" {
  description = "Google OAuth client secret"
  type        = string
  sensitive   = true
}

variable "google_admin_email" {
  description = "Optional: restrict first-admin signup to this email"
  type        = string
  default     = ""
}

# --- JWT -------------------------------------------------------------------
variable "jwt_signing_key" {
  description = "HMAC signing key for session JWTs (>= 32 chars)"
  type        = string
  sensitive   = true

  validation {
    condition     = length(var.jwt_signing_key) >= 32
    error_message = "jwt_signing_key must be at least 32 characters."
  }
}

# --- Optional integrations (leave default to disable) ----------------------
variable "jira_site_url" {
  type    = string
  default = ""
}
variable "jira_email" {
  type    = string
  default = ""
}
variable "jira_api_token" {
  type      = string
  default   = ""
  sensitive = true
}
variable "jira_webhook_secret" {
  description = "Shared secret verifying inbound Jira webhook calls (per the webhook PR)"
  type        = string
  default     = ""
  sensitive   = true
}

variable "llm_api_key" {
  type      = string
  default   = ""
  sensitive = true
}

variable "slack_bot_token" {
  type      = string
  default   = ""
  sensitive = true
}

variable "smtp_host" {
  type    = string
  default = ""
}
variable "smtp_port" {
  type    = string
  default = "587"
}
variable "smtp_username" {
  type    = string
  default = ""
}
variable "smtp_password" {
  type      = string
  default   = ""
  sensitive = true
}
variable "smtp_from" {
  type    = string
  default = "ReeTrack <noreply@example.com>"
}

variable "invitation_domain_0" {
  type    = string
  default = "reeinvent.com"
}
variable "invitation_domain_1" {
  type    = string
  default = "etf.unsa.ba"
}
