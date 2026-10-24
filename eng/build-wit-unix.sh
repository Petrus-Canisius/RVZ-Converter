#!/usr/bin/env bash
set -euo pipefail
source_dir=$1
destination=$2
script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
pwsh -NoProfile -File "$script_dir/prepare-wit-sha1.ps1" -SourceRoot "$source_dir/../.."
cd "$source_dir"
if [[ $(uname -s) == Darwin ]]; then
  export MACOSX_DEPLOYMENT_TARGET=14.0
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
make -j3 wit RVZ_MBEDTLS=1 HAVE_SHA=0 HAVE_ZLIB=1 XDEF=-DHAVE_ZLIB=1 XFLAGS="-fcommon -ffile-prefix-map=$source_dir=." "${linker_flags[@]}"
cc -std=gnu99 -Isrc -Irvz-mbedtls/include '-DMBEDTLS_CONFIG_FILE="crypto/rvz-sha1-config.h"' rvz-sha1-selftest.c src/rvz-sha1.c rvz-mbedtls/library/sha1.c rvz-mbedtls/library/platform_util.c -o rvz-sha1-selftest
./rvz-sha1-selftest
mkdir -p "$(dirname "$destination")"
cp wit "$destination"
chmod 755 "$destination"
