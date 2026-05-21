#!/usr/bin/env bash
set -euo pipefail

# ------------------------------------------------------------------
#  sync-references.sh
#  Synkroniserer dine lokale referencer fra
#      ~/Documents/Code2Web/references/
#  ind i repo'ets
#      references-shipped/
#  saa de kan committes og foelger med koden.
#
#  Koer scriptet naar du har forfinet referencer (sat 's', 'i' osv.)
#  og er klar til at distribuere erfaringerne til kolleger.
# ------------------------------------------------------------------

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOCAL_REFS="$HOME/Documents/Code2Web/references"
REPO_REFS="$SCRIPT_DIR/references-shipped"

if [ ! -d "$LOCAL_REFS" ]; then
    echo "Ingen lokale referencer fundet i: $LOCAL_REFS"
    exit 1
fi

echo "Synkroniserer referencer:"
echo "  fra : $LOCAL_REFS"
echo "  til : $REPO_REFS"
echo

# Genskab repo-mappen fra bunden, saa slettede/omdoebte referencer ogsaa
# forsvinder fra repoet - ellers samler det vraggods over tid.
rm -rf "$REPO_REFS"
mkdir -p "$REPO_REFS"

# Kopier kun .txt-filer (referencer + mapping); ignorer andet skidt.
count=0
for f in "$LOCAL_REFS"/*.txt; do
    [ -e "$f" ] || continue
    cp "$f" "$REPO_REFS/"
    count=$((count + 1))
done

echo "$count fil(er) kopieret."
echo
echo "Husk at committe ændringerne hvis du er tilfreds:"
echo "  git add references-shipped/"
echo "  git commit -m 'Update shipped references'"
