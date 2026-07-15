Performance
===========

The NetworkMonitor uses `libpcap <https://en.wikipedia.org/wiki/Pcap>`__ to capture network packets at the Ethernet level. libpcap is a widely used industry standard for packet capture, found in tools such as Wireshark. On Linux and macOS it is typically already present; on Windows, `npcap <https://npcap.com/>`__ provides the equivalent implementation and is installed automatically by the Desomnia installer.

Internally, Desomnia uses `SharpPcap <https://github.com/chmorgan/sharppcap>`__ and `PacketNet <https://github.com/chmorgan/packetnet>`__ to communicate with libpcap and to parse the structure of captured packets. Both libraries are designed with performance in mind.

Berkeley Packet Filter
-----------------------

:OS: 🪟 *Windows* 🐧 *Linux* 🍎 *macOS*

The most significant performance optimisation in the NetworkMonitor is its use of `Berkeley Packet Filter <https://en.wikipedia.org/wiki/Berkeley_Packet_Filter>`__ (BPF) rules. Rather than examining every packet in user space, Desomnia declares its filtering criteria directly inside the kernel's capture module, so that packets it does not need are discarded before they are ever copied to user space.

Desomnia builds this filter as a **positive whitelist**: it gathers the exact traffic each watched host and service depends on — the connection attempts that should trigger a wake, together with the address-resolution (ARP/NDP), Wake-on-LAN and ping traffic it needs to act on — and captures only that. Everything else is dropped in the kernel, so traffic Desomnia has no interest in imposes no overhead regardless of its volume. Copying packets from kernel to user space has a cost whether or not the application ends up using them; keeping the filter tight is what avoids that cost.

TCP services
++++++++++++

TCP is where the filter is most efficient. Every new TCP connection begins with a distinct **SYN** packet, so to notice a connection attempt Desomnia only needs to capture the SYNs directed at a watched service — the rest of the stream (payload, acknowledgements, retransmissions) is dropped in the kernel. A large file transfer over a watched TCP port therefore generates no load on Desomnia regardless of its size; only its opening packet is ever seen.

Each watched TCP service contributes its port to the whitelist, so the capture is restricted to exactly the ports in your configuration — subject to the :ref:`condition below <performance-unconditional>`.

UDP services
++++++++++++

UDP has no handshake: every datagram is independent, and no distinct packet marks the start of a "connection". Desomnia therefore treats *any* datagram arriving at a watched UDP port as a connection attempt. The kernel filter, however, is still restricted **by port** — only datagrams to the UDP ports you have configured are captured, exactly as for TCP, and unrelated UDP traffic on other ports is discarded in the kernel. (Earlier versions could only capture *all* UDP traffic once any UDP service was configured; per-port UDP filtering is now the default.)

The one thing to watch for is high-throughput traffic **on a watched port**: because every datagram there is passed up as a potential trigger, configuring a watched service on a port that also carries bulk UDP — video streaming, real-time feeds — can raise CPU usage. Keep watched UDP services off such ports; UDP on any other port costs nothing.

.. _performance-unconditional:

Hosts watched without service filters
+++++++++++++++++++++++++++++++++++++

Per-port precision — for both TCP and UDP — holds only while every watched host has at least one **Must** service filter: a ``<ServiceFilterRule type="Must">`` or the equivalent ``<Service>`` shorthand. Such a filter tells Desomnia the host should be woken only for specific ports, and those ports are all it needs to capture.

A host configured **without** any Must filter is watched *unconditionally* — it should wake on any connection attempt at all, so Desomnia has no choice but to capture that host's full demand baseline: every TCP SYN and all UDP traffic. Because the kernel filter is a single expression shared by the whole capture, one such host widens it for **every** host on the same address family — the per-port TCP and UDP restrictions dissolve, and the capture falls back to "any TCP SYN plus all UDP".

To keep the capture strictly port-scoped, give every watched host a Must service filter. Leaving a host unconditional remains perfectly valid; it simply trades kernel-side precision for the ability to wake on anything.

Filter complexity limits
++++++++++++++++++++++++

A compiled BPF program has a finite instruction budget, and a whitelist enumerating very many ports can exceed what libpcap will accept. Desomnia handles this automatically: it generates the filter from most precise to most general and installs the first variant the kernel accepts. If the fully port-precise filter is too large it drops TCP port precision, then UDP port precision, then falls back to capturing whole TCP streams, and — only as a last resort — installs no kernel filter at all and filters entirely in user space, logging a warning. Large configurations therefore degrade gracefully instead of failing outright; a warning in the log is the signal that the filter had to be coarsened.

Local resource management
-------------------------

There is one case where Desomnia deliberately keeps more than the opening packet. When a host is watched as a local resource for :doc:`sleep management </guides/sleep>` — Desomnia measuring its traffic to decide whether the system is idle — throughput can only be judged from the data itself, not from connection attempts. For such a host the SYN-only optimisation is lifted on its watched TCP services and the **full data stream** is captured so that bytes can be counted.

This does not disable the filter: the capture is still a positive whitelist scoped to the host's configured service ports (a local resource host always carries at least one Must service filter), and all other traffic is dropped in the kernel as usual. Only the payload of the watched services is added back.

Memory footprint
----------------

:OS: 🐧 *Linux* (arm64)

Running as an always-on :doc:`Wake-on-LAN / Sleep Proxy </guides/wol-proxy>` on a small device — a Raspberry Pi or comparable single-board computer — puts a premium on memory. The standard build runs on the .NET runtime, which compiles the application just-in-time as it runs; on a Raspberry Pi this settles at roughly **130 MB** of resident memory, most of which is the runtime and its compiler rather than Desomnia's own working set.

Native build
++++++++++++

For 64-bit Linux a **native build** is published alongside the standard one — the ``…_linux-x64-native.zip`` and ``…_linux-arm64-native.zip`` assets on the `releases page <https://github.com/mad0x20wizard/Desomnia/releases>`__. It is compiled ahead of time into a single self-contained executable, with no just-in-time compiler and no separate .NET runtime. This cuts the resident footprint to around **48 MB**, removes the runtime dependency entirely, and shortens startup, since no compilation happens at launch.

Use it when Desomnia runs on a memory-constrained 64-bit device — whether as an always-on monitor or proxy, or as a host that sleeps itself. In exchange for the smaller footprint it carries a few constraints:

- **64-bit Linux only.** The native build targets ``linux-x64`` and ``linux-arm64``; 32-bit systems and other platforms use the standard build.
- **A recent system.** It requires *glibc 2.35 or newer* — Debian 12 "Bookworm", Ubuntu 22.04, Raspberry Pi OS (Bookworm) and later. On older systems, use the standard build.
- **No dynamic plugins.** Plugins cannot be loaded at runtime; the :doc:`Firewall Knock Operator </plugins/fko>` is built in, but other plugins are unavailable in this build.

Everything else — packet capture, the Berkeley Packet Filter whitelist described above, the Sleep Proxy, and :doc:`local sleep management </guides/sleep>` via ``systemd-logind`` — behaves exactly as in the standard build.