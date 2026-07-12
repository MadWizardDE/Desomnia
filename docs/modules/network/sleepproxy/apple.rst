.. _sleepproxy-apple:

Apple compatibility
===================

:OS: 🪐 *Platform-independent*

Apple's **mDNSResponder** — the origin of the Bonjour Sleep Proxy protocol and the implementation with the biggest reach — is the most likely counterpart a Desomnia :doc:`Sleep Proxy <../sleepproxy>` or client will encounter in the wild. Desomnia's implementation has been developed against Apple's open-source ``mDNSResponder`` and verified against real Apple hardware. Both directions work out of the box:

**Desomnia as a proxy for Apple clients.** A Mac with *Wake for network access* enabled registers with a Desomnia proxy like with any Apple one. The quirks of Apple's client are accepted transparently — registrations without a zone section, per-service instance names, service-type enumeration and subtype pointers, auxiliary records such as the device-info TXT, and registrations that span multiple messages (routine for a Mac with AirPlay or sharing enabled). SRV priorities/weights and TXT attributes are preserved and re-advertised exactly as registered. When the Mac wakes up, its return announcement releases the lease immediately.

**Desomnia as a client of an Apple proxy.** A host with ``handoff="SleepProxy"`` registers with an Apple TV, HomePod or AirPort base station just as a Mac would: the registration includes everything Apple's proxy needs to defend the host's addresses (reverse address mappings for ARP/NDP proxying), advertise its services, and wake it with a Magic Packet — including the *SecureOn* password, if one is configured.

.. _sleepproxy-caveats:

Known caveats
-------------

The following limitations apply when one end of the exchange is an Apple implementation; none of them affect Desomnia-to-Desomnia operation.

- **No wake filters on Apple proxies.** Desomnia's :doc:`filter rules </guides/filtering/service>` travel as private protocol extensions that Apple's proxy ignores. An Apple proxy wakes the host according to its own fixed policy — essentially any incoming TCP connection attempt to the sleeping host — so wake gating by source host or address range is only enforced when the proxy is also a Desomnia instance.
- **Apple proxies cap leases at 24 hours** and always wake the host when the lease expires, regardless of any expiry setting. However long your ``handoffDuration``, expect a sleeping host parked at an Apple proxy to be woken at least once a day; Desomnia then re-registers on the next idle, so the cycle is self-healing.
- **A short blind spot after registration.** For roughly the first ten seconds after accepting a registration, Apple's proxy probes the host to confirm it is really asleep and does not yet answer address resolution on its behalf. Connection attempts in this window can fail; they succeed once the proxy starts defending the addresses.
- **Apple clients ignore registration errors.** Apple's client treats any response as success, so a registration a Desomnia proxy has to refuse (for example because the lease pool is exhausted) goes unnoticed by a Mac — it suspends believing it is proxied. Nothing can be done about this on the proxy side.
- **No TCP keepalive proxying.** Apple's proxies can keep a sleeping Mac's established TCP connections alive by answering keepalive probes on its behalf. Desomnia accepts such registrations but does not currently sustain the connections, so long-lived idle connections of a sleeping Mac may time out. This is a planned improvement.
- **Friendly names are not preserved by Apple proxies.** The service names from Desomnia's configuration travel as a private extension; an Apple proxy simply advertises the service without them, which is cosmetic.

Compatible devices
------------------

The following Aplle devices offer a Bonjour Sleep Proxy on the network and can be used instead of a seperate Desomnia deployment, if you own one of these:

.. list-table::
   :header-rows: 1
   :widths: 30 70

   * - Device
     - Notes
   * - **Apple TV**
     - 2nd generation and later. Acts as a Sleep Proxy automatically whenever it is powered and on the network; the most common proxy in Apple households today.
   * - **HomePod / HomePod mini**
     - Acts as a Sleep Proxy automatically, like the Apple TV.
   * - **AirPort Express / Extreme / Time Capsule**
     - Apple's Wi-Fi base stations, from firmware 7.4.2 on. Discontinued in 2018, but still deployed in many networks.

.. note::

   Several proxies may serve the same network segment simultaneously — clients simply pick the one with the best metric. If Desomnia should win that election against an Apple TV, advertise a better metric via ``sleepProxyMetrics``.