using System.Diagnostics;

namespace MadWizard.Desomnia.Processes.Manager
{
    /**
     * A process as the platform's own enumeration sees it: always an id, plus whatever identity
     * that enumeration happened to pay for already.
     *
     * Platforms whose cheapest answer is a fully materialized System.Diagnostics.Process hand
     * it over, so the refresh never asks the OS twice. Platforms that can list ids with a single
     * syscall yield bare entries and describe only the ids the refresh finds genuinely new
     * (see ProcessManager.QueryProcess) – on those, most processes never become a
     * System.Diagnostics.Process at all.
     */
    public readonly record struct ProcessInformation(int Id)
    {
        public static implicit operator ProcessInformation(int id) => new(id);
        public static implicit operator ProcessInformation(Process native) => new(native);

        public ProcessInformation(Process native) : this(native.Id) { Native = native; }

        /**
         * How far up an ancestry we are willing to walk. Real process trees are nowhere near this
         * deep; the limit is there because a platform that ever reported a parent loop would take
         * the whole daemon down with a stack overflow, which is not an exception anybody can catch.
         */
        internal uint MaxParents { get; init; } = 32;

        /// <summary>The BCL process object, when the enumeration produced one anyway.</summary>
        public Process? Native { get; init; }

        /// <summary>The image name. When null, it is read from <see cref="Native"/>.</summary>
        public string? Name { get => field ?? Native?.ProcessName; init; }

        /// <summary>The session the process belongs to. When null, it is read from <see cref="Native"/>.</summary>
        public int? SessionId { get => field ?? Native?.SessionId; init; }

        /// <summary>The parent process id, when the platform already knows it – see <see cref="ProcessManager.QueryParentProcess"/>.</summary>
        public int? ParentId { get; init; }

        public string? ImagePath { get; init; }

    }
}
