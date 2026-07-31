Available actions
=================

Process
-------

stop
++++

:🔥 action:

:optional: ``timeout``

This action causes the group of processes to stop. If an optional timeout is specified, Desomnia will first attempt to terminate the processes gracefully. Any remaining processes will eventually be terminated when the timeout elapses. Without a timeout there is no grace period: the processes are terminated outright, since asking and terminating in the same moment would give them no chance to act on it.

What "gracefully" means differs by platform. On 🪟 *Windows* the process is asked to close its main window; a process without one cannot be asked and is terminated straight away. On 🐧 *Linux* and 🍎 *macOS* it is sent ``SIGTERM``, which a process can act on to shut down in an orderly way — but only if it chose to handle it, and a desktop application generally does not. On macOS in particular this is not the same as the quit an application receives from the Dock: that would offer to save unsaved documents, and it is not something a system daemon is permitted to send.