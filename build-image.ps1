#!/usr/bin/env pwsh
# Builds the BrothermanBill bot container image (linux-x64) as a loadable tarball.
# Output: ./artifacts/brothermanbill.tar.gz  ->  docker load -i brothermanbill.tar.gz
# Requires the .NET SDK only (no Docker needed to build).

$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot 'BrothermanBill/BrothermanBill.csproj'
$artifacts = Join-Path $PSScriptRoot 'artifacts'
$output = Join-Path $artifacts 'brothermanbill.tar.gz'

New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

dotnet publish $project `
    -c Release `
    --os linux `
    --arch x64 `
    /t:PublishContainer `
    -p:ExcludeLavalinkJar=true `
    -p:ContainerArchiveOutputPath=$output

if ($LASTEXITCODE -ne 0) { throw "Container publish failed with exit code $LASTEXITCODE" }

Write-Host "`nImage written to $output" -ForegroundColor Green
Write-Host "Ship it:  scp artifacts/brothermanbill.tar.gz you@server:/path/brothermanbill/" -ForegroundColor Green
