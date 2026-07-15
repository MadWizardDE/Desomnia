Constraints
+++++++++++

- It runs on 64-bit Linux only (``linux-x64`` and ``linux-arm64``) and requires *glibc 2.35 or newer* — Debian 12 "Bookworm", Ubuntu 22.04, Raspberry Pi OS (Bookworm), or later. For 32-bit systems, other architectures, or older systems, use the standard build.
- Plugins cannot be loaded at runtime; the Firewall Knock Operator is included, but other plugins are not available.

Everything else, including local sleep management via ``systemd-logind``, behaves as in the standard build.
