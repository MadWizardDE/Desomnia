namespace MadWizard.Desomnia.Display.Manager
{
    /// <summary>
    /// The identity guarantee behind <see cref="IDisplayManager"/>: a disconnected display
    /// is remembered — weakly — for as long as anyone still references the instance, and a
    /// reconnect with an equal <see cref="DisplayIdentity"/> recalls that very instance, so
    /// upper layers never need identity checks beyond reference equality. Once the last
    /// reference is collected the entry clears itself, and a later reconnect gets a fresh
    /// instance.
    ///
    /// Remembered per instance, not per identity: identical panels (garbage EDID serials!)
    /// may disconnect side by side, and a recall simply hands back one of the matching
    /// twins — correct precisely because they are physically indistinguishable.
    ///
    /// Not thread-safe on purpose — call under the manager's lock.
    /// </summary>
    public class DisplayMemory<TDisplay> where TDisplay : class, IDisplayExternal
    {
        private readonly List<WeakReference<TDisplay>> _remembered = [];

        /// <summary>Remembers a display that just disconnected.</summary>
        public void Remember(TDisplay display)
        {
            Prune();

            _remembered.Add(new WeakReference<TDisplay>(display));
        }

        /// <summary>
        /// Recalls — and forgets — a still-referenced display with this identity, or null
        /// if none survived. Most-recently-remembered first: a hot-plug re-negotiation
        /// pulse must recall the very instance it just remembered, never an older twin
        /// that happens to share the identity.
        /// </summary>
        public TDisplay? Recall(DisplayIdentity identity)
        {
            Prune();

            for (int i = _remembered.Count - 1; i >= 0; i--)
            {
                if (_remembered[i].TryGetTarget(out TDisplay? display) && display.Identity == identity)
                {
                    _remembered.RemoveAt(i);

                    return display;
                }
            }

            return null;
        }

        private void Prune()
        {
            _remembered.RemoveAll(reference => !reference.TryGetTarget(out _));
        }
    }
}
