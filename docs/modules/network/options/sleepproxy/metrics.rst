sleepProxyMetrics
+++++++++++++++++

:default: ``best``

The metric this proxy advertises. You may use one of the shorthands ``best``, ``average`` or ``worst``, or specify the four fields explicitly as ``intent-portability-marginalPower-totalPower`` (each ``10``–``99``), following Apple's convention — for example ``30-40-70-70``. Lower is more preferred. Leave this at ``Best`` if this device is the intended proxy for the segment; raise it if the machine is only an opportunistic fallback.