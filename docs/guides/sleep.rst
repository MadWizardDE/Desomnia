Local Sleep Management
======================

:OS: 🪟 Windows 🐧 Linux

The limits of built-in sleep management
----------------------------------------

Both Windows and Linux include an idle detection mechanism that can suspend the system automatically. For an interactive desktop machine this is usually sufficient — a laptop that sleeps after ten minutes with no keyboard or mouse activity. Headless servers and home lab machines live in a different world: they need to stay alive for a client in another room, wake up the moment a connection arrives, and go back to sleep the instant the last client disconnects. Neither platform's built-in approach was designed with this in mind.

The core shortcomings are the same on both:

- There is no awareness of network connections — a file server can stay awake all night serving nobody, or suspend in the middle of a transfer.
- The system cannot distinguish between a process that is doing meaningful work and one that is simply running in the background.
- Once asleep, there is no built-in mechanism to bring the machine back when a client arrives.

On Windows
++++++++++

The Windows case deserves a closer look, because it was the original motivation for Desomnia. Windows lets processes and kernel drivers register *power requests* — explicit instructions to keep the system awake. You can inspect the active ones at any time:

.. code:: PowerShell

    powercfg /requests

::

    SYSTEM:
    [PROCESS] \Device\HarddiskVolume3\Program Files\VideoLAN\vlc.exe

    DISPLAY:
    None.

User processes appear with their executable path, which at least tells you what is keeping the system awake. Kernel drivers, however, typically use low-level APIs that carry no useful caller information — the request shows up with an opaque name and no stated reason.

Windows does provide a way to suppress requests from specific sources via ``powercfg /requestsoverride``, but the mechanism has hard limits: it does not work for all API types, overrides do not survive a reboot, and there is no way to do the inverse — allow only named requests and ignore everything else.

Desomnia addresses all of these limitations, on both platforms, by replacing the built-in sleep management with a configurable set of monitors. You define exactly which activity should keep the system awake, and what actions to take when everything goes quiet.

Timing is everything
--------------------

.. code:: xml

    <?xml version="1.0" encoding="utf-8"?>
    <SystemMonitor version="1" timeout="2min">

        <!-- ... monitor configuration goes here ... -->

    </SystemMonitor>

Desomnia uses a polling approach to check for activity. Each time the configured ``timeout`` elapses, it queries every active monitor to determine whether any watched resources are currently in use or were active since the last check.

Without further configuration, Desomnia only logs which resources are active. In some cases this may be sufficient. To have Desomnia actively manage sleep behaviour, continue reading.

Monitoring resources
--------------------

Desomnia organises your system into a tree of logical resources. The root is the ``<SystemMonitor>``, representing the system as a whole. You divide this into monitors and resources of decreasing scope, each tracking a specific type of activity. Every resource has an idle state, and state changes can trigger configured actions.

Common events
-------------

Attributes that control how Desomnia responds to state changes are identified by their ``on`` prefix, followed by the event name. The attribute value specifies the action to take.

Some events support a delay, so that a brief state change does not immediately trigger a response.

onIdle
++++++

:⚡️ event:

The ``onIdle`` event fires during the timeout phase when a resource has been idle for the full timeout duration. Most resources support this event; the available actions depend on the resource type and its parent monitor.

To put the system to sleep when idle, configure the ``sleep`` action on the ``<SystemMonitor>``:

.. code:: xml

    <?xml version="1.0" encoding="utf-8"?>
    <SystemMonitor version="1" timeout="2min" onIdle="sleep+10min">

        <!-- ... -->

    </SystemMonitor>

.. note::

    The ``+10min`` suffix adds a **10-minute delay** to the ``sleep`` action. The action executes only if the system remains idle for the full delay period. If activity resumes beforehand, the delay is cancelled and restarts the next time idle state is reached.

onDemand
++++++++

:⚡️ event:

The ``onDemand`` event fires when a resource transitions from idle to active. The action runs no later than the next timeout check; some monitors also support firing it immediately when activity is detected.

The most common use on the ``<SystemMonitor>`` is the ``sleepless`` action, which issues a power request to prevent the system from suspending itself. The request is released automatically when the system returns to idle:

.. code:: xml

    <?xml version="1.0" encoding="utf-8"?>
    <SystemMonitor version="1" timeout="2min" onIdle="sleep" onDemand="sleepless">

        <!-- ... -->

    </SystemMonitor>

Configuring both ``onIdle`` and ``onDemand`` together is the recommended setup when using Desomnia as a full replacement for the built-in power management.

Start from here
---------------

A good starting point is to configure Desomnia to replicate the behaviour of the built-in power management system, then refine it from there:

.. code:: xml

    <?xml version="1.0" encoding="utf-8"?>
    <SystemMonitor version="1" timeout="2min" onIdle="sleep" onDemand="sleepless">

        <SessionMonitor />
        <NetworkSessionMonitor />
        <PowerRequestMonitor />

    </SystemMonitor>

The built-in power management monitors these same sources by default, so this configuration should produce roughly the same behaviour as before. From here you can add rules and filters to accommodate specific requirements, and bring in additional monitors that the built-in system does not support at all.

Exploring the core modules
--------------------------

The following monitors are available without any additional plugins. Each has its own reference page; this section briefly describes their purpose and their relation to the built-in power management.

SystemMonitor
+++++++++++++

The root container for all resource monitors. It provides the events and actions that control system-level sleep behaviour. See :doc:`/modules/system/monitor` to understand its role in Desomnia's architecture.

PowerRequestMonitor
+++++++++++++++++++

Keeps the system awake while any process or driver has an active power request outstanding. See :doc:`/modules/power/monitor` to learn how to selectively exclude or allow specific requests.

ProcessMonitor
++++++++++++++

The built-in power management has no concept of process presence — a process cannot keep the system awake simply by running. This monitor fills that gap. See :doc:`/modules/process/monitor` to learn how to watch individual processes or process groups, set CPU thresholds for activity detection, and effectively grant any process the ability to issue power requests.

NetworkMonitor
++++++++++++++

Beyond basic idle detection, this monitor can watch local network services and keep the system awake while they are in use. It also integrates with the Wake-on-LAN functionality to wake remote hosts on demand. See :doc:`/modules/network/monitor` for the full reference, or the :doc:`wol-client` and :doc:`wol-proxy` guides for practical Wake-on-LAN setup.

NetworkSessionMonitor
+++++++++++++++++++++

:OS: 🪟 Windows only

Keeps the system awake while remote clients have open SMB sessions — for example, while someone is actively accessing shared files or folders. See :doc:`/modules/network_session/monitor` to learn how to filter which sessions should count as activity.

SessionMonitor
++++++++++++++

:OS: 🪟 Windows only

The built-in power management keeps the system awake for as long as a user is interacting with it, based on input activity such as mouse and keyboard movement.

Enabling this monitor makes user session activity contribute to the system's overall idle state. See :doc:`/modules/session/monitor` to learn how to filter by user account and how to use session events — for example, to automatically log out idle sessions or stop idle processes.
