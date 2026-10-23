#!/usr/bin/env bash
set -euo pipefail
source_dir=$1
destination=$2
cd "$source_dir"
if [[ $(uname -s) == Darwin ]]; then
  mkdir -p .build-tools
  ln -sf "$(command -v gawk)" .build-tools/awk
  export PATH="$PWD/.build-tools:$PATH"
  # WIT's packed pointer tables require classic relocations on Apple Silicon.
  linker_flags=(LDFLAGS=-Wl,-no_fixup_chains)
else
  linker_flags=()
fi
# GCC 10 changed common-symbol defaults. WIT 3.05a uses the earlier C convention.
export NO_FUSE=1
make -j3 wit HAVE_ZLIB=1 XDEF=-DHAVE_ZLIB=1 XFLAGS="-fcommon -ffile-prefix-map=$source_dir=. -Wno-error=implicit-function-declaration" "${linker_flags[@]}"
mkdir -p "$(dirname "$destination")"
cp wit "$destination"
chmod 755 "$destination"
