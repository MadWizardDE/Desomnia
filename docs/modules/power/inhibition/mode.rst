``block``
  The operation is completely prevented for as long as the lock is held. Privileged processes can override this, but unprivileged applications cannot.

``block-weak``
  A weaker variant of ``block`` that can be overridden by the system or a privileged process without releasing the lock. Used when an application wants to signal preference but not enforce it unconditionally.

``delay``
  Does not prevent the operation, but delays it by a short window (configured in ``logind.conf`` via ``InhibitDelayMaxSec``). The application is expected to perform any necessary cleanup within this window and then release the lock. Used by media players that need to flush buffers before a suspend.
