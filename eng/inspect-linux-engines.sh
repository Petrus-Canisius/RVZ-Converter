#!/usr/bin/env bash
set -euo pipefail
root=$1
sources=$2
# These are documented OS ABI dependencies, not copied from the build host.
for engine in "$root/tools/dolphin/dolphin-tool" "$root/tools/wit/wit"; do
  file "$engine"
  readelf -h "$engine"
  ldd "$engine" | tee -a "$root/native-dependencies.txt"
  if ldd "$engine" | grep -q 'not found'; then exit 1; fi
  readelf --version-info "$engine" | grep -o 'GLIBC_[0-9.]*' | sort -Vu | tail -1 | tee -a "$root/native-dependencies.txt"
done
# All non-glibc dependencies must resolve from the documented Ubuntu 24.04 packages.
# Upstream dependencies built by Dolphin's USE_SYSTEM_LIBS=OFF are static and their
# complete preferred sources are in the corresponding-source archive.
cp "$root/native-dependencies.txt" "$sources/linux-dependencies-$(uname -m).txt"
