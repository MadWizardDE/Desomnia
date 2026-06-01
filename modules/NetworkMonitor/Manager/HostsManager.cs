using Microsoft.Extensions.Logging;
using System.Net;

namespace MadWizard.Desomnia.Network.Manager
{
    public class HostsManager(string path) : IStaticNameMapping, IDisposable
    {
        public required ILogger<HostsManager> Logger { private get; init; }

        private readonly Dictionary<string, List<IPAddress>> _mappings = new(StringComparer.OrdinalIgnoreCase);

        void IStaticNameMapping.Update(string name, IPAddress ip)
        {
            lock (_mappings)
            {
                if (!_mappings.TryGetValue(name, out var ips))
                    _mappings[name] = ips = [];

                if (!ips.Contains(ip))
                {
                    ips.Add(ip);

                    Logger.LogTrace("Inserted mapping '{name}' -> {ip}", name, ip);

                    Flush();
                }
            }
        }

        void IStaticNameMapping.Delete(string name)
        {
            lock (_mappings)
            {
                if (_mappings.Remove(name))
                {
                    Logger.LogTrace("Removed mappings for '{name}'", name);

                    Flush();
                }
            }
        }

        void IDisposable.Dispose()
        {
            lock (_mappings)
            {
                if (_mappings.Count > 0)
                {
                    // On shutdown drop every mapping we ever added – even those for which Delete() was
                    // not called – so that no stale Desomnia entries are left behind in the hosts file.
                    _mappings.Clear();

                    Flush();
                }
            }
        }

        #region Hosts file read/write
        /*
         * Host name -> IP mappings are written into the "hosts" file. To keep our edits isolated
         * from any other (manual or third-party) entries, every mapping managed by Desomnia lives
         * strictly between the marker lines below. We never touch anything outside of this block.
         * The markers use the hosts file comment character '#', so they are ignored by the name
         * resolver itself.
         */
        private const string BlockStartMarker   = "# DESOMNIA-BEGIN";
        private const string BlockEndMarker     = "# DESOMNIA-END";

        /// <summary>
        /// Rewrites the managed block in the hosts file from the current in-memory mapping: everything
        /// between the markers is discarded and replaced with the freshly exported entries. The block
        /// (and its markers) is removed entirely when no mappings remain. Must be called under <see cref="_hostsFileLock"/>.
        /// </summary>
        private void Flush()
        {
            try
            {
                List<string> lines = File.Exists(path) ? [.. File.ReadAllLines(path)] : [];

                // Strip the previously written block (markers + content), wherever it sits in the file.
                var (start, end) = FindBlock(lines);
                if (start >= 0)
                    lines.RemoveRange(start, end - start + 1);

                // Re-create the block from the current mapping, reusing the original position if we had one.
                List<string> entries = [.. ExportEntries()];
                if (entries.Count > 0)
                {
                    List<string> block = [BlockStartMarker, .. entries, BlockEndMarker];

                    if (start >= 0)
                    {
                        lines.InsertRange(start, block);
                    }
                    else
                    {
                        if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
                            lines.Add(string.Empty);

                        lines.AddRange(block);
                    }
                }

                try
                {
                    File.WriteAllLines(path, lines);
                }
                catch (Exception ex)
                {
                    throw new HostsFileException($"Failed to write hosts file \"{path}\"", ex);
                }
            }
            catch (Exception ex)
            {
                throw new HostsFileException($"Failed to read hosts file \"{path}\"", ex);
            }
        }

        /// <summary>Projects the in-memory mapping into hosts file entry lines, ordered for a stable file layout.</summary>
        private IEnumerable<string> ExportEntries()
        {
            foreach (var (name, ips) in _mappings.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
                foreach (var ip in ips)
                    yield return FormatEntry(ip, name);
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

        private static string FormatEntry(IPAddress ip, string name) => $"{ip} {name}";
        #endregion
    }

    public class HostsFileException(string? message, Exception? innerException) : Exception(message, innerException) { }
}
