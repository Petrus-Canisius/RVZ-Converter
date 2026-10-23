#!/usr/bin/env bash
set -euo pipefail
source_dir=$1
destination=$2
cd "$source_dir"
if [[ $(uname -s) == Darwin ]]; then
  mkdir -p .build-tools
  ln -sf "$(command -v gawk)" .build-tools/awk
  export PATH="$PWD/.build-tools:$PATH"
fi
# GCC 10 changed common-symbol defaults. WIT 3.05a uses the earlier C convention.
export NO_FUSE=1
make -j3 wit CFLAGS='-O2 -fcommon -std=gnu99 -Isrc/libwbfs -Isrc -Isrc/ui -I. -Iwork -Wno-error=implicit-function-declaration'
mkdir -p "$(dirname "$destination")"
cp wit "$destination"
chmod 755 "$destination"
