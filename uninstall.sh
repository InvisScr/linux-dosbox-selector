#!/usr/bin/env bash
# Removes the app and the Dolphin context-menu entry. Leaves flavors.json alone.
set -euo pipefail

install_dir="${XDG_DATA_HOME:-$HOME/.local/share}/dosbox-selector"
servicemenu="${XDG_DATA_HOME:-$HOME/.local/share}/kio/servicemenus/dosbox-here.desktop"
config_dir="${XDG_CONFIG_HOME:-$HOME/.config}/dosbox-selector"

rm -f "$servicemenu"
rm -rf "$install_dir"

if command -v kbuildsycoca6 >/dev/null 2>&1; then
    kbuildsycoca6 --noincremental >/dev/null 2>&1 || true
fi

echo "Removed the app and the Dolphin entry."
echo "Your flavor list is still at $config_dir — delete it by hand if you want it gone."
