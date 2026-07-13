Native packages
===============

:OS: 🐧 *Linux*

Desomnia is distributed as native ``.deb`` and ``.rpm`` packages for the most common Linux
distributions. This is the recommended way to install it on a headless Linux server or
single-board computer: the package registers Desomnia as a systemd service, pulls in the
required system libraries, and follows the standard filesystem layout.

The packages contain the **native, ahead-of-time compiled build** — a single self-contained
binary that needs no .NET runtime and uses far less memory (around 48 MB instead of ~130 MB),
which makes it a good fit for an always-on :doc:`Wake-on-LAN proxy </guides/wol-proxy>`. See
:doc:`/modules/network/performance` for the reasoning.

.. note::

   Because this is the native build, two features are unavailable: runtime plugin loading (the
   :doc:`Firewall Knock Operator </plugins/fko>` is built in, but other plugins are not) and
   managing the sleep of the *host it runs on*. If you need either, install the full build via
   :doc:`Homebrew </installation/homebrew>`, the :doc:`archive </installation/manually>`, or
   :doc:`Docker </installation/docker>` instead.

Supported platforms
--------------------

Packages are published for the following architectures on the `GitHub Releases`_ page:

- ``amd64`` / ``x86_64`` — 64-bit Intel/AMD
- ``arm64`` / ``aarch64`` — 64-bit ARM, e.g. a Raspberry Pi 3/4/5 running a 64-bit OS

They require **glibc 2.35 or newer** — Debian 12 "Bookworm", Ubuntu 22.04, Raspberry Pi OS
(Bookworm), a current Fedora/openSUSE, or later. For 32-bit ARM, other architectures, or older
systems, use the :doc:`archive </installation/manually>` or :doc:`Docker </installation/docker>`
installation.

Installation
------------

Debian, Ubuntu, Raspberry Pi OS
+++++++++++++++++++++++++++++++

Download the ``.deb`` matching your architecture from the `GitHub Releases`_ page and install it
with ``apt``, which resolves the required system libraries automatically:

.. code:: bash

   sudo apt install ./desomnia_<version>_arm64.deb

Fedora, RHEL, openSUSE
++++++++++++++++++++++

Download the ``.rpm`` matching your architecture and install it with ``dnf`` (or ``zypper`` on
openSUSE):

.. code:: bash

   sudo dnf install ./desomnia-<version>-1.aarch64.rpm

.. note::

   The ``libpcap`` library is required and pulled in automatically. The optional tools
   ``ethtool`` and ``iproute2`` — used to arm the network interface for Wake-on-LAN and to
   assign temporary address mappings — are recommended and installed alongside on most systems.

Filesystem layout
-----------------

Desomnia uses the following locations in alignment with the `Filesystem Hierarchy Standard`_ (FHS):

.. include:: ./paths/bin.rst
   :end-before: .. permissions ..

.. include:: ./paths/data.rst
   :end-before: .. plugins ..

/usr/lib/systemd/system/desomnia.service
    The systemd service unit.

Configuration
-------------

The package installs a ready-to-run configuration at ``/etc/desomnia/monitor.xml`` that puts
Desomnia into a zero-configuration :doc:`Sleep Proxy </modules/network/sleepproxy>` mode: it
watches the network in promiscuous mode and keeps sleeping hosts reachable, waking them on
demand, without any per-host setup. This is why the service can run immediately after
installation, and it suits the always-on :doc:`Wake-on-LAN proxy </guides/wol-proxy>` role
directly.

To tailor it to your network — declaring specific hosts, a router, or a Wake-on-LAN client role —
edit the file and restart the service:

.. code:: bash

   sudo nano /etc/desomnia/monitor.xml
   sudo systemctl restart desomnia

Your changes are preserved across package upgrades. See :doc:`/concepts/resources` for the full
set of configuration elements.

Running as a service
--------------------

Because a working configuration ships with the package, the systemd service is enabled and
started **automatically** on installation. Check its status at any time:

.. code:: bash

   systemctl status desomnia

To stop it and prevent it from starting at boot:

.. code:: bash

   sudo systemctl disable --now desomnia

Journal
+++++++

.. include:: ./journal.rst

Updating
--------

Download the newer package and install it the same way. If the service was running, it is
restarted automatically; your ``monitor.xml`` is left untouched.

Uninstallation
--------------

.. code:: bash

   sudo apt remove desomnia      # Debian/Ubuntu
   sudo dnf remove desomnia      # Fedora/RHEL

This stops and disables the service. Your configuration in ``/etc/desomnia`` and any log files
are left in place; remove them manually if you no longer need them.

.. _`GitHub Releases`: https://github.com/mad0x20wizard/Desomnia/releases

.. _`Filesystem Hierarchy Standard`: https://en.wikipedia.org/wiki/Filesystem_Hierarchy_Standard
