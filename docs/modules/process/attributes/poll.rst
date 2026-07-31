pollInterval
++++++++++++

:⏱️ duration:
:default: ``2s``

Sets the interval at which the Process Monitor polls the OS for process changes.

On Windows, setting ``pollInterval`` explicitly disables ETW and switches to polling. This may be useful for testing or in environments where ETW is not available.

On macOS and Linux, where no event-based alternative is used, this is the detection latency for a process starting. A tick costs a single system call there, so short intervals are affordable.
