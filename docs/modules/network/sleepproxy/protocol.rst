Protocol details
=========================

:OS: 🪐 *Platform-independent*

This page documents the wire-level details of Desomnia's :doc:`Sleep Proxy <../sleepproxy>` — what exactly travels in a registration, how oversized registrations are handled, and how the proxy announces itself on the network. None of this is required for configuring or using the proxy; it is intended for readers who want to understand the exchange itself, for example when debugging interoperability with other implementations.

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

- As a **client**, the ``handoffMTU`` attribute (see :doc:`Handoff <../handoff>`) controls whether an oversized registration is split into a burst of smaller messages — the same strategy Apple's client uses — or sent as one large datagram and left to IP fragmentation.
- As a **proxy**, both forms are accepted: fragmented datagrams are received reassembled through an operating-system socket (up to the UDP maximum of 64 KiB), and multi-message bursts are collected and processed as a single registration. Bursts from Desomnia clients announce their page count and complete instantly; bursts from other clients (such as macOS, whose registrations routinely span several messages once AirPlay or sharing is enabled) are bounded by a short collection window.

Proxy advertisement
-------------------

The proxy announces itself on the link as a ``_sleep-proxy._udp`` DNS-SD service. Its instance name carries the metric in Apple's convention (for example ``30-40-70-70 desktop``), and its TXT record identifies the implementation:

``impl``
  The implementation name, ``Desomnia``.
``ver``
  The running Desomnia version, for example ``3.1.0-alpha4``.

Apple's proxies publish neither key, which makes it easy to tell the implementations apart when browsing ``_sleep-proxy._udp`` — for example with ``dns-sd -B _sleep-proxy._udp`` on macOS.
