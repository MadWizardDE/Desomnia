namespace MadWizard.Desomnia.Environments
{
    /// <summary>When an environment block merges (its onlyIf attribute).</summary>
    internal enum EnvironmentMergeMode
    {
        /// <summary>Merged whenever the block's conditions match; for a default block: always (the default).</summary>
        Always,

        /// <summary>Merged only when no other environment matches (&lt;DefaultEnvironment&gt; only).</summary>
        Else,

        /// <summary>Never merged - disables the block without deleting it.</summary>
        Never,
    }
}
