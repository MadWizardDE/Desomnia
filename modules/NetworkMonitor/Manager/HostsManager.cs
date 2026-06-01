using Microsoft.Extensions.Logging;
using System.Net;

namespace MadWizard.Desomnia.Network.Manager
{
    public class HostsManager(string path) : IStaticNameMapping, IDisposable
    {
        public required ILogger<HostsManager> LoggerHosts { private get; init; }

        /*
         * Host name -> IP mappings are written into the Windows "hosts" file. To keep our edits
         * isolated from any other (manual or third-party) entries, every mapping managed by
         * Desomnia lives strictly between the marker lines below. We never touch anything outside
         * of this block. The markers use the hosts file comment character '#', so they are ignored
         * by the name resolver itself.
         */
        private const string BlockStartMarker = "# --- BEGIN Desomnia managed hosts (do not edit – automatically generated) ---";
        private const string BlockEndMarker = "# --- END Desomnia managed hosts ---";

        // The hosts file is a process-wide resource; serialize all read-modify-write cycles so that
        // concurrent cache instances (one per network) cannot corrupt each other's edits.
        private static readonly object HostsFileLock = new();

        // Host names this instance added, so that we can guarantee their removal on shutdown,
        // regardless of whether Delete() was ever called for them.
        private readonly HashSet<string> _managedNames = [with(StringComparer.OrdinalIgnoreCase)];

        void IStaticNameMapping.Update(string name, IPAddress ip)
        {
            EditHostsFile(lines =>
            {
                var (start, end) = EnsureBlock(lines);

                // Drop any previous mapping for this host name within our block before re-adding it.
                RemoveEntries(lines, start, ref end, name);

                lines.Insert(end, FormatEntry(ip, name));

                _managedNames.Add(name);
                return true;
            });

            LoggerHosts.LogTrace($"Mapped host '{name}' to {ip} in hosts file");
        }

        void IStaticNameMapping.Delete(string name)
        {
            EditHostsFile(lines =>
            {
                var (start, end) = FindBlock(lines);
                if (start < 0)
                    return false;

                if (!RemoveEntries(lines, start, ref end, name))
                    return false;

                RemoveBlockIfEmpty(lines, start, end);
                return true;
            });

            _managedNames.Remove(name);

            LoggerHosts.LogTrace($"Removed host '{name}' from hosts file");
        }

        public void Dispose()
        {
            if (_managedNames.Count == 0)
                return;

            // On shutdown remove every entry we ever added, even those for which Delete() was not
            // called, so that no stale Desomnia mappings are left behind in the hosts file.
            EditHostsFile(lines =>
            {
                var (start, end) = FindBlock(lines);
                if (start < 0)
                    return false;

                bool changed = false;
                foreach (var name in _managedNames)
                    changed |= RemoveEntries(lines, start, ref end, name);

                if (changed)
                    RemoveBlockIfEmpty(lines, start, end);

                return changed;
            });

            _managedNames.Clear();
        }

        /// <summary>Reads the hosts file, applies <paramref name="edit"/> and writes it back when the edit reports a change.</summary>
        private void EditHostsFile(Func<List<string>, bool> edit)
        {
            lock (HostsFileLock)
            {
                List<string> lines;
                try
                {
                    lines = File.Exists(path) ? [.. File.ReadAllLines(path)] : [];
                }
                catch (Exception ex)
                {
                    LoggerHosts.LogError($"Failed to read hosts file \"{path}\" – {ex.Message}");
                    return;
                }

                if (!edit(lines))
                    return;

                try
                {
                    File.WriteAllLines(path, lines);
                }
                catch (Exception ex)
                {
                    LoggerHosts.LogError($"Failed to write hosts file \"{path}\" – {ex.Message}");
                }
            }
        }

        /// <summary>Returns the indices of the start and end marker, or (-1, -1) when the block is absent.</summary>
        private static (int start, int end) FindBlock(List<string> lines)
        {
            int start = lines.FindIndex(line => line.Trim() == BlockStartMarker);
            if (start < 0)
                return (-1, -1);

            int end = lines.FindIndex(start + 1, line => line.Trim() == BlockEndMarker);
            return end < 0 ? (-1, -1) : (start, end);
        }

        /// <summary>Locates the managed block, appending a fresh one at the end of the file when none exists.</summary>
        private static (int start, int end) EnsureBlock(List<string> lines)
        {
            var (start, end) = FindBlock(lines);
            if (start >= 0)
                return (start, end);

            if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
                lines.Add(string.Empty);

            lines.Add(BlockStartMarker);
            lines.Add(BlockEndMarker);

            return (lines.Count - 2, lines.Count - 1);
        }

        /// <summary>Removes every entry for <paramref name="name"/> between the markers, adjusting <paramref name="end"/>.</summary>
        private static bool RemoveEntries(List<string> lines, int start, ref int end, string name)
        {
            bool removed = false;
            for (int i = end - 1; i > start; i--)
            {
                if (IsEntryForName(lines[i], name))
                {
                    lines.RemoveAt(i);
                    end--;
                    removed = true;
                }
            }
            return removed;
        }

        /// <summary>Removes the marker lines when no entries remain between them.</summary>
        private static void RemoveBlockIfEmpty(List<string> lines, int start, int end)
        {
            if (end == start + 1)
            {
                lines.RemoveAt(end);   // end marker
                lines.RemoveAt(start); // start marker
            }
        }

        private static bool IsEntryForName(string line, string name)
        {
            var tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            // hosts entry: "<ip> <name> [aliases...]"; first token is the address, the rest are names.
            for (int i = 1; i < tokens.Length; i++)
            {
                if (string.Equals(tokens[i], name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string FormatEntry(IPAddress ip, string name) => $"{ip}\t{name}";
    }
}
