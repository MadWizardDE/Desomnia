With the minimal configuration in place, Desomnia reacts to any connection directed at the watched host's IP address. Depending on your operating system and running applications, background traffic — such as network discovery, address resolution, or periodic health checks — can occasionally match and trigger an unwanted wake-up.

The following sections explain how to narrow this down to the connections you actually care about.

Filter by service
~~~~~~~~~~~~~~~~~

The most reliable way to prevent unwanted wake-ups is to restrict Desomnia to specific services by port number. Adding a ``<ServiceFilterRule>`` with ``type="Must"`` makes it an inclusive filter: only connections to that specific port will trigger a wake-up.

.. code:: xml

   <RemoteHost name="server" MAC="00:1A:2B:3C:4D:5E" IPv4="192.168.1.10">
     <ServiceFilterRule name="SSH" port="22" type="Must" />
   </RemoteHost>

This configuration wakes ``"server"`` only when a TCP connection to port 22 is detected. All other connections to that host are ignored.

To react to multiple services, add one rule per port:

.. code:: xml

   <RemoteHost name="server" MAC="00:1A:2B:3C:4D:5E" IPv4="192.168.1.10">
     <ServiceFilterRule name="SSH"  port="22"   type="Must" />
     <ServiceFilterRule name="SMB"  port="445"  type="Must" />
     <ServiceFilterRule name="RDP"  port="3389" type="Must" />
     <ServiceFilterRule name="HTTP" port="80"   type="Must" />
   </RemoteHost>

As long as at least one inclusive rule is present, any connection to a port not listed is automatically ignored.

For UDP-based services, set ``protocol="UDP"`` explicitly:

.. code:: xml

   <ServiceFilterRule name="DNS" protocol="UDP" port="53" type="Must" />

.. hint::
   The ``name`` attribute on a ``<ServiceFilterRule>`` is optional and purely descriptive. It appears in log output and helps identify which rule triggered an event.

Excluding specific services
^^^^^^^^^^^^^^^^^^^^^^^^^^^

If only a small number of services cause unwanted noise, you may prefer the opposite approach: leave the default behaviour in place — wake on any connection — and explicitly exclude the services you want to ignore using ``type="MustNot"``:

.. code:: xml

   <RemoteHost name="server" MAC="00:1A:2B:3C:4D:5E" IPv4="192.168.1.10">
     <ServiceFilterRule name="Monitoring" port="8080" type="MustNot" />
   </RemoteHost>

Connections to port 8080 are ignored; all other connections to this host can still trigger a wake-up.

.. note::
   ``type="MustNot"`` is the default for all filter rules when no type is set explicitly. The two approaches — whitelisting with ``type="Must"`` and blacklisting with ``type="MustNot"`` — can be mixed, but the interaction can be difficult to reason about: as soon as at least one ``type="Must"`` rule is present, the filter switches to whitelist mode and ``type="MustNot"`` rules act only as exclusions within that whitelist.

.. proxy ..

Shorter notation with ``<Service>``
^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^

When all your rules use ``type="Must"``, the ``<Service>`` shorthand is equivalent and less verbose:

.. code:: xml

   <RemoteHost name="server" MAC="00:1A:2B:3C:4D:5E" IPv4="192.168.1.10">
     <Service name="SSH"  port="22" />
     <Service name="SMB"  port="445" />
     <Service name="RDP"  port="3389" />
     <Service name="HTTP" port="80" />
   </RemoteHost>

Each ``<Service>`` element is equivalent to a ``<ServiceFilterRule>`` with ``type="Must"``.

Beyond the shorter notation, a ``<Service>`` is a full service *definition* rather than a bare filter rule: Desomnia can advertise it via :doc:`multicast DNS </modules/network/mdns>` (Bonjour) while the host is asleep. Clients that rely on service discovery — such as macOS, printers, or media players — then keep finding the service, and the connection attempt that follows wakes the host. This is the same mechanism as Apple's *Sleep Proxy* protocol, which Desomnia :doc:`fully implements </modules/network/sleepproxy>`.

The following configuration options control this behaviour:

``advertiseServices``
  Enables mDNS advertising of the declared services of a sleeping host. Advertising is **off by default**; set ``advertiseServices="true"`` on the ``<NetworkMonitor>`` to enable it for all hosts, or on an individual host to enable (or suppress) it selectively.
``serviceName``
  The DNS-SD service type a ``<Service>`` is advertised as — for example ``serviceName="smb"`` yields ``_smb._tcp``. If omitted, the lowercase ``name`` is used. Ideally this matches the IANA-registered service name.
``handoff``
  If the watched host itself runs Desomnia, it can instead :doc:`register its services with the proxy </modules/network/handoff>` just before it suspends (``handoff="SleepProxy"`` on the host), making the static declarations on the proxy unnecessary. Individual services are excluded from that registration with ``handoff="false"`` on the ``<Service>`` element.
