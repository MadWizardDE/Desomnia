sleepProxyPort
++++++++++++++

The UDP port the Sleep Proxy service listens on. Clients discover the port through the proxy's DNS-SD advertisement, so it does not normally need to be fixed: when unset, Desomnia reserves an ephemeral port from the operating system. Setting an explicit port makes the endpoint predictable (for example for firewall rules); the port is then bound *shareable*, so it may coexist with other programs on the machine that also bind it reusably — such as an OS-level mDNS responder when using ``5353``.
