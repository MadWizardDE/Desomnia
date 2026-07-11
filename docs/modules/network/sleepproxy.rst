Sleep Proxy
===========

:OS: 🪐 *Platform-independent*

A **Sleep Proxy** is an always-on device that maintains the network presence of hosts that have gone to sleep, and wakes them again on demand. The concept and its wire protocol originate from Apple's *Bonjour Sleep Proxy* (also known as *Wake on Demand*): before a machine suspends, it hands its services over to a proxy via multicast DNS; the proxy keeps answering service discovery queries on the sleeper's behalf, and the instant a client actually tries to use one of those services, the proxy wakes the machine back up.

Desomnia can act as **either end** of this exchange:

- As a **Sleep Proxy server** — the always-on device that accepts registrations and wakes hosts. This is the role described on this page.
- As a **Sleep Proxy client** — a machine that registers its own services before suspending. Because that is part of a host's departure handshake, it is configured through the ``handoff`` attribute and documented on the :doc:`Handoff <handoff>` page.

Because it speaks the standard protocol, a Desomnia proxy can serve non-Desomnia clients, and a Desomnia client can register with a non-Desomnia proxy. Interoperability with Apple's implementation — in both directions — is covered in :ref:`its own section <sleepproxy-apple>` below.

.. hint::

   The Sleep Proxy builds directly on :doc:`promiscuous mode <promiscuous>` and the address-claiming machinery described under :doc:`Handoff <handoff>`. If you have not yet set up a working proxy, start with the :doc:`/guides/wol-proxy` guide; the Sleep Proxy adds automatic, standards-based service registration on top of that foundation.

How it works
------------

1. A host about to suspend sends a registration to the proxy, listing its MAC address, its IP addresses, and the services it wants kept alive (for example RDP on port 3389).
2. The proxy grants a **lease** for a bounded duration and begins :doc:`answering mDNS queries <mdns>` for those services, so that other devices continue to discover the host as if it were awake.
3. When a client tries to reach one of the registered services — or when the lease is about to expire with the :ref:`expiry action <sleepproxy-expire>` set to wake — the proxy sends a Magic Packet and the host comes back online.
4. Once the host is awake again it announces its return, the lease ends immediately, and the proxy releases everything it was holding on the host's behalf.

What a registration carries
---------------------------

A registration is a DNS UPDATE message describing everything the proxy needs to stand in for the host:

- The host's **MAC address** (and, for a virtual machine, the physical host's MAC as the wake target), plus an optional *SecureOn* password (``handoffPassword``).
- Its **IP addresses**, together with their reverse mappings — these are what allow a proxy to answer address resolution (ARP/NDP) for the sleeping host.
- Its **services**: DNS-SD type and port, the instance label each is advertised under (labels may differ per service), the SRV **priority** and **weight**, and the full set of **TXT attributes**. A Desomnia proxy re-advertises all of these verbatim, so a browsing client sees the service exactly as the host itself would present it.
- The requested **lease duration** (``handoffDuration``).

Between two Desomnia instances, the registration additionally carries private extensions, encoded as EDNS0 options that third-party implementations simply ignore:

- The **friendly service names** used in Desomnia's configuration and logs. These deliberately do *not* travel as TXT attributes, because a third-party proxy would re-advertise them on the link.
- The service's actionable **wake filter rules** (see :doc:`/guides/filtering/service`), so that the proxy applies the same wake gating the host itself would — see the :ref:`Apple caveats <sleepproxy-caveats>` for why this only works between Desomnia peers.
- **Paging information** for large registrations, see below.

Large registrations
-------------------

A registration with many services can exceed what fits into a single network packet. Desomnia handles this on both ends:

- As a **client**, the ``handoffMTU`` attribute (see :doc:`Handoff <handoff>`) controls whether an oversized registration is split into a burst of smaller messages — the same strategy Apple's client uses — or sent as one large datagram and left to IP fragmentation.
- As a **proxy**, both forms are accepted: fragmented datagrams are received reassembled through an operating-system socket (up to the UDP maximum of 64 KiB), and multi-message bursts are collected and processed as a single registration. Bursts from Desomnia clients announce their page count and complete instantly; bursts from other clients (such as macOS, whose registrations routinely span several messages once AirPlay or sharing is enabled) are bounded by a short collection window.

Enabling the proxy
------------------

The Sleep Proxy service is offered automatically when a ``<NetworkMonitor>`` runs in :doc:`promiscuous mode <promiscuous>` and is allowed to learn hosts and/or services dynamically. In practice this means combining ``watchMode="promiscuous"`` with an ``autoDetect`` value that includes ``Host`` and/or ``Service``:

.. code:: xml

   <SystemMonitor version="1">

     <NetworkMonitor watchMode="promiscuous" autoDetect="Host|Service">
       <!-- statically declared hosts may still appear here -->
     </NetworkMonitor>

   </SystemMonitor>

The distinction between the two discovery entities is important for the proxy:

``Service``
  The proxy may attach **newly registered services to hosts that already exist** in its configuration. A registration for a host it does not know is rejected.
``Host``
  The proxy may additionally **create host definitions on the fly** for hosts it has never seen, purely from their registration. Combined with ``Service`` this yields a proxy that requires no per-host configuration at all.

See :doc:`auto-configuration <auto>` for the full list of ``autoDetect`` entities and how inheritance between ``<NetworkMonitor>`` and individual hosts works.

Leases
------

Every registration is bound to a lease with a finite duration. The client requests a duration — for a Desomnia client this is its ``handoffDuration`` (see :doc:`Handoff <handoff>`) — and the proxy clamps the request into the range it is willing to grant. The granted duration is returned in the registration response.

sleepProxyLeaseMin
++++++++++++++++++

:inherited:
:default: ``30min``

The shortest lease the proxy will grant. Requests below this are rounded up.

sleepProxyLeaseMax
++++++++++++++++++

:inherited:
:default: ``365d``

The longest lease the proxy will grant. Requests above this are capped.

sleepProxyLimit
+++++++++++++++

:inherited:
:default: ``100``

The maximum number of simultaneous leases. Once the pool is exhausted, further registrations are refused until a lease ends. This bounds the resources a single proxy commits to sleeping hosts.

.. _sleepproxy-expire:

sleepProxyLeaseExpire
+++++++++++++++++++++

:inherited:
:default: ``wake``

What the proxy does when a lease reaches its end without the host having come back on its own:

``none``
  Simply release the lease. If the host is still asleep, its services stop being advertised until it registers again.
``wake``
  Send a Magic Packet to wake the host before releasing the lease — but only if the host is not already responding. A woken host reclaims its presence and, once it idles again, suspends with a fresh registration; a registered host therefore never silently disappears from the network. This mirrors the behaviour of Apple's proxies, which always wake a host whose lease runs out.

Choosing between proxies
------------------------

Several Sleep Proxies may be present on one network. Each advertises a four-part **metric** that lets clients pick the most suitable one — a dedicated, mains-powered, always-on device is a better proxy than an incidentally-available laptop. Clients prefer the proxy with the *lowest* metric; a Desomnia client works through the candidates from best to worst until a registration succeeds (see ``handoffRetry`` on the :doc:`Handoff <handoff>` page).

sleepProxyMetrics
+++++++++++++++++

:inherited:
:default: ``best``

The metric this proxy advertises. You may use one of the shorthands ``best``, ``average`` or ``worst``, or specify the four fields explicitly as ``intent-portability-marginalPower-totalPower`` (each ``10``–``99``), following Apple's convention — for example ``30-40-70-70``. Lower is more preferred. Leave this at ``Best`` if this device is the intended proxy for the segment; raise it if the machine is only an opportunistic fallback.

sleepProxyPort
++++++++++++++

:inherited:
:default: *(unset — an ephemeral port)*

The UDP port the Sleep Proxy service listens on. Clients discover the port through the proxy's DNS-SD advertisement, so it does not normally need to be fixed: when unset, Desomnia reserves an ephemeral port from the operating system. Setting an explicit port makes the endpoint predictable (for example for firewall rules); the port is then bound *shareable*, so it may coexist with other programs on the machine that also bind it reusably — such as an OS-level mDNS responder when using ``5353``.

Proxy advertisement
-------------------

The proxy announces itself on the link as a ``_sleep-proxy._udp`` DNS-SD service. Its instance name carries the metric in Apple's convention (for example ``30-40-70-70 desktop``), and its TXT record identifies the implementation:

``impl``
  The implementation name, ``Desomnia``.
``ver``
  The running Desomnia version, for example ``3.1.0-alpha4``.

Apple's proxies publish neither key, which makes it easy to tell the implementations apart when browsing ``_sleep-proxy._udp`` — for example with ``dns-sd -B _sleep-proxy._udp`` on macOS.

Registering with a Sleep Proxy
------------------------------

To make *this* machine register its services with a proxy before it sleeps, configure :doc:`handoff <handoff>` with ``handoff="SleepProxy"`` rather than the options on this page — those govern the accepting side. Desomnia locates a proxy to register with in one of two ways:

- **Statically**, by declaring the proxy as a ``<SleepProxy>`` host inside ``<NetworkMonitor>``.
- **Dynamically**, by enabling ``autoDetect="SleepProxy"`` so that Desomnia discovers advertised proxies on the network and registers with the one offering the best metric.

.. code:: xml

   <NetworkMonitor autoDetect="SleepProxy" handoff="SleepProxy">
     <!-- or point at a specific proxy: -->
     <SleepProxy name="proxy" IPv4="192.168.1.2" />
   </NetworkMonitor>

sleepProxyDiscovery
+++++++++++++++++++

:inherited:
:default: ``eager``

Controls *when* a client looks for a proxy to register with:

``eager``
  Discover a proxy up front and keep the registration current, so handoff at suspend time is immediate.
``lazy``
  Defer discovery until the host is actually about to suspend. This avoids background traffic at the cost of a slightly slower suspend.

.. note::

   ``eager`` and ``lazy`` are mutually exclusive. A discovered proxy is forgotten again when the local host resumes, so that a proxy which has itself gone away is not trusted indefinitely.

.. _sleepproxy-apple:

Compatibility with Apple's Bonjour Sleep Proxy
----------------------------------------------

Desomnia's implementation has been developed against Apple's open-source ``mDNSResponder`` and verified against real Apple hardware. Both directions work out of the box:

**Desomnia as a proxy for Apple clients.** A Mac with *Wake for network access* enabled registers with a Desomnia proxy like with any Apple one. The quirks of Apple's client are accepted transparently — registrations without a zone section, per-service instance names, service-type enumeration and subtype pointers, auxiliary records such as the device-info TXT, and registrations that span multiple messages (routine for a Mac with AirPlay or sharing enabled). SRV priorities/weights and TXT attributes are preserved and re-advertised exactly as registered. When the Mac wakes up, its return announcement releases the lease immediately.

**Desomnia as a client of an Apple proxy.** A host with ``handoff="SleepProxy"`` registers with an Apple TV, HomePod or AirPort base station just as a Mac would: the registration includes everything Apple's proxy needs to defend the host's addresses (reverse address mappings for ARP/NDP proxying), advertise its services, and wake it with a Magic Packet — including the *SecureOn* password, if one is configured.

.. _sleepproxy-caveats:

Known caveats
+++++++++++++

The following limitations apply when one end of the exchange is an Apple implementation; none of them affect Desomnia-to-Desomnia operation.

- **No wake filters on Apple proxies.** Desomnia's :doc:`filter rules </guides/filtering/service>` travel as private protocol extensions that Apple's proxy ignores. An Apple proxy wakes the host according to its own fixed policy — essentially any incoming TCP connection attempt to the sleeping host — so wake gating by source host or address range is only enforced when the proxy is also a Desomnia instance.
- **Apple proxies cap leases at 24 hours** and always wake the host when the lease expires, regardless of any expiry setting. However long your ``handoffDuration``, expect a sleeping host parked at an Apple proxy to be woken at least once a day; Desomnia then re-registers on the next idle, so the cycle is self-healing.
- **A short blind spot after registration.** For roughly the first ten seconds after accepting a registration, Apple's proxy probes the host to confirm it is really asleep and does not yet answer address resolution on its behalf. Connection attempts in this window can fail; they succeed once the proxy starts defending the addresses.
- **Apple clients ignore registration errors.** Apple's client treats any response as success, so a registration a Desomnia proxy has to refuse (for example because the lease pool is exhausted) goes unnoticed by a Mac — it suspends believing it is proxied. Nothing can be done about this on the proxy side.
- **No TCP keepalive proxying.** Apple's proxies can keep a sleeping Mac's established TCP connections alive by answering keepalive probes on its behalf. Desomnia accepts such registrations but does not currently sustain the connections, so long-lived idle connections of a sleeping Mac may time out. This is a planned improvement.
- **Friendly names are not preserved by Apple proxies.** The service names from Desomnia's configuration travel as a private extension; an Apple proxy simply advertises the service without them, which is cosmetic.

Known Sleep Proxy devices
-------------------------

Besides Desomnia itself, the following devices offer a Bonjour Sleep Proxy on the network — relevant both as alternatives and because a Desomnia client will discover and rank them alongside Desomnia proxies:

.. list-table::
   :header-rows: 1
   :widths: 30 70

   * - Device
     - Notes
   * - **Desomnia**
     - This project, on any supported platform. Recognisable by the ``impl``/``ver`` keys in its TXT record.
   * - **Apple TV**
     - 2nd generation and later. Acts as a Sleep Proxy automatically whenever it is powered and on the network; the most common proxy in Apple households today.
   * - **HomePod / HomePod mini**
     - Acts as a Sleep Proxy automatically, like the Apple TV.
   * - **AirPort Express / Extreme / Time Capsule**
     - Apple's Wi-Fi base stations, from firmware 7.4.2 on. Discontinued in 2018, but still deployed in many networks.
   * - **Open-source implementations**
     - A handful of community projects re-implement the server side of the protocol with varying completeness; they interoperate with Desomnia clients to the extent that they implement the standard registration exchange.

.. note::

   Several proxies may serve the same network segment simultaneously — clients simply pick the one with the best metric. If Desomnia should win that election against an Apple TV, advertise a better metric via ``sleepProxyMetrics``.

See also
--------

- :doc:`Handoff <handoff>` — the departure handshake and the client-side attributes ``handoff``, ``handoffDuration``, ``handoffMTU`` and ``handoffRetry``.
- :doc:`mDNS responder <mdns>` — how the proxy answers service discovery queries for sleeping hosts.
- :doc:`/guides/wol-proxy` — setting up the always-on proxy device that this feature builds on.
