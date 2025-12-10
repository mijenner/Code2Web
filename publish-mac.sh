#!/usr/bin/env bash
set -euo pipefail

RUNTIME="${1:-osx-arm64}"  # standard: Apple Silicon
CONFIG="${2:-Release}"

# Find projektfilen ud fra scriptets placering
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_PATH="$SCRIPT_DIR/Code2Web/Code2Web.csproj"

DEST_ROOT="$HOME/cli"
DEST="$DEST_ROOT/Code2Web-$RUNTIME"

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
    --self-contained false \
    -o "$DEST"

echo
echo "   Publish complete."
echo "   Files are in: $DEST"
