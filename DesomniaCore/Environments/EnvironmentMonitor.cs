using Autofac;
using Autofac.Core;
using MadWizard.Desomnia.Configuration.Binding;
using Microsoft.Extensions.Configuration.Xml;
using NLog;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace MadWizard.Desomnia.Environments
{
    /// <summary>
    /// The single authority over the running configuration. It resolves the configuration
    /// file into the effective &lt;SystemMonitor&gt; the application boots from, watches
    /// everything that can change that result — the file itself (auto-reload) and, in the
    /// &lt;EnvironmentMonitor&gt; mode, the live condition states — and owns the reload signal
    /// the application loop rebuilds on. It is a persistent citizen: created once with the
    /// constant configuration path, resolved as a managed component and kept across rebuilds,
    /// so the change sources are not stopped and restarted on every rebuild.
    ///
    /// <para>Two modes: the &lt;EnvironmentMonitor&gt; root <em>augments</em> — the condition
    /// attributes of all &lt;Environment&gt; blocks are evaluated and the active blocks merged
    /// into one effective &lt;SystemMonitor&gt;; any other root (the classic &lt;SystemMonitor&gt;)
    /// is <em>passthrough</em> — the file is served verbatim. Both watch the file for edits when
    /// auto-reload is enabled, and both serve an in-memory snapshot to each build, so a rebuild
    /// never reads a half-written file.</para>
    /// </summary>
    public sealed class EnvironmentMonitor : IDisposable
    {
        static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        // the file watcher coalesces its raw events over this window, so a rebuild reads the file
        // only after the editor has finished writing it
        static readonly TimeSpan FileDebounce = TimeSpan.FromMilliseconds(500);

        readonly string _configPath;
        readonly Lock _lock = new();

        // true for an <EnvironmentMonitor> root (augmenting mode); false for a classic
        // <SystemMonitor> (passthrough — the file is served and watched, never merged)
        readonly bool _augmenting;

        // the parsed configuration (augmenting mode); replaced wholesale by a reload on a file edit
        string _version = "1";
        TimeSpan _debounce;
        string? _outputPath;
        ConflictResolution _onConflict;
        IReadOnlyList<EnvironmentBlock> _blocks = [];

        // the raw file text the current configuration was last read from — a spurious watcher
        // event that reports the same content skips the (condition-flapping) re-parse
        string? _readText;

        IReadOnlySet<string> _collectionElements = new HashSet<string>();

        // the effective configuration served to each build: the merged <SystemMonitor> (augmenting)
        // or the file text (passthrough). A snapshot, so a mid-write file never reaches a build.
        string? _effectiveConfig;
        string? _writtenPath;

        // auto-reload (the file watch): a process-bound decision the builder sets from the
        // command line, or the platform sets directly (the Windows service always reloads)
        bool _watchFile;
        string? _watchPath;

        // the persistent container the conditions resolve from (a reload begins a fresh child
        // scope from it); null until Activate is called (the test-only Apply path leaves it null)
        ILifetimeScope? _parentScope;
        ILifetimeScope? _conditions;

        EnvironmentWatcher? _conditionWatcher;
        FileSystemWatcher? _fileWatcher;
        Timer? _fileDebounce;

        // the reload signal (it replaces the old RestartSignal): the application loop links its
        // run against ReloadToken, and a change — a condition or a file edit — cancels it. InjectInto
        // arms a fresh token per inner build, so a change that lands during a build's startup
        // cancels that build's token and is never lost (both take _lock).
        CancellationTokenSource _reload = new();

        bool _disposed;

        private EnvironmentMonitor(string configPath, bool augmenting)
        {
            _configPath = configPath;
            _augmenting = augmenting;
        }

        private EnvironmentMonitor(string configPath, EnvironmentParser.Result config, string? outputPath)
            : this(configPath, augmenting: true)
        {
            Adopt(config, outputPath);
        }

        private void Adopt(EnvironmentParser.Result config, string? outputPath)
        {
            _version = config.Version;
            _debounce = config.Debounce;
            _outputPath = outputPath;
            _onConflict = config.OnConflict;
            _blocks = config.Blocks;
        }

        /// <summary>
        /// Inspects the configuration file's root element and builds the matching monitor: an
        /// augmenting monitor for the &lt;EnvironmentMonitor&gt; root, otherwise null (the classic
        /// &lt;SystemMonitor&gt; or any file the passthrough pipeline should handle verbatim — see
        /// <see cref="Passthrough"/>). Throws for an unknown root.
        /// </summary>
        public static EnvironmentMonitor? Detect(string configPath)
        {
            if (!File.Exists(configPath))
                return null; // let the configuration provider report the missing file

            string rootName;
            try
            {
                using var reader = XmlReader.Create(configPath);
                reader.MoveToContent();
                rootName = reader.LocalName;
            }
            catch (XmlException)
            {
                return null; // legacy dialect quirks (e.g. value-less attributes); handled by the provider
            }

            if (rootName.Equals(EnvironmentParser.SYSTEM_MONITOR_ELEMENT, StringComparison.OrdinalIgnoreCase))
                return null;

            if (!rootName.Equals(EnvironmentParser.ROOT_ELEMENT, StringComparison.OrdinalIgnoreCase))
                throw new ConfigurationValueException($"Unknown configuration root element <{rootName}>; " +
                    $"expected <{EnvironmentParser.SYSTEM_MONITOR_ELEMENT}> or <{EnvironmentParser.ROOT_ELEMENT}>.");

            XDocument document;
            try
            {
                document = XDocument.Load(configPath);
            }
            catch (XmlException ex)
            {
                throw new ConfigurationValueException($"'{configPath}' is not well-formed XML. Note that value-less " +
                    $"attributes (e.g. \"must\") are not supported below an <{EnvironmentParser.ROOT_ELEMENT}> root; " +
                    $"write them with an explicit value (e.g. type=\"Must\") instead.", ex);
            }

            var config = EnvironmentParser.Parse(document);

            return new EnvironmentMonitor(configPath, config, ResolveOutputPath(configPath, config.OutputEffectiveXML));
        }

        /// <summary>The passthrough monitor for a classic &lt;SystemMonitor&gt; (or legacy) config:
        /// no augmentation, the file is served verbatim and — when auto-reload is on — watched for
        /// edits. The application always has a monitor; <see cref="Detect"/> handles the augmenting
        /// case, this the rest.</summary>
        public static EnvironmentMonitor Passthrough(string configPath) => new(configPath, augmenting: false);

        /// <summary>Enables the configuration-file watch (auto-reload). A process-bound decision:
        /// set before <see cref="Activate"/>, from the command line or the platform (the Windows
        /// service always reloads). <paramref name="watchPath"/> watches a different path than the
        /// configuration file, but the effective configuration is still read from the file.</summary>
        public void EnableAutoReload(bool enabled, string? watchPath = null)
        {
            _watchFile = enabled;
            _watchPath = watchPath;
        }

        /// <summary>Resolves the output path relative to the configuration file, unless it is absolute.</summary>
        private static string? ResolveOutputPath(string configPath, string? outputEffectiveXML)
        {
            if (outputEffectiveXML is null)
                return null;

            string configFullPath = Path.GetFullPath(configPath);

            string outputPath = Path.GetFullPath(outputEffectiveXML, Path.GetDirectoryName(configFullPath)!);

            // overwriting the configuration would also feed the config file watcher a restart loop
            if (string.Equals(outputPath, configFullPath, StringComparison.OrdinalIgnoreCase))
                throw new ConfigurationValueException($"{EnvironmentParser.OUTPUT_EFFECTIVE_XML_ATTRIBUTE} " +
                    $"must not point at the configuration file itself ({configFullPath}).");

            return outputPath;
        }

        /// <summary>
        /// Resolves the blocks' conditions out of the given scope (keyed by attribute name,
        /// see <see cref="IEnvironmentCondition"/>), evaluates them and injects the resulting
        /// effective configuration into the given source. One-shot form used by the tests;
        /// the running application uses <see cref="Activate"/> + <see cref="InjectInto"/>.
        /// </summary>
        internal void Apply(ExtendedXmlConfigurationSource source, ILifetimeScope conditions)
        {
            _conditions = conditions;

            ResolveConditions(conditions, _blocks);

            // module knowledge needed to distinguish collection items from singleton elements
            _collectionElements = new HashSet<string>(source.CollectionElements, StringComparer.OrdinalIgnoreCase);

            _effectiveConfig = Compute(_blocks, out _);

            InjectInto(source);
        }

        /// <summary>
        /// Resolves the conditions once (augmenting mode), computes the initial effective
        /// configuration and starts watching — the file (when auto-reload is on) and, in the
        /// augmenting mode, the conditions. Called once, at boot; the watchers then live for the
        /// whole process (surviving configuration rebuilds).
        /// </summary>
        internal void Activate(IReadOnlySet<string> collectionElements, ILifetimeScope persistent)
        {
            lock (_lock)
            {
                _parentScope = persistent;
                _collectionElements = collectionElements;

                // the change-detection baseline for both modes, so a spurious first watcher event
                // (unchanged content) does not re-parse or reload
                _readText = TryReadFile();

                if (_augmenting)
                {
                    // a child scope, so disposing the conditions (here and on a reload) never
                    // touches the persistent container itself
                    _conditions = persistent.BeginLifetimeScope();

                    ResolveConditions(_conditions, _blocks);

                    _effectiveConfig = Compute(_blocks, out _);

                    _conditionWatcher = CreateConditionWatcher(_blocks, _debounce);
                }
                // passthrough leaves the effective config unset and lets the build's provider read
                // the file (which keeps the legacy value-less-attribute dialect working)

                StartFileWatching();
            }
        }

        /// <summary>Points the given per-build source at the current effective configuration and
        /// arms a fresh reload token for that build. When no snapshot is available yet (a passthrough
        /// file that could not be read), the source is left to read the file itself.</summary>
        internal void InjectInto(ExtendedXmlConfigurationSource source)
        {
            lock (_lock)
            {
                // arm a fresh token bound to this build; a later change cancels it (and never the
                // previous run's, which has already ended)
                var previous = _reload;
                _reload = new CancellationTokenSource();
                previous.Dispose();

                if (_effectiveConfig is not string snapshot)
                    return; // no snapshot: let the provider read the file (it reports a missing one)

                source.EffectiveConfiguration = () => new MemoryStream(Encoding.UTF8.GetBytes(snapshot));
            }
        }

        /// <summary>The reload signal: the application loop links its run against this token, and a
        /// change — a condition or a file edit — cancels it, which ends the run and rebuilds.</summary>
        internal CancellationToken ReloadToken
        {
            // a cancelled token once disposed (the loop has already stopped by then): it never reads
            // a disposed CTS, and its stopping-token check short-circuits the cancelled result
            get { lock (_lock) return _disposed ? new CancellationToken(canceled: true) : _reload.Token; }
        }

        // cancels the current build's reload token; called under _lock, after the effective
        // configuration has already been updated
        private void SignalReload(string reason)
        {
            Logger.Info($"{reason}. Reloading...");

            try
            {
                _reload.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // already shutting down; the loop reads the token's state, not the reason
            }
        }

        /// <summary>Builds the watcher over all resolved conditions of the given blocks (null when
        /// there are none). Its subscriptions do fallible platform work, so it is created as part of
        /// the trial and only committed once it — and everything else — succeeds.</summary>
        private EnvironmentWatcher? CreateConditionWatcher(IReadOnlyList<EnvironmentBlock> blocks, TimeSpan debounce)
        {
            var conditions = blocks.SelectMany(block => block.Conditions).ToList();

            return conditions.Count > 0 ? new EnvironmentWatcher(this, conditions, debounce) : null;
        }

        /// <summary>Starts the debounced configuration-file watcher (auto-reload). Absorbs the old
        /// ConfigFileWatcher: it lives across rebuilds, so the loop no longer arms one per run.</summary>
        private void StartFileWatching()
        {
            if (!_watchFile)
                return;

            var path = _watchPath ?? _configPath;

            var directory = Path.GetDirectoryName(Path.GetFullPath(path));

            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                Logger.Warn($"Auto-reload is enabled, but the watch directory '{directory}' does not exist - not watching.");
                return;
            }

            _fileDebounce = new Timer(OnConfigFileSettled);

            _fileWatcher = new FileSystemWatcher(directory, Path.GetFileName(path))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            };

            _fileWatcher.Changed += OnConfigFileChanged;
            _fileWatcher.Created += OnConfigFileChanged;
            _fileWatcher.Renamed += OnConfigFileChanged;

            _fileWatcher.EnableRaisingEvents = true;
        }

        // a raw watcher event only (re)arms the debounce; the settle callback does the work, so a
        // burst of events for one save collapses to a single reload after the write finishes
        private void OnConfigFileChanged(object sender, FileSystemEventArgs e)
        {
            lock (_lock)
            {
                if (_disposed)
                    return;

                _fileDebounce?.Change(FileDebounce, Timeout.InfiniteTimeSpan);
            }
        }

        private void OnConfigFileSettled(object? state)
        {
            lock (_lock)
            {
                if (_disposed)
                    return;

                // a watcher callback must never throw (it runs on a ThreadPool thread, outside any
                // host try/catch); a bad or half-written edit keeps the current configuration
                try
                {
                    if (_augmenting)
                        ReloadAugmenting();
                    else
                        ReloadPassthrough();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to process the configuration file change - keeping the current configuration.");
                }
            }
        }

        /// <summary>
        /// Re-evaluates the live conditions (called off the condition watcher, debounced). If the
        /// resulting effective configuration differs from the one currently served, updates it and
        /// cancels the reload token so the application loop rebuilds.
        /// </summary>
        internal void Reevaluate()
        {
            lock (_lock)
            {
                if (_disposed || !_augmenting)
                    return;

                var active = ComputeActiveBlocks(_blocks);

                var document = BuildEffectiveDocument(active, _version, _onConflict);

                var effective = document.ToString(SaveOptions.DisableFormatting);

                if (effective == _effectiveConfig)
                    return;

                _effectiveConfig = effective;

                WriteEffectiveConfig(document, active);

                SignalReload($"Environment -> {DescribeActive(active)}");
            }
        }

        /// <summary>
        /// Re-reads the configuration file after an edit (augmenting mode): re-parses the
        /// &lt;Environment&gt; blocks and re-resolves their conditions, then swaps the running
        /// configuration in only if it all succeeds — a bad edit keeps the current environments.
        /// This is the only place the conditions are re-resolved, so an ordinary condition-driven
        /// rebuild never restarts a change source. A change of the configuration ROOT (mode switch)
        /// needs a full process restart and is left to the current configuration. Test seam.
        /// </summary>
        internal void Reload()
        {
            lock (_lock)
            {
                if (!_disposed && _augmenting)
                    ReloadAugmenting();
            }
        }

        // under _lock
        private void ReloadAugmenting()
        {
            if (_parentScope is null)
                return; // never activated (the test-only Apply path)

            EnvironmentParser.Result config;
            string? outputPath;
            string text;
            try
            {
                text = File.ReadAllText(_configPath);

                if (text == _readText)
                    return; // a spurious watcher event on unchanged content — nothing to do

                var document = XDocument.Parse(text);

                if (document.Root?.Name.LocalName.Equals(EnvironmentParser.ROOT_ELEMENT, StringComparison.OrdinalIgnoreCase) != true)
                {
                    Logger.Warn($"The configuration root is no longer <{EnvironmentParser.ROOT_ELEMENT}>; " +
                        $"switching the configuration mode requires a full restart. Keeping the current environments.");
                    return;
                }

                config = EnvironmentParser.Parse(document);
                outputPath = ResolveOutputPath(_configPath, config.OutputEffectiveXML);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to read the environment configuration from '{_configPath}'; keeping the current environments.");
                return;
            }

            // build the ENTIRE new state on a trial — conditions, effective config, AND the
            // replacement change watcher (whose subscriptions do fallible platform work) — without
            // touching the running state, so any fault keeps the current environments intact
            var trial = _parentScope.BeginLifetimeScope();
            EnvironmentWatcher? watcher = null;
            string effective;
            XElement document2;
            IReadOnlyList<EnvironmentBlock> active;
            try
            {
                ResolveConditions(trial, config.Blocks);
                active = ComputeActiveBlocks(config.Blocks);
                document2 = BuildEffectiveDocument(active, config.Version, config.OnConflict);
                effective = document2.ToString(SaveOptions.DisableFormatting);
                watcher = CreateConditionWatcher(config.Blocks, config.Debounce);
            }
            catch (Exception ex)
            {
                watcher?.Dispose();
                trial.Dispose();
                Logger.Warn(ex, $"The edited environment configuration in '{_configPath}' is invalid; keeping the current environments.");
                return;
            }

            // commit: swap the fully-built new state in first, then retire the old — a fault while
            // retiring the old state can no longer break the (already live) new state, and none of
            // the remaining steps throw
            var oldWatcher = _conditionWatcher;
            var oldConditions = _conditions;

            _conditionWatcher = watcher;
            _conditions = trial;
            _readText = text;
            Adopt(config, outputPath);

            var previous = _effectiveConfig;
            RemoveEffectiveConfig();
            _effectiveConfig = effective;
            LogActive(active);
            WriteEffectiveConfig(document2, active);

            // best effort: the new state is already live, so a fault here is logged, not fatal
            try { oldWatcher?.Dispose(); } catch (Exception ex) { Logger.Warn(ex, "Failed to retire the previous environment watcher."); }
            try { oldConditions?.Dispose(); } catch (Exception ex) { Logger.Warn(ex, "Failed to retire the previous environment conditions."); }

            if (effective != previous)
                SignalReload("Configuration file changed");
        }

        // under _lock
        private void ReloadPassthrough()
        {
            string text;
            try
            {
                text = File.ReadAllText(_configPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logger.Warn(ex, $"Failed to read the configuration file '{_configPath}'; keeping the current configuration.");
                return;
            }

            if (text == _readText)
                return; // no material change (a touch, or a spurious watcher event)

            _readText = text;

            SignalReload("Configuration file changed");
        }

        private string? TryReadFile()
        {
            try
            {
                return File.ReadAllText(_configPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FileNotFoundException or DirectoryNotFoundException)
            {
                Logger.Warn(ex, $"Failed to read the configuration file '{_configPath}'.");
                return null;
            }
        }

        /// <summary>Computes the effective configuration from the given blocks and live condition
        /// states (and, when it changed, writes the effective XML file).</summary>
        private string Compute(IReadOnlyList<EnvironmentBlock> blocks, out IReadOnlyList<EnvironmentBlock> active)
        {
            active = ComputeActiveBlocks(blocks);

            LogActive(active);

            var document = BuildEffectiveDocument(active, _version, _onConflict);

            WriteEffectiveConfig(document, active);

            return document.ToString(SaveOptions.DisableFormatting);
        }

        private static void LogActive(IReadOnlyList<EnvironmentBlock> active)
        {
            if (active.Count == 0 || active.All(block => block.IsDefault))
                Logger.Warn($"Environment -> {DescribeActive(active)} - no environment condition matches.");
            else
                Logger.Info($"Environment -> {DescribeActive(active)}");
        }

        /// <summary>
        /// Re-evaluates all conditions; true if the resulting effective configuration
        /// differs from the one the application was built from.
        /// </summary>
        internal bool HasEffectiveConfigChanged(out string reason)
        {
            var active = ComputeActiveBlocks(_blocks);

            if (BuildEffectiveConfig(active, _version, _onConflict) == _effectiveConfig)
            {
                reason = string.Empty;
                return false;
            }

            reason = $"Environment -> {DescribeActive(active)}";
            return true;
        }

        /// <summary>
        /// Evaluates all blocks (each block exactly once), in document order.
        /// Blocks with onlyIf="never" are excluded; default blocks with onlyIf="else"
        /// are included only when no other environment matches. A block referencing
        /// another environment is applied only while that target is applied (onlyIf) or
        /// is not applied (onlyIfNot) - the parser guarantees the combined reference graph
        /// is acyclic, so the memoized recursion below terminates.
        /// </summary>
        private static List<EnvironmentBlock> ComputeActiveBlocks(IReadOnlyList<EnvironmentBlock> blocks)
        {
            Dictionary<EnvironmentBlock, bool> applied = [];

            bool IsApplied(EnvironmentBlock block)
            {
                if (applied.TryGetValue(block, out bool result))
                    return result;

                result = block.MergeMode != EnvironmentMergeMode.Never && block.IsActive;

                if (result && IsSuppressed(block))
                {
                    Logger.Trace($"Environment '{block.DisplayName}' is suppressed by " +
                        $"{EnvironmentParser.ONLY_IF_NOT_ATTRIBUTE} = \"{block.OnlyIfNot}\".");

                    result = false;
                }

                if (result && !IsRequirementMet(block))
                {
                    Logger.Trace($"Environment '{block.DisplayName}' is inactive because its required " +
                        $"{EnvironmentParser.ONLY_IF_ATTRIBUTE} = \"{block.OnlyIf}\" environment is not applied.");

                    result = false;
                }

                return applied[block] = result;
            }

            // suppressed while any environment named by onlyIfNot is applied
            bool IsSuppressed(EnvironmentBlock block)
                => block.OnlyIfNot is string target && IsAnyApplied(target);

            // requirement met unless onlyIf names an environment that is not applied
            bool IsRequirementMet(EnvironmentBlock block)
                => block.OnlyIf is not string target || IsAnyApplied(target);

            bool IsAnyApplied(string name)
                => blocks.Any(other => !other.IsDefault
                    && other.Name?.Equals(name, StringComparison.OrdinalIgnoreCase) == true && IsApplied(other));

            var matching = blocks.Where(block => !block.IsDefault).Where(IsApplied).ToHashSet();

            return blocks.Where(block => block.IsDefault
                ? (block.MergeMode == EnvironmentMergeMode.Always || matching.Count == 0) && IsApplied(block)
                : matching.Contains(block)).ToList();
        }

        private void ResolveConditions(IComponentContext context, IReadOnlyList<EnvironmentBlock> blocks)
        {
            foreach (var block in blocks)
            {
                // disabled blocks behave like commented-out ones - their conditions
                // are neither watched nor required to resolve
                if (block.MergeMode == EnvironmentMergeMode.Never)
                    continue;

                block.Conditions = block.ConditionAttributes
                    .Select(attribute => CreateCondition(context, block, attribute.Name, attribute.Value))
                    .ToList();
            }
        }

        private static IEnvironmentCondition CreateCondition(IComponentContext context, EnvironmentBlock block, string name, string value)
        {
            try
            {
                if (context.ResolveOptionalNamed<IEnvironmentCondition>(name.ToLowerInvariant(), TypedParameter.From(value))
                    is IEnvironmentCondition condition)
                {
                    return condition;
                }
            }
            // a condition constructor rejecting the attribute value surfaces wrapped in the
            // container's resolution exception - report it as the configuration problem it is
            catch (DependencyResolutionException ex) when (FindConfigurationError(ex) is ConfigurationValueException error)
            {
                throw new ConfigurationValueException($"Environment '{block.DisplayName}': " +
                    $"invalid condition {name} = \"{value}\" ({error.Message})", error);
            }

            throw new ConfigurationValueException($"Environment '{block.DisplayName}': " +
                $"no condition is registered for attribute '{name}'.");
        }

        private static ConfigurationValueException? FindConfigurationError(Exception exception)
        {
            for (Exception? inner = exception; inner is not null; inner = inner.InnerException)
                if (inner is ConfigurationValueException error)
                    return error;

            return null;
        }

        private XElement BuildEffectiveDocument(IReadOnlyList<EnvironmentBlock> active, string version, ConflictResolution onConflict)
        {
            var merged = ConfigMerger.Merge(active, _collectionElements, onConflict);

            // regardless of the mode, the version attribute always lives on the configuration root
            merged.SetAttributeValue(EnvironmentParser.VERSION_ATTRIBUTE, version);

            return merged;
        }

        private string BuildEffectiveConfig(IReadOnlyList<EnvironmentBlock> active, string version, ConflictResolution onConflict)
            => BuildEffectiveDocument(active, version, onConflict).ToString(SaveOptions.DisableFormatting);

        /// <summary>
        /// Writes the effective configuration (indented, for legibility) to the configured
        /// output path. The file is removed again when this instance is disposed.
        /// </summary>
        private void WriteEffectiveConfig(XElement effective, IReadOnlyList<EnvironmentBlock> active)
        {
            if (_outputPath is null)
                return;

            try
            {
                var document = new XDocument(new XDeclaration("1.0", "utf-8", null),
                    new XComment($" effective configuration [active environments: {DescribeActive(active)}]" +
                                  " - generated by Desomnia, removed on shutdown "),
                    effective);

                document.Save(_outputPath);

                _writtenPath = _outputPath;

                Logger.Trace($"Effective configuration written to: '{_outputPath}'");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logger.Warn(ex, $"Failed to write the effective configuration to {_outputPath}");
            }
        }

        private void RemoveEffectiveConfig()
        {
            if (_writtenPath is not string path)
                return;

            _writtenPath = null;

            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logger.Warn(ex, $"Failed to remove the effective configuration at {path}");
            }
        }

        private static string DescribeActive(IReadOnlyList<EnvironmentBlock> active)
        {
            var names = active.Where(block => !block.IsDefault).Select(block => "'" + block.DisplayName + "'").ToList();

            var text = names.Count > 0 ? string.Join(", ", names) : "none";

            return active.Any(block => block.IsDefault) ? $"{text} (+ default)" : text;
        }

        public void Dispose()
        {
            // under the lock so it serializes with Reevaluate and the settle callback: an in-flight
            // one either ran to completion before this, or waits on the lock and then sees _disposed
            // and skips — so it can never re-create the effective file after RemoveEffectiveConfig.
            // The watchers' timer/handle stop does not block for a running callback (that would
            // deadlock against this lock), which is why the _disposed guard is what makes it safe.
            lock (_lock)
            {
                if (_disposed)
                    return;

                _disposed = true;

                _fileWatcher?.Dispose();

                _fileDebounce?.Dispose();

                _conditionWatcher?.Dispose();

                _conditions?.Dispose();

                RemoveEffectiveConfig();

                _reload.Dispose();
            }
        }
    }
}
