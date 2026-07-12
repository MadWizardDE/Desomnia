sleepProxyDiscovery
+++++++++++++++++++

:default: ``eager``

Controls *when* a client looks for a proxy to register with:

``eager``
  Discover a proxy up front and keep the registration current, so handoff at suspend time is immediate.
``lazy``
  Defer discovery until the host is actually about to suspend. This avoids background traffic at the cost of a slightly slower suspend.