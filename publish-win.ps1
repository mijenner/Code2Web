param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

# Find projektfilen ud fra scriptets placering
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectPath = Join-Path $ScriptDir "Code2Web/Code2Web.csproj"

# Output-mappe, fx C:\Users\mje\cli\Code2Web-win-x64
$DestRoot = Join-Path $env:USERPROFILE "cli"
$Dest = Join-Path $DestRoot "Code2Web-$Runtime"

Write-Host "Project      : $ProjectPath"
Write-Host "Runtime      : $Runtime"
Write-Host "Configuration: $Configuration"
Write-Host "Output       : $Dest"
Write-Host ""

New-Item -ItemType Directory -Force -Path $Dest | Out-Null

dotnet publish $ProjectPath `
    -c $Configuration `
    -r $Runtime `
    -p:PublishSingleFile=true `
    --self-contained false `
    -o $Dest

Write-Host ""
Write-Host "   Publish complete."
Write-Host "   Files are in: $Dest"
