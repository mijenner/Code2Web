#!/usr/bin/env bash
set -euo pipefail

RUNTIME="${1:-osx-x64}"
CONFIG="${2:-Release}"
SKIP_REFERENCES="${SKIP_REFERENCES:-0}"

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
    -p:AssemblyName=cliCode2Web-x64 \
    --self-contained true \
    -o "$DEST"

# Shipped references: kopier fra repoets references-shipped/ ind ved siden af
# binaeren. Saadan er publish reproducerbar uanset hvilken maskine du bygger fra.
# Koer sync-references.sh foerst for at opdatere references-shipped/ fra dine
# lokale referencer i ~/Documents/Code2Web/references/.
if [ "$SKIP_REFERENCES" != "1" ]; then
    SHIPPED_SRC="$SCRIPT_DIR/references-shipped"
    if [ -d "$SHIPPED_SRC" ]; then
        SHIPPED_DST="$DEST/references"
        echo
        echo "Kopierer shipped references:"
        echo "  fra : $SHIPPED_SRC"
        echo "  til : $SHIPPED_DST"

        rm -rf "$SHIPPED_DST"
        mkdir -p "$SHIPPED_DST"

        find "$SHIPPED_SRC" -maxdepth 1 -type f -name '*.txt' -exec cp {} "$SHIPPED_DST/" \;

        count=$(find "$SHIPPED_DST" -maxdepth 1 -type f -name '*.txt' | wc -l | tr -d ' ')
        echo "  ($count fil(er) kopieret)"
    else
        echo
        echo "INFO: references-shipped/ blev ikke fundet i repoet."
        echo "      Koer sync-references.sh foerst hvis du vil have referencer"
        echo "      med i builden. Brug SKIP_REFERENCES=1 for at undertrykke denne besked."
    fi
fi

echo
echo "   Publish complete."
echo "   Files are in: $DEST"
