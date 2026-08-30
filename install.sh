#!/usr/bin/env bash
# Builds DOSBox Selector and registers the "DOSBox Here" entry in Dolphin's context menu.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

install_dir="${XDG_DATA_HOME:-$HOME/.local/share}/dosbox-selector"
servicemenu_dir="${XDG_DATA_HOME:-$HOME/.local/share}/kio/servicemenus"

echo "==> Publishing to $install_dir"
rm -rf "$install_dir"
dotnet publish "$here/src/DosBoxSelector/DosBoxSelector.csproj" \
    --configuration Release \
    --output "$install_dir" \
    --nologo

chmod +x "$install_dir/DosBoxSelector"

echo "==> Installing service menu to $servicemenu_dir"
mkdir -p "$servicemenu_dir"
sed "s|@INSTALL_DIR@|$install_dir|g" \
    "$here/packaging/dosbox-here.desktop.in" \
    > "$servicemenu_dir/dosbox-here.desktop"

# KDE has refused to run service menus without the executable bit since Plasma 5.85.
chmod +x "$servicemenu_dir/dosbox-here.desktop"

# Harmless on Plasma 6, which mostly picks changes up on its own.
if command -v kbuildsycoca6 >/dev/null 2>&1; then
    kbuildsycoca6 --noincremental >/dev/null 2>&1 || true
fi

cat <<EOF

Installed.

  App:          $install_dir/DosBoxSelector
  Service menu: $servicemenu_dir/dosbox-here.desktop
  Flavors:      ${XDG_CONFIG_HOME:-$HOME/.config}/dosbox-selector/flavors.json

Restart Dolphin to pick up the new entry:

    killall dolphin

Then right-click empty space inside any folder and choose "DOSBox Here".
EOF
