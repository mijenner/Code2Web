#!/usr/bin/env bash
set -euo pipefail

RUNTIME="${1:-osx-arm64}"
CONFIG="${2:-Release}"

# Mappen hvor scriptet ligger (repo-roden)
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Find .csproj (helst cliCode2Web.csproj, ellers første .csproj)
PROJECT_PATH="$(find "$SCRIPT_DIR" -name 'cliCode2Web.csproj' -print -quit)"

if [ -z "$PROJECT_PATH" ]; then
  PROJECT_PATH="$(find "$SCRIPT_DIR" -name '*.csproj' -print -quit)"
  if [ -z "$PROJECT_PATH" ]; then
    echo " Fandt ingen .csproj-filer under $SCRIPT_DIR"
    exit 1
  else
    echo " Flere .csproj fundet - bruger: $PROJECT_PATH"
  fi
fi

# Output-mappe: /Users/<user>/cli
DEST="$HOME/cli"

echo "Project      : $PROJECT_PATH"
echo "Runtime      : $RUNTIME"
echo "Configuration: $CONFIG"
echo "Output       : $DEST"
echo

mkdir -p "$DEST"

dotnet publish "$PROJECT_PATH" \
    -c "$CONFIG" \
    -r "$RUNTIME" \
    -p:PublishSingleFile=true \
    --self-contained true \
    -o "$DEST"

echo
echo "   Publish complete."
echo "   Files are in: $DEST"
