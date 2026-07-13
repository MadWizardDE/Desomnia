#!/bin/sh
# Runs after install and after upgrade, on both deb (postinst) and rpm (%post).
set -e

if command -v systemctl >/dev/null 2>&1; then
    systemctl daemon-reload >/dev/null 2>&1 || true

    # Distinguish a first install from an upgrade across both packagers:
    #   deb: $1 = "configure"; $2 = previously configured version (empty on first install)
    #   rpm: $1 = 1 (first install) or 2 (upgrade)
    if { [ "$1" = "configure" ] && [ -z "$2" ]; } || [ "$1" = "1" ]; then
        # The package ships a working zero-configuration Sleep Proxy config, so Desomnia can run
        # right away: enable it at boot and start it now.
        systemctl enable --now desomnia.service >/dev/null 2>&1 || true
    elif systemctl is-active --quiet desomnia.service; then
        # Upgrade: restart to pick up the new binary, but only if it was already running.
        systemctl try-restart desomnia.service >/dev/null 2>&1 || true
    fi
fi

exit 0
