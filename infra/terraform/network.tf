# Security group for the app box: HTTP/HTTPS from the internet, plus SSH for admin access and
# CI-over-SSH deploys. PostgreSQL runs as a container on this same box (not exposed), so there is
# no separate database security group and no 5432 ingress.
resource "aws_security_group" "ec2" {
  name        = "${var.project}-ec2"
  description = "ReeTrack app box: HTTP/HTTPS + SSH in, all out"
  vpc_id      = data.aws_vpc.default.id

  ingress {
    description = "HTTP (Caddy + ACME HTTP-01 challenge)"
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  ingress {
    description = "HTTPS"
    from_port   = 443
    to_port     = 443
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  ingress {
    description = "HTTP/3 (QUIC)"
    from_port   = 443
    to_port     = 443
    protocol    = "udp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  # SSH for admin + automated CI deploys. GitHub-hosted runners have no fixed IP range, so
  # ssh_ingress_cidr defaults to 0.0.0.0/0; access is key-only (no password auth). Narrow it to
  # your own IP if you switch to manual deploys.
  ingress {
    description = "SSH (admin + CI deploy)"
    from_port   = 22
    to_port     = 22
    protocol    = "tcp"
    cidr_blocks = var.ssh_ingress_cidr
  }

  egress {
    description = "All outbound (GHCR, Google, Groq, Slack, Atlassian, Lets Encrypt)"
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = { Name = "${var.project}-ec2" }
}
