#!/usr/bin/env pwsh
# One-click, idempotent deploy from the dev machine to the Ubuntu server over SSH:
#   build -> ship -> load & (re)start -> clean up.
#
# Safe to re-run. `docker compose up -d` reconciles to the desired state (only
# recreates what changed), and each run also:
#   - removes orphaned containers (services dropped from the compose file)
#   - prunes dangling images (the superseded brothermanbill image, etc.)
# Pass -Fresh to fully tear the stack down before bringing it up.
#
# Prompts for the server host and SSH user; remote path defaults below.
# Assumes key-based SSH (no password prompt).
#
# One-time server setup (NOT done by this script):
#   sudo mkdir -p /opt/brothermanbill
#   sudo chown "$USER:$USER" /opt/brothermanbill
#   printf 'DiscordBotToken=YOUR_TOKEN\n' > /opt/brothermanbill/.env && chmod 600 /opt/brothermanbill/.env
#   # Docker installed and your user in the 'docker' group.
# The .env (token) stays on the server; this script never sends it.

[CmdletBinding()]
param(
    [string]$Server,
    [string]$User,
    [string]$RemotePath = "/opt/brothermanbill",
    [switch]$Fresh      # `docker compose down` before `up` (clean restart)
)

$ErrorActionPreference = 'Stop'

if (-not $Server) { $Server = Read-Host "Server host (IP / hostname / ssh alias)" }
if (-not $User)   { $User   = Read-Host "SSH username" }
if (-not $Server -or -not $User) { throw "Server host and SSH username are required." }

$target = "${User}@${Server}"
$freshStep = if ($Fresh) { "docker compose down && " } else { "" }

Push-Location $PSScriptRoot
try {
    # 1. Build the image tarball (reuses build-image.ps1).
    Write-Host "==> Building image..." -ForegroundColor Cyan
    & "$PSScriptRoot/build-image.ps1"
    if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE)." }

    # 2. Ship the tarball + compose file + Lavalink config (relative paths avoid
    #    Windows scp treating a C:\ drive letter as a remote host).
    Write-Host "==> Copying files to ${target}:${RemotePath} ..." -ForegroundColor Cyan
    scp artifacts/brothermanbill.tar.gz docker-compose.yml BrothermanBill/Lavalink/application.yml "${target}:${RemotePath}/"
    if ($LASTEXITCODE -ne 0) { throw "scp failed (exit $LASTEXITCODE)." }

    # 3 & 4. Load, (re)start, and clean up on the server. Each step gates the next
    #    (&&), so any failure aborts and surfaces a non-zero exit. Idempotent:
    #    up -d reconciles state; --remove-orphans and image prune clean leftovers.
    #    `docker image prune -f` only removes dangling (untagged) images
    Write-Host "==> Deploying on the server..." -ForegroundColor Cyan
    $remote = "cd '$RemotePath' && test -f .env && docker load -i brothermanbill.tar.gz && ${freshStep}docker compose up -d --remove-orphans && docker image prune -f && rm -f brothermanbill.tar.gz && docker compose ps"
    ssh $target $remote
    if ($LASTEXITCODE -ne 0) { throw "Remote deploy failed (exit $LASTEXITCODE). If '.env' is missing in $RemotePath, create it (DiscordBotToken=...)." }

    # 5. Local cleanup.
    Write-Host "==> Cleaning up local tarball..." -ForegroundColor Cyan
    Remove-Item artifacts/brothermanbill.tar.gz -Force -ErrorAction SilentlyContinue

    Write-Host "`nDeployed to ${target}:${RemotePath}" -ForegroundColor Green
}
finally {
    Pop-Location
}
