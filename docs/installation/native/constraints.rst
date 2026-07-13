Constraints
+++++++++++

- It runs only on ``linux-arm64`` and requires *glibc 2.35 or newer* — Debian 12 "Bookworm", Ubuntu 22.04, Raspberry Pi OS (Bookworm), or later. For 32-bit ARM, other architectures, or older systems, use the standard build.
- Plugins cannot be loaded at runtime; the Firewall Knock Operator is included, but other plugins are not available.
- It is intended for always-on monitor and proxy roles and does not manage the sleep of the device it runs on.
