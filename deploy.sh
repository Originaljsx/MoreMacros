#!/usr/bin/env bash
# Builds the plugin from WSL and copies it to a Windows folder that Dalamud can load as a dev plugin.
set -euo pipefail
cd "$(dirname "$0")"

WIN_APPDATA="${WIN_APPDATA:-/mnt/c/Users/$USER/AppData/Roaming}"
export PATH="$HOME/.dotnet:$PATH"
export DALAMUD_HOME="${DALAMUD_HOME:-$WIN_APPDATA/XIVLauncher/addon/Hooks/dev}"
DEST="${1:-$WIN_APPDATA/XIVLauncher/devPlugins/MoreMacros}"

dotnet build -c Release
mkdir -p "$DEST"
cp bin/Release/MoreMacros.dll bin/Release/MoreMacros.json bin/Release/MoreMacros.deps.json "$DEST/"
echo "Copied to $DEST"
echo "Dev plugin path for Dalamud: $(wslpath -w "$DEST")\\MoreMacros.dll"
