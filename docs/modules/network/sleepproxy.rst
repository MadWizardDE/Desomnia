Sleep Proxy
===========

:OS: 🪐 *Platform-independent*

A **Sleep Proxy** is an always-on device that maintains the network presence of hosts that have gone to sleep, and wakes them again on demand. The concept and its wire protocol originate from Apple's *Bonjour Sleep Proxy* (also known as *Wake on Demand*): before a machine suspends, it hands its services over to a proxy via multicast DNS; the proxy keeps answering service discovery queries on the sleeper's behalf, and the instant a client actually tries to use one of those services, the proxy wakes the machine back up.

Desomnia can act as **either end** of this exchange:

- As a **Sleep Proxy server** — the always-on device that accepts registrations and wakes hosts. This is the role described on this page.
- As a **Sleep Proxy client** — a machine that registers its own services before suspending. Because that is part of a host's departure handshake, it is configured through the ``handoff`` attribute and documented on the :doc:`Handoff <handoff>` page.

Because it speaks the standard protocol, a Desomnia proxy can serve non-Desomnia clients (for example Apple devices), and a Desomnia client can register with a non-Desomnia proxy.

.. hint::

   The Sleep Proxy builds directly on :doc:`promiscuous mode <promiscuous>` and the address-claiming machinery described under :doc:`Handoff <handoff>`. If you have not yet set up a working proxy, start with the :doc:`/guides/wol-proxy` guide; the Sleep Proxy adds automatic, standards-based service registration on top of that foundation.

How it works
------------

1. A host about to suspend sends a registration to the proxy, listing its MAC address, its IP addresses, and the services it wants kept alive (for example RDP on port 3389).
2. The proxy grants a **lease** for a bounded duration and begins :doc:`answering mDNS queries <mdns>` for those services, so that other devices continue to discover the host as if it were awake.
3. When a client tries to reach one of the registered services — or when the lease is about to expire with the :ref:`expiry action <sleepproxy-expire>` set to wake — the proxy sends a Magic Packet and the host comes back online.
4. Once the host is awake again it reclaims its own services, the lease ends, and the proxy releases everything it was holding on the host's behalf.

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

Every registration is bound to a lease with a finite duration. A client may request a specific duration; the proxy clamps the request into the range it is willing to grant and, if the client requests nothing, falls back to a default.

sleepProxyLease
+++++++++++++++

:inherited:
:default: *(unset — falls back to* ``sleepProxyLeaseMax`` *)*

The role of this attribute depends on which side you configure it on:

- On a **proxy**, it is the lease duration granted when a client registers without requesting one of its own.
- On a **client** (a host handing off with ``handoff="SleepProxy"``), it is the *desired* lease duration transmitted to the proxy at registration time. The proxy is free to clamp the request into the range it is willing to grant — see ``sleepProxyLeaseMin`` and ``sleepProxyLeaseMax`` below.

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
:default: ``none``

What the proxy does when a lease reaches its end without the host having come back on its own:

``none``
  Simply release the lease. If the host is still asleep, its services stop being advertised until it registers again.
``wake``
  Send a Magic Packet to wake the host before releasing the lease — but only if the host is not already responding. Use this when a registered host should never silently disappear from the network, even if it stays asleep longer than its lease.

Choosing between proxies
------------------------

Several Sleep Proxies may be present on one network. Each advertises a four-part **metric** that lets clients pick the most suitable one — a dedicated, mains-powered, always-on device is a better proxy than an incidentally-available laptop. Clients prefer the proxy with the *lowest* metric.

sleepProxyMetrics
+++++++++++++++++

:inherited:
:default: ``best``

The metric this proxy advertises. You may use one of the shorthands ``best``, ``average`` or ``worst``, or specify the four fields explicitly as ``intent-portability-marginalPower-totalPower`` (each ``10``–``99``), following Apple's convention — for example ``30-40-70-70``. Lower is more preferred. Leave this at ``Best`` if this device is the intended proxy for the segment; raise it if the machine is only an opportunistic fallback.

sleepProxyPort
++++++++++++++

:inherited:
:default: ``5353``

The UDP port the Sleep Proxy service listens on. The default is the standard multicast DNS port and should rarely be changed.

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

See also
--------

- :doc:`Handoff <handoff>` — the departure handshake and the ``handoff`` attribute used by the client side.
- :doc:`mDNS responder <mdns>` — how the proxy answers service discovery queries for sleeping hosts.
- :doc:`/guides/wol-proxy` — setting up the always-on proxy device that this feature builds on.
