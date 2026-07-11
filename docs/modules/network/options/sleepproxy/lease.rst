sleepProxyLeaseExpire
+++++++++++++++++++++

:default: ``wake``

What the proxy does when a lease reaches its end without the host having come back on its own:

``none``
  Simply release the lease. If the host is still asleep, its services stop being advertised until it registers again.
``wake``
  Send a Magic Packet to wake the host before releasing the lease — but only if the host is not already responding. A woken host reclaims its presence and, once it idles again, suspends with a fresh registration; a registered host therefore never silently disappears from the network. This mirrors the behaviour of Apple's proxies, which always wake a host whose lease runs out.

sleepProxyLeaseDurationMin
++++++++++++++++++++++++++

:inherited:
:default: ``30min``

The shortest lease the proxy will grant. Requests below this are rounded up.

sleepProxyLeaseDurationMax
++++++++++++++++++++++++++

:default: ``365d``

The longest lease the proxy will grant. Requests above this are capped.

sleepProxyLeaseLimit
++++++++++++++++++++

:default: ``100``

The maximum number of simultaneous leases. Once the pool is exhausted, further registrations are refused until a lease ends. This bounds the resources a single proxy commits to sleeping hosts.
