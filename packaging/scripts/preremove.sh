#!/bin/sh
# Runs before removal on both deb (prerm) and rpm (%preun).
# deb passes "remove" / "upgrade" / "purge"; rpm passes 0 (final removal) or 1 (upgrade).
# Only tear the service down on a real removal, never on an upgrade.
set -e

if [ "$1" = "remove" ] || [ "$1" = "purge" ] || [ "$1" = "0" ]; then
    if command -v systemctl >/dev/null 2>&1; then
        systemctl disable --now desomnia.service >/dev/null 2>&1 || true
    fi
fi

exit 0
