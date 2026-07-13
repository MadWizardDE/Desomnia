/etc/desomnia
    Configuration directory. Place your ``monitor.xml`` here; you can also add an
    ``NLog.config`` for additional :doc:`logging </concepts/logging>`.

/var/log/desomnia
    Log output, if file :doc:`logging </concepts/logging>` is enabled in ``NLog.config`` and used ``${var:logDir}`` as base path.

.. plugins ..

/var/lib/desomnia/plugins
    Drop your additional plugins here; these will be loaded when the programs starts.
