Interface blocking
==================

Desomnia can take network interfaces out of service for you. This is useful whenever one connection should be preferred over another — the classic example being a docked laptop, where the internal WiFi should stay silent as long as the wired connection is available. Instead of scripting the enable/disable dance yourself, you declare which interfaces should be blocked, and Desomnia keeps reality in line with that declaration.

A block is expressed with the ``<NetworkInterfaceBlock>`` element:

.. code:: xml

    <NetworkInterfaceBlock interface="wlan0" />

The ``interface`` attribute selects the interfaces to block and uses the same notation as :doc:`interface selection <interface>` — an interface name on Linux and macOS, a name or GUID on Windows, and a regular expression anywhere. A single block may therefore cover several interfaces at once, for example ``interface="en0|en12"``.

Placement
---------

Where you place the element decides how long the block holds:

Inside a ``<NetworkMonitor>``
+++++++++++++++++++++++++++++

.. code:: xml

    <NetworkMonitor name="Ethernet" interface="eth0">
        <NetworkInterfaceBlock interface="wlan0" />
        <!-- hosts, etc. -->
    </NetworkMonitor>

The block is tied to the life of that particular monitor: the matched interfaces stay blocked while the monitored interface is being watched, and are released as soon as the monitor shuts down — for example, when the ethernet cable is pulled. The blocked interface then comes back on its own, and its own configuration (if any) takes over. A monitor can never block the interface it is monitoring itself; such a match is ignored with a warning.

At the configuration root
+++++++++++++++++++++++++

.. code:: xml

    <SystemMonitor>
        <NetworkInterfaceBlock interface="wlan0" />
        <!-- monitors, etc. -->
    </SystemMonitor>

A block at the root of ``<SystemMonitor>`` is not tied to any monitor — it holds as long as the configuration that declares it is in effect. Root-level blocks take precedence over everything else: an interface blocked this way is never monitored, even if a ``<NetworkMonitor>`` would otherwise match it. A running monitor on such an interface is shut down in an orderly fashion before the block is applied.

Enforcement
-----------

.. code:: xml

    <NetworkInterfaceBlock interface="wlan0" force="true" />

By default, Desomnia is tolerant about outside interference: if you (or the system) manually re-enable a blocked interface, Desomnia respects that decision and leaves the interface alone until the blocking declaration itself changes. With ``force="true"``, the block is enforced instead — whenever the interface comes back into service, Desomnia takes it right back out.

Restoration
-----------

Blocks never outlive their declaration. When a block is lifted — because its monitor ended, the configuration changed, or Desomnia shuts down — the interfaces are brought back into service. Only a state Desomnia actually took away is restored: an interface that was already down when the block began stays down when it is released, and an interface that has vanished in the meantime (a dock's USB adapter, for example) is left to start fresh when it reappears.
