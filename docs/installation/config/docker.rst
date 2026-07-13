.. code:: yaml

  services:
    desomnia:
      image: mad0x20wizard/desomnia

      volumes:
        - ./config:/etc/desomnia              # optional; provide your own monitor.xml to override the default
        - ./plugins:/var/lib/desomnia/plugins # optional
        - ./logs:/var/log/desomnia            # optional

      restart: unless-stopped

      network_mode: host

      cap_add:
        - NET_RAW
        - NET_ADMIN
