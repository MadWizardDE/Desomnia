#!/bin/sh
# Runs after install and after upgrade, on both deb (postinst) and rpm (%post).
set -e

CONFIG=/etc/desomnia/monitor.xml
TEMPLATE_PROXY=/usr/share/desomnia/monitor-proxy.xml
TEMPLATE_HOST=/usr/share/desomnia/monitor-host.xml

# A machine qualifies for local sleep management (instead of the always-on Sleep Proxy
# role) when it can actually suspend AND has a wired adapter that can wake it up again
# via Wake-on-LAN. Boxes that fail either test -- e.g. a Raspberry Pi, whose NIC may
# claim WoL support but which cannot suspend -- stay with the Sleep Proxy default.
host_can_sleep() {
    # Suspend-to-RAM or hibernation must be available.
    grep -qwE 'mem|disk' /sys/power/state 2>/dev/null || return 1

    # Without ethtool (a Recommends) the WoL capability cannot be probed; keep the default.
    command -v ethtool >/dev/null 2>&1 || return 1

    for dev in /sys/class/net/*; do
        [ -e "$dev/device" ] || continue        # physical interfaces only
        [ -e "$dev/wireless" ] && continue      # WoWLAN is not a wake path we set up
        if ethtool "${dev##*/}" 2>/dev/null | grep -q '^[[:space:]]*Supports Wake-on:.*g'; then
            return 0                            # 'g' = magic packet
        fi
    done
    return 1
}

# First install only: pick the default configuration matching the machine's role. The
# packaged monitor.xml is the zero-configuration Sleep Proxy (promiscuous watch) for
# always-on boxes; sleep-capable hosts get the local variant instead. The swap is done
# only while monitor.xml is still byte-identical to the pristine packaged default, so a
# configuration that already existed before this installation is never overwritten --
# and after the first installation the file is never touched again (upgrades skip this,
# and the package itself marks it config|noreplace).
choose_default_config() {
    [ -f "$TEMPLATE_HOST" ] && [ -f "$TEMPLATE_PROXY" ] || return 0
    command -v cmp >/dev/null 2>&1 || return 0
    cmp -s "$CONFIG" "$TEMPLATE_PROXY" || return 0

    if host_can_sleep; then
        cp "$TEMPLATE_HOST" "$CONFIG"
    fi
}

if command -v systemctl >/dev/null 2>&1; then
    systemctl daemon-reload >/dev/null 2>&1 || true

    # Distinguish a first install from an upgrade across both packagers:
    #   deb: $1 = "configure"; $2 = previously configured version (empty on first install)
    #   rpm: $1 = 1 (first install) or 2 (upgrade)
    if { [ "$1" = "configure" ] && [ -z "$2" ]; } || [ "$1" = "1" ]; then
        choose_default_config || true

        # Whichever role was chosen, the installed configuration is ready to run:
        # enable Desomnia at boot and start it now.
        systemctl enable --now desomnia.service >/dev/null 2>&1 || true
    elif systemctl is-active --quiet desomnia.service; then
        # Upgrade: restart to pick up the new binary, but only if it was already running.
        systemctl try-restart desomnia.service >/dev/null 2>&1 || true
    fi
fi

exit 0
