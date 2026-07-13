#!/bin/sh
# Runs after removal on both deb (postrm) and rpm (%postun).
set -e

if command -v systemctl >/dev/null 2>&1; then
    systemctl daemon-reload >/dev/null 2>&1 || true
fi

exit 0
