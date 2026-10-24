#!/usr/bin/env bash
set -euo pipefail
root=$1
for engine in "$root/tools/dolphin/dolphin-tool" "$root/tools/wit/wit"; do
  file "$engine"
  lipo -info "$engine"
  otool -L "$engine" | tee -a "$root/native-dependencies.txt"
  minimum=$(otool -l "$engine" | awk '/LC_BUILD_VERSION/{build=1;next} build && /minos/{print $2;exit}')
  if [[ -z "$minimum" || ${minimum%%.*} -gt 14 ]]; then
    echo "Engine minimum macOS exceeds the declared 14.0 baseline: $minimum" >&2
    exit 1
  fi
  printf 'Minimum macOS: %s\n' "$minimum" | tee -a "$root/native-dependencies.txt"
  if otool -L "$engine" | tail -n +2 | grep -Ev '^\s*(/usr/lib/|/System/Library/|@executable_path/|@loader_path/|@rpath/)' | grep -q .; then
    echo 'Non-relocatable engine dependency detected.' >&2
    exit 1
  fi
done
