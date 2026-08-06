output "app_url" {
  description = "Public URL of the deployed app"
  value       = "https://${local.domain}"
}

output "elastic_ip" {
  description = "Elastic IP of the app box. Set as the GitHub Actions variable DEPLOY_HOST (both repos), and SSH to it for admin."
  value       = aws_eip.this.public_ip
}

output "instance_id" {
  description = "EC2 instance ID"
  value       = aws_instance.this.id
}

output "ssh_hint" {
  description = "How to open a shell on the box (uses the private key matching ssh_public_key)"
  value       = "ssh ubuntu@${aws_eip.this.public_ip}"
}

output "google_redirect_uris" {
  description = "Register these exact URIs in the Google Cloud Console OAuth client"
  value = [
    "https://${local.domain}/api/auth/google/callback",
    "https://${local.domain}/api/integrations/calendar/google/callback",
  ]
}
