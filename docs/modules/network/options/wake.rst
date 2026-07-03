wakeType
++++++++

:inherited:
:default: ``auto``

Specifies, how Magic Packets should be sent. Possible values are:

``auto``
  Checks if the requested IP address is in the same subnet as the configured network interface. If both hosts reside in the same network (meaning they share the same range of IP addresses), ``link`` will be used. Otherwise a combination of ``network|unicast`` will be used, because it will be unlikely then, that the target host can be reached with a link layer packet.

``link``
  Uses ethernet packets with an an `EtherType <https://en.wikipedia.org/wiki/EtherType>`__ of ``0x0842``, that operate below the threshold of user space applications, to prevent interference with actual network services. For this to work, the target host has to be on the same subnet.

``network``
  Uses UDP packets with the Magic Packet as payload, which can be routed over the internet or a tunnel connection (VPN). It may be necessary to configure a static address mapping on the router or VPN server, to reach the sleeping host. Can be observed by user space applications, that listen on the specified port number.

``unicast``
  Send the Magic Packet directly to the device to wake up, without using a broad- or multicast. The link layer packet will be directed at the Ethernet address of the device and potentially all the known IP addresses of the host, if configured with ``network``.

``none``
  Do not send any Magic Packets. This setting is used to temporarily prevent a host from waking up.

wakePort
++++++++

:inherited:
:default: ``9``

Specifies the port number for Magic Packets encapsulated in UDP packets, if ``wakeType`` was configured with ``network``. If ``wakeType`` is configure with ``link``, this attribute has no meaning.

Typical port numbers used for this are ``7`` (Echo) or ``9`` (Discard). 

wakeTimeout
+++++++++++

:inherited:
:default: ``10s``

Defines the timeout, after that a wake up will be considered as failed, if we haven't received any response from the target host after the Magic Packet has been sent. There will be a warning in the log file, if this happens. Also buffered packets will only be forwarded to the target host, if the demand request succeeds.

wakeRepeat
+++++++++++

:inherited:

This option specifies whether to resend the Magic Packet and how long to wait between each attempt, until the configured ``wakeTimeout`` has elapsed.

wakePassword
++++++++++++

:inherited:

The *SecureOn* password included in the Magic Packet, for adapters that require a password before they will wake the host. As defined by the standard, a SecureOn password is at most 6 bytes long (4- and 6-byte forms are supported). By default the password is read as text; set ``wakePasswordEncoding="base64"`` to supply the raw bytes as Base64 instead.

.. note::
  This is the same password a host transmits to a :doc:`Sleep Proxy </modules/network/sleepproxy>` during :doc:`handoff </modules/network/handoff>` (as ``handoffPassword``), so that the proxy can wake it later.

wakePing
++++++++

:inherited:
:default: ``false``

This option specifies whether to actively ping the target host during a wake operation. Each individual ping will timeout after the configured ``pingTimeout``, until the configured ``wakeTimeout`` has elapsed. If you do not configure this parameter, the NetworkMonitor will try to passively listen for a sign that the target host has successfully been woken up. The target host will actively pinged in any case, if it is not part of the local subnet.