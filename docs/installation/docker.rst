Docker container
================

:OS: 🐧 *Linux*

If used as a :doc:`Wake-on-LAN proxy </guides/wol-proxy>`, Desomnia can be run inside a `Docker container`_, which isolates the service from the rest of the system and provides easy portability. It is also the quickest way to get an instance running on Linux, as it comes packaged with all necessary libraries and plugins. Only two additional capabilities need to be granted explicitly, as Desomnia requires raw network access — something Docker restricts by default.

Filesystem layout
-----------------

The following directories inside the container must be bind-mounted from the host to provide your configuration and access log output:

.. include:: ./paths.rst

Configuration
-------------

The following ``docker-compose.yaml`` contains all the settings needed to start the container:

.. code:: yaml

  services:
    desomnia:
      image: mad0x20wizard/desomnia

      volumes:
        - ./config:/etc/desomnia
        - ./plugins:/var/lib/desomnia/plugins # optional
        - ./logs:/var/log/desomnia # optional

      restart: unless-stopped

      network_mode: host

      cap_add:
        - NET_RAW
        - NET_ADMIN

Place this file in the current directory and run ``docker compose up`` to start the container. The host paths on the left side of each volume mapping (e.g. ``./config``) are relative to the directory where the compose file lives; the right side shows the path inside the container.

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
