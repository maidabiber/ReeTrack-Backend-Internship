terraform {
  required_version = ">= 1.6"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.60"
    }
  }
  # For a demo, local state is fine (it holds the DB password + secrets, so keep it out of git —
  # see .gitignore). Production-grade upgrade: an encrypted S3 backend + DynamoDB lock.
  # backend "s3" {}
}

provider "aws" {
  region = var.region
  default_tags {
    tags = {
      Project   = var.project
      ManagedBy = "terraform"
    }
  }
}

locals {
  domain = "${var.duckdns_subdomain}.duckdns.org"
}

# --- Networking: reuse the default VPC/subnets to keep the demo simple -------
data "aws_vpc" "default" {
  default = true
}

data "aws_subnets" "default" {
  filter {
    name   = "vpc-id"
    values = [data.aws_vpc.default.id]
  }
}

# --- Latest Ubuntu 24.04 LTS (Noble) AMI, published by Canonical --------------
data "aws_ami" "ubuntu" {
  most_recent = true
  owners      = ["099720109477"] # Canonical

  filter {
    name   = "name"
    values = ["ubuntu/images/hvm-ssd*/ubuntu-noble-24.04-amd64-server-*"]
  }

  filter {
    name   = "virtualization-type"
    values = ["hvm"]
  }
}
