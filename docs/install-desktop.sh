#!/bin/sh
set -eu
package_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
applications=${XDG_DATA_HOME:-"$HOME/.local/share"}/applications
mkdir -p "$applications"
escaped=$(printf '%s' "$package_root" | sed 's/\\/\\\\/g; s/"/\\"/g; s/`/\\`/g; s/\$/\\$/g; s/%/%%/g')
cat > "$applications/rvz-converter.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=RVZ Converter
Comment=Convert Wii and GameCube disc images
Exec="$escaped/RVZConverter" %F
Icon=$package_root/docs/App.svg
Terminal=false
Categories=Utility;
EOF
printf 'Installed user desktop entry. Keep the portable package at its current path.\n'
