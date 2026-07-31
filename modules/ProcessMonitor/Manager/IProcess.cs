namespace MadWizard.Desomnia.Processes.Manager
{
    /**
     * What the monitor needs to know about a process – deliberately not a System.Diagnostics.Process.
     *
     * Every member below is something a platform can answer on its own terms, and every one of them
     * an implementation is free to answer lazily: FilePath and ProcessorTime are the expensive two,
     * and the vast majority of the processes on a machine are never asked either.
     */
    public interface IProcess
    {
        int Id { get; }
        int SessionId { get; }

        string Name { get; }

        string? ImagePath { get; }

        /// <summary>Processor time consumed since the process started, or null if it cannot be sampled (any more).</summary>
        TimeSpan? ProcessorTime { get; }

        IProcess? Parent { get; }
        bool HasParent(IProcess parent)
        {
            IProcess process = this;

            while (process.Parent != null)
            {
                if (process.Parent == parent)
                    return true;

                process = process.Parent;
            }

            return false;
        }

        bool HasStopped { get; }
        Task Stop(TimeSpan timeout = default);
        event EventHandler Stopped;
    }
}
