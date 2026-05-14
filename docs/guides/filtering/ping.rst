Filter ping traffic
~~~~~~~~~~~~~~~~~~~

Some operating systems and network tools send ICMP echo requests (pings) to check whether a host is reachable. These operate at the IP layer, before any TCP or UDP connection is established, and can trigger an unwanted wake-up if they happen to target a watched host.

If you have already configured at least one ``type="Must"`` service filter, or used ``<Service>`` declarations, ping traffic is automatically excluded — no further configuration is needed.

Without any inclusive service filter, you can suppress pings explicitly:

.. code:: xml

   <NetworkMonitor>
     <PingFilterRule />
     <RemoteHost name="server" MAC="00:1A:2B:3C:4D:5E" IPv4="192.168.1.10" />
   </NetworkMonitor>

Placing the rule at the ``<NetworkMonitor>`` level applies it to all watched hosts. To limit it to a specific host, place the rule inside the corresponding ``<RemoteHost>`` element.
