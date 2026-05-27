Wake-on-LAN
===========

Wake-on-LAN depends on the network interface being configured to listen for a Magic Packet before the host powers off. Many systems do not keep this enabled persistently — the NIC reverts to a default power-off state after each reboot or power cycle, and a Magic Packet sent to a freshly suspended machine will be silently ignored.

.. note::

    Wake-on-LAN also needs to be enabled in the system's BIOS or UEFI firmware. This is a one-time hardware setting that Desomnia cannot configure. Consult your motherboard or NIC documentation if unsure.

How to enable
-------------

Platform specific tools have to be used to enable Wake-on-LAN for a particular network interface. The following sections explain how to do this for Windows and Linux. Desomnia can take care of this automatically, ensuring the interface is ready every time before the machine suspends. The ``allowWakeOnLAN`` attribute on ``<NetworkMonitor>`` controls which mode to ensure and is documented in the :doc:`configuration reference <config>`.

Just before issuing a suspend, Desomnia queries the monitored interface to determine whether the required Wake-on-LAN mode is already active. If it is not — and the hardware reports it as supported — Desomnia enables it and records the previous state. After the system resumes, it restores that original value. A machine that had Wake-on-LAN disabled before the suspend will have it disabled again after waking up; Desomnia holds the setting open only for the duration of the sleep.

.. attention::

  If the requested mode is not supported by the hardware at all, a warning is logged and the suspend proceeds normally. The system will still sleep; it simply may not respond to a Magic Packet if the NIC does not support it at the hardware level.

ethtool
+++++++

:OS: 🐧 *Linux*

Desomnia uses ``ethtool`` to read and configure Wake-on-LAN on the interface. The tool must be installed on the system; if it is absent, Desomnia will log a warning at startup and skip the automatic activation.

.. code:: bash

    # Debian/Ubuntu
    apt install ethtool

    # Fedora/RHEL
    dnf install ethtool

You can check the current Wake-on-LAN state of an interface at any time:

.. code:: bash

    ethtool eth0

.. code::

    Settings for eth0:
        ...
        Supports Wake-on: pumbg
        Wake-on: d

The ``Supports Wake-on`` line lists all modes the hardware is capable of. ``Wake-on`` shows the currently active modes — ``d`` means disabled. When Desomnia enables Wake-on-LAN automatically, it changes this to include the configured mode (e.g. ``g`` for Magic Packet) just before suspending, then restores it to the previous value after the system resumes.

.. note::

    Changes made by ``ethtool`` are not persistent across reboots. This is intentional in Desomnia's design: the interface starts each boot with Wake-on-LAN in its default state, and Desomnia re-applies the setting every time before sleeping. No additional persistence configuration is required.

PowerShell
++++++++++

:OS: 🪟 *Windows*

On Windows, Wake-on-LAN settings are stored per adapter in the NIC driver and are generally persistent across reboots. However, they can be silently disabled by Windows Update replacing the driver, by power management policy changes, or simply by a non-default factory configuration.

.. admonition:: Work in progress

    Automatic Wake-on-LAN activation on Windows is not yet implemented. The configuration attribute is accepted but has no effect. The section below describes the planned behaviour and explains how to configure Wake-on-LAN manually in the meantime.

Windows exposes NIC power settings through the adapter's advanced driver properties. You can inspect and modify them with PowerShell:

.. code:: PowerShell

    # Read current Wake-on-LAN state
    Get-NetAdapterPowerManagement -Name "Ethernet"

.. code:: PowerShell

    # Enable Wake on Magic Packet
    Set-NetAdapterPowerManagement -Name "Ethernet" -WakeOnMagicPacket Enabled

Alternatively, the same setting is available in Device Manager under the adapter's *Power Management* tab and in the *Advanced* properties as *Wake on Magic Packet*.

When implemented, Desomnia will use the Windows network adapter management API to check whether the required mode is active before each suspend and enable it if not, restoring the original state on resume — the same behaviour as on Linux.
