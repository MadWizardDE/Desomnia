#!/usr/bin/env bash
#
# NativeAOT publish of the Desomnia daemon (always-on / embedded build).
#
# Run this ON the target architecture (e.g. the Raspberry Pi, linux-arm64) — NativeAOT cannot
# cross-compile from Windows. The AOT prerequisites (clang, zlib1g-dev, an objcopy/ld toolchain)
# must be present; a prior `dotnet publish` on the Pi will already have pulled them in.
#
# -p:PublishAot=true also defines the DESOMNIA_AOT compile symbol across all projects (see
# Directory.Build.props), which excludes the AOT-incompatible paths: the D-Bus/logind suspend
# manager and runtime plugin loading. The sysfs-based SysPowerManager is used instead; it emits
# no power requests, so an always-on device never auto-suspends itself.
#
# Usage:  ./publish-aot.sh [output-dir]
#         RID=linux-arm ./publish-aot.sh      # override the runtime identifier
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJ="$SCRIPT_DIR/DesomniaDaemon.csproj"

RID="${RID:-linux-arm64}"
OUT="${1:-$SCRIPT_DIR/bin/aot-$RID}"

echo "Publishing NativeAOT ($RID) -> $OUT"
dotnet publish "$PROJ" \
    -c Release \
    -r "$RID" \
    -p:PublishAot=true \
    -o "$OUT"

# AssemblyName is 'desomniad' (see DesomniaDaemon.csproj).
BIN="$OUT/desomniad"
if [[ -x "$BIN" ]]; then
    echo
    echo "AOT binary: $BIN ($(du -h "$BIN" | cut -f1))"
    echo
    echo "Shared-library dependencies (should be just libc/libm/libgcc_s/libstdc++/libz + libpcap):"
    ldd "$BIN" 2>/dev/null || true
    echo
    echo "Run it as root, then measure resident memory:"
    echo "    sudo \"$BIN\""
    echo "    grep -E 'VmRSS|RssAnon|RssFile' /proc/\$(pidof desomniad)/status"
else
    echo "ERROR: expected native binary not found at $BIN" >&2
    exit 1
fi
