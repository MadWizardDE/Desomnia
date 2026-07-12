Multicast DNS
==============

:OS: 🪐 *Platform-independent*

Multicast DNS (mDNS, RFC 6762) and DNS-based service discovery (DNS-SD, RFC 6763) — together marketed by Apple as *Bonjour* and implemented on Linux by *Avahi* — let devices find each other and each other's services without any central server. A client that wants to print, stream, or open a remote desktop asks the local link "who offers this service?", and the hosts that do answer directly. This is how ``_rdp._tcp``, ``_smb._tcp``, AirPlay, printers and countless other services are located on a home or office network.

The problem for power management is simple: **a sleeping host cannot answer.** Once a machine suspends, it stops responding to the multicast queries that advertise its services, and to the clients on the network it effectively ceases to exist. Service discovery breaks precisely when we most want the ability to wake the machine back up.

Why Desomnia answers
--------------------

When Desomnia runs as a :doc:`Sleep Proxy <sleepproxy>`, it takes over the mDNS presence of the hosts registered with it. For every registered host and service, the proxy answers the relevant multicast DNS queries **on the sleeping host's behalf**, so that:

- Service discovery continues to work while the host is asleep — browsing clients still see the service and can attempt to connect.
- The connection attempt that follows discovery is what the proxy turns into a wake-up. The client resolves the service, opens a connection, the proxy intercepts it, sends a Magic Packet, and the host is back before the client gives up.

Without this responder, a proxy could still wake hosts that are addressed directly by IP, but any client relying on Bonjour/DNS-SD to *find* the service first would never get far enough to trigger a wake. The mDNS responder is what makes wake-on-demand transparent to standards-based clients.

What Desomnia answers
---------------------

For the hosts and services it is proxying, Desomnia responds directly to the multicast DNS record types that service discovery depends on:

``PTR``
  Service enumeration — "which instances of this service type exist?" — so the sleeping host's service appears in a client's browse results.
``SRV``
  The host name and port an instance lives on, pointing a client at the sleeping host.
``TXT``
  The service's metadata key/value pairs.
``A`` / ``AAAA``
  The IPv4 and IPv6 addresses backing the host name.

In addition, the proxy advertises **itself** as a Sleep Proxy server, using the structured DNS-SD instance name (the four-part metric followed by a label) that clients use to :doc:`choose between available proxies <sleepproxy>`.

Advertising over mDNS is **opt-in** and split into two independent toggles that solve different problems: ``advertiseHosts`` publishes the address records (``A`` / ``AAAA``) that resolve a host name to an IP, and ``advertiseServices`` publishes the service records (``PTR`` / ``SRV`` / ``TXT``) that service discovery depends on. You can enable either on its own; the two sections below describe when each is worthwhile.

Both are off by default and can be set on the ``<NetworkMonitor>``, to apply to every watched host, or on an individual host, to enable or suppress advertising selectively. Note that on a host the address toggle is spelled ``advertiseHost``, in the singular — ``advertiseServices`` keeps its plural spelling at both levels.

Advertising hosts
-----------------

``advertiseHosts`` makes Desomnia answer name resolution — the ``A`` / ``AAAA`` records that map a **host name** to its IP addresses — for a host that cannot answer for itself.

A machine that is awake resolves its own name: it advertises it over mDNS, or it is registered with the network's DNS, so a client asking "what is the address of *server*?" gets an answer. A **sleeping** host cannot, and whether anything else answers in its place depends entirely on the network. Many routers resolve local host names, but not all do, and a network whose clients are pointed at a public DNS resolver has no local authority for these names at all. Where nothing answers, the name resolves to nothing: the client never obtains an address, so it never connects — and a proxy's ARP/NDP address defence, which only comes into play *once a client already has an address to reach*, never gets the chance to act. Defending the addresses is, perhaps surprisingly, not by itself enough to keep a host reachable by name.

Enabling ``advertiseHosts`` closes that gap. Desomnia answers the name query on the sleeping host's behalf, handing out its IP so clients can still resolve, reach, and thereby wake it while its owner is asleep.

.. code:: xml

   <NetworkMonitor watchMode="promiscuous" advertiseHosts="true">
     <!-- ... -->
   </NetworkMonitor>

Advertising services
--------------------

``advertiseServices`` makes Desomnia publish the **service** records (``PTR`` / ``SRV`` / ``TXT``) that service discovery depends on, so that browsing clients (Bonjour/DNS-SD) can find a host's services without knowing anything about it in advance. This fills two distinct gaps:

- **While a host sleeps** it stops answering service browses, and to the network its services effectively cease to exist. The proxy keeps them in clients' browse results, and the connection attempt that follows a discovery is what triggers the wake.
- **For systems that do not advertise their services at all.** Not every operating system ships mDNS service advertisement — Windows, for instance, has no such machinery out of the box. Desomnia can publish the services in its stead, even while the host is running and even if no other mDNS software (Bonjour, Avahi) is installed, making them discoverable to the rest of the network.

Because Desomnia answers only when no other responder on the link does, it coexists with hosts that advertise for themselves: it steps in exactly when — and only when — the advertisement would otherwise be missing.

.. code:: xml

   <NetworkMonitor advertiseServices="true">
     <!-- ... -->
   </NetworkMonitor>

Coverage and interaction
------------------------

Which hosts and services the responder covers, once enabled, is governed by registration (see :doc:`Handoff <handoff>`) and by the ``autoDetect`` entities ``Host`` and ``Service`` (see :doc:`auto-configuration <auto>`). Desomnia also *consumes* mDNS in the other direction — browsing the network to discover services advertised by remote hosts and to locate other Sleep Proxies — using the same multicast DNS stack; this browsing is independent of both toggles.

.. note::

   Only one mDNS responder should answer for a given name or service on a segment at a time. Desomnia holds back whenever another responder — most often the host itself, while awake — already answers, and steps in only when nothing else does; for a proxied host that is the interval it is asleep and its lease is held.
