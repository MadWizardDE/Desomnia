Docker container
================

:OS: 🐧 *Linux*

If used as a :doc:`Wake-on-LAN proxy </guides/wol-proxy>`, Desomnia can be run inside a `Docker container`_, which isolates the service from the rest of the system and provides easy portability. It is also the quickest way to get an instance running on Linux, as it comes packaged with all necessary libraries and plugins, as well as a ready-to-run default configuration. Only two additional capabilities need to be granted explicitly, as Desomnia requires raw network access — something Docker restricts by default.

Filesystem layout
-----------------

The following directories inside the container can be bind-mounted from the host to supply your own configuration and plugins and to collect log output:

.. include:: ./paths/data.rst

Configuration
-------------

The image ships a ready-to-run default configuration, so a freshly started container acts as a zero-configuration :doc:`Sleep Proxy </modules/network/sleepproxy>` out of the box — it watches the network in promiscuous mode and wakes sleeping hosts on demand, with no per-host setup. Bind-mounting the ``config`` directory is therefore optional; do it only to supply your own ``monitor.xml``, which overrides the built-in default.

The following ``docker-compose.yaml`` contains all the settings needed to start the container:

.. include:: ./config/docker.rst

Place this file in the current directory and run ``docker compose up`` to start the container. The host paths on the left side of each volume mapping (e.g. ``./config``) are relative to the directory where the compose file lives; the right side shows the path inside the container.

.. note::

   If you bind-mount ``config`` you provide the whole configuration directory, which hides the built-in default. Put your own ``monitor.xml`` inside it, or leave the mount out to run the default.

Native image
------------

For 64-bit ARM hosts, a **native** image is published alongside the standard one, distinguished by a ``-native`` suffix on the tag — for example ``mad0x20wizard/desomnia:latest-native`` or ``mad0x20wizard/desomnia:<version>-native``. To use it, simply add the suffix to the image in the compose file:

.. code:: yaml

  services:
    desomnia:
      image: mad0x20wizard/desomnia:latest-native
      # ... the rest of the configuration is unchanged

It is built from the ahead-of-time compiled, self-contained daemon on a minimal base image that carries no .NET runtime. The result is a considerably smaller image with a noticeably lower memory footprint, which makes it a good fit for always-on single-board computers such as a Raspberry Pi acting as a Wake-on-LAN proxy. See :doc:`/modules/network/performance` for the underlying details.

.. include:: ./native/constraints.rst

Everything else — the Network Monitor and the Sleep Proxy — behaves exactly as in the standard image.

Limitations
-----------

The Docker deployment has inherent limitations compared to a native installation. The container is primarily designed to act as a **Wake-on-LAN proxy** — monitoring the network and waking sleeping hosts on behalf of other devices. The Network Monitor is always fully functional, provided the capabilities above are granted.

- **Process Monitor** — The Process Monitor is not useful inside a container, as Desomnia can only see the processes running within it, not those of the host.

- **Local Sleep Management** — By default, the container cannot control the host's sleep state or observe inhibition locks, because it has no access to the system D-Bus and cannot write to ``/sys/power/state``. To enable this, the container must be created as **privileged** and the host D-Bus socket must be bind-mounted:

.. code:: yaml

  desomnia:
    privileged: true
    volumes:
      - /run/dbus:/run/dbus

.. tip::

  With the D-Bus accessible, Desomnia can suspend the system and monitor inhibition locks as it would on a native installation.

.. _`Docker container`: https://hub.docker.com/r/mad0x20wizard/desomnia
