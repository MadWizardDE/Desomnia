``MAC``
  Attempt to discover the MAC address of configured hosts and routers.
``IPv4``
  Attempt to discover the IPv4 address of configured hosts and routers.
``IPv6``
  Attempt to discover the IPv6 address(es) of configured hosts and routers.
``Router``
  Attempt to locate the acting network router automatically, so that you can omit a ``<Router>`` element from your configuration.
🚧 ``VPN``
  Attempt to discover VPN devices connected to your router (if possible).
``SleepProxy``
  Discover :doc:`Sleep Proxies </modules/network/sleepproxy>` on the network, so this host can register its services with one before it suspends. See :doc:`handoff </modules/network/handoff>` for how registration is triggered.
``Service``
  Learn services dynamically and attach them to **hosts that already exist** in the configuration. On a :doc:`Sleep Proxy </modules/network/sleepproxy>`, this permits a registering host to add services to its known host definition.
``Host``
  Allow **new host definitions to be created** dynamically for hosts that are not configured statically. On a Sleep Proxy this lets a host that has never been seen register from scratch; combined with ``Service`` the proxy needs no per-host configuration at all.

You may also use ``nothing`` to disable all discovery, or ``everything`` to enable all available options.
