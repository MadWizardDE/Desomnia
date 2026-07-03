mDNS Responder
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

Relationship to auto-configuration
----------------------------------

The responder is not a separate feature to switch on: it is active for whatever the proxy is currently holding. Which hosts and services that includes is governed by registration (see :doc:`Handoff <handoff>`) and by the ``autoDetect`` entities ``Host`` and ``Service`` (see :doc:`auto-configuration <auto>`). Desomnia also *consumes* mDNS in the other direction — browsing the network to discover services advertised by remote hosts and to locate other Sleep Proxies — using the same multicast DNS stack.

.. note::

   Only one mDNS responder should answer for a given service on a segment at a time. While a host is awake it answers for itself; the proxy answers only for the interval a host is asleep and its lease is held, and steps back as soon as the host reclaims its own presence on resume.
