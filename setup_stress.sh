#!/bin/bash
set -e

### --- BASIC CONFIGURATION ---
APP_NAME="CacheStressTester"
APP_DIR="/home/ec2-user/$APP_NAME"
DOTNET_VERSION="8.0"
LOG_FILE="/var/log/${APP_NAME}_$(date +'%Y%m%d_%H%M%S').log"

# Execution parameters (can be modified or passed as env vars)
ENVIRONMENT="AWS"
THREADS=500
REQUESTS_PER_THREAD=20000
AWS_SECRET_ARN="arn:aws:secretsmanager:us-east-1:XXXXXXXXXXXX:secret:RedisConnectionString"
TAG="run1"

### --- HELPER FUNCTIONS ---
function log() {
  echo "[INFO] $1"
}

function check_root() {
  if [ "$EUID" -ne 0 ]; then
    log "This script requires root privileges. Please run with sudo."
    exit 1
  fi
}

function install_dotnet_amazon_linux() {
  if command -v dotnet >/dev/null 2>&1; then
    log ".NET SDK already installed: $(dotnet --version)"
    return
  fi

  log "Installing .NET SDK $DOTNET_VERSION..."
  if grep -q "Amazon Linux release 2" /etc/system-release; then
    rpm -Uvh https://packages.microsoft.com/config/amazon/2/packages-microsoft-prod.rpm
    yum install -y dotnet-sdk-$DOTNET_VERSION
  else
    dnf install -y dotnet-sdk-$DOTNET_VERSION
  fi
  log ".NET SDK installation completed successfully."
}

function deploy_app() {
  log "Creating destination directory: $APP_DIR"
  mkdir -p "$APP_DIR"

  if [ -d "./publish" ]; then
    log "Copying local binaries from ./publish"
    cp -r ./publish/* "$APP_DIR/"
  elif [ -n "$1" ]; then
    SRC=$1
    log "Downloading binaries from $SRC"
    aws s3 cp "$SRC" "$APP_DIR" --recursive
  else
    log "ERROR: No ./publish folder found and no S3 source provided."
    exit 1
  fi
}

function run_app() {
  log "Starting $APP_NAME..."
  cd "$APP_DIR"
  nohup dotnet "$APP_NAME.dll" \
    --Environment "$ENVIRONMENT" \
    --Threads "$THREADS" \
    --RequestsPerThread "$REQUESTS_PER_THREAD" \
    --AwsSecretArn "$AWS_SECRET_ARN" \
    --Tag "$TAG" \
    >> "$LOG_FILE" 2>&1 &
  log "Application launched successfully. Log file: $LOG_FILE"
}

### --- MAIN EXECUTION ---
check_root
install_dotnet_amazon_linux
deploy_app "$1"
run_app

log "Setup completed successfully."
