param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

# Mappen hvor scriptet ligger (repo-roden)
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Find .csproj (helst cliCode2Web.csproj, ellers første .csproj)
$projectFiles = Get-ChildItem -Path $ScriptDir -Filter *.csproj -Recurse

if ($projectFiles.Count -eq 0) {
    Write-Error " Fandt ingen .csproj-filer under $ScriptDir"
    exit 1
}

$project = $projectFiles | Where-Object { $_.Name -eq 'cliCode2Web.csproj' } | Select-Object -First 1
if (-not $project) {
    $project = $projectFiles | Select-Object -First 1
    Write-Host " Flere .csproj fundet - bruger: $($project.FullName)"
}

$ProjectPath = $project.FullName

# Output-mappe: C:\Users\<user>\cli
$Dest = Join-Path $env:USERPROFILE "cli"

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
    --self-contained true `
    -o $Dest

if ($LASTEXITCODE -ne 0) {
    Write-Error " dotnet publish failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "  Publish complete."
Write-Host "  Files are in: $Dest"
