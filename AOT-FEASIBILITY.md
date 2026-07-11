# Desomnia NativeAOT Feasibility — Findings

_Status as of 2026-07-11. Working notes from the AOT expedition; the project is intentionally
left "dirty" (diagnostic analyzers enabled across projects) while this is in progress._

## Motivation

`DesomniaDaemon` on a Raspberry Pi (ARM64) sits at ~130 MB RSS baseline, while only ~7 MB is the
managed heap. The rest is CoreCLR hosting cost: the JIT, assembly/metadata loading, ICU, GC
reservations, and thread stacks. The goals of going NativeAOT:

1. **Cut memory** so Desomnia becomes viable on small/embedded ARM64 devices (routers, SBCs).
2. **Drop the runtime dependency** — today's framework-dependent build requires the .NET runtime
   installed on the target; an AOT binary is a single self-contained file (just needs libc +
   `libpcap`). For embedded/OpenWrt-class boxes this deployability win is arguably bigger than the RAM.

Realistic memory expectation: **~20–40 MB RSS (a 3–6x cut)**, not the 2 MB of hello-world — the GC
heap, thread stacks, and libpcap capture buffers set the floor. Managed heap (~7 MB) is unchanged;
what disappears is the JIT + metadata + loader overhead.

Architecture caveat: NativeAOT Linux supports **x64 and arm64** (the Pi is arm64 — ideal). **armhf
is experimental; MIPS is unsupported.** So classic cheap MIPS routers stay out of reach; modern
ARM64 SBCs/routers are the target.

## Cheap wins already applied (no AOT required)

In `DesomniaDaemon.csproj` (runtimeconfig-only, zero code change, low risk):

- `InvariantGlobalization=true` — drops the ICU mapping.
- `ServerGarbageCollection=false` + `ConcurrentGarbageCollection=false` — Workstation GC avoids
  per-core heap reservations (big lever on a multi-core Pi).

Confirmed these land in `desomniad.runtimeconfig.json` (embedded in the single-file bundle — that's
why no loose runtimeconfig is visible on disk). Verified no culture-sensitive parse/format exists in
the daemon path, so Invariant is safe (all numeric parsing is explicit-invariant, integer/hex, or
network-format). **To measure:** compare `RssAnon`/`RssFile` in `/proc/<pid>/status` before/after;
`grep -Ei 'icu|globalization' /proc/<pid>/maps` should be empty with Invariant on.

## The AOT experiment (2026-07-11)

**Vehicle:** `DesomniaService` (win-x64). It statically links the same core (SleepProxy, NetworkMonitor,
etc.), needs no plugins for the test, and — unlike the daemon — can be AOT-built locally on Windows.

**Result: full whole-program compile + native link succeeded.**

- ILC compiled the entire closure — Autofac, all modules, WMI (`Microsoft.Management.Infrastructure`),
  ETW (`TraceEvent`), config binding, NLog — with **no fatal errors** (only warnings).
- Produced a single **18.9 MB** self-contained `DesomniaService.exe`.
- Smoke test: the binary runs, initializes the AOT runtime, executes managed code including the four
  `LogManager…RegisterLayoutRenderer<T>()` calls (**NLog registration works under AOT**), and stops at
  our own elevation guard as expected.

**CONFIRMED (elevated run with real config): the AOT Service reaches steady state and captures
packets with no errors.** Autofac container `Build()` + full module-graph activation, config binding,
NetworkMonitor, SleepProxy and packet capture all work under NativeAOT. The open-generic logger
(`RegisterGeneric` + `MakeGenericType` over reference types) works as predicted — no lambda fallback needed.

### Fixes it took to get there (the resolved chain)

1. **Missing constructors/members** (config binder could not create `SystemMonitorConfig`, etc.) →
   root all Desomnia assemblies with `preserve="all"` (see `build/Desomnia.TrimmerRoots.xml`). Also
   keeps non-public members, which the binder needs (`BindNonPublicProperties = true`).
2. **`Autofac.Core.ImplicitRegistrationSource..cctor` → "Requested type member cannot be found"**
   (Autofac reflecting on its own private helper) → root `Autofac` +
   `Autofac.Extensions.DependencyInjection` in the same descriptor.
3. **`Meta<T, TMetadata>` → `MakeGenericMethod(int)` "missing native code"** (strongly-typed metadata
   view over the value-type `Order` property) → switch the two *consumption* sites to loosely-typed
   `Meta<T>` + metadata-dictionary reads under `#if DESOMNIA_AOT` (`NetworkMonitor.Services`,
   `NetworkContext` plugin filter). Registrations stay strongly-typed (they only read property names
   from the expression — AOT-safe). The symbol is defined via `-p:DesomniaAot=true` (see
   `Directory.Build.props`); the publish script passes it so it propagates to referenced projects.

Note: config binding worked despite its `IL3050` warning because every bound collection element is a
reference type and the `= []` field initializers pre-instantiate the `List<T>`s — so no value-type
generic construction happens at runtime.

**Still unverified: the actual memory target.** The Windows figure isn't comparable; only a
linux-arm64 build on the Pi proves whether we hit ~20–40 MB RSS.

## Autofac verdict (docs + clean ILC compile)

Autofac's core is `IsAotCompatible` on .NET 8+, and `Autofac.dll` compiled clean under ILC. The
features Desomnia relies on are in the **supported, no-warning** set:

- `RegisterType<T>()`, constructor injection, **`PropertiesAutowired` (public properties)**,
  `RegisterModule<T>`, lifetime scopes, reference-type `IEnumerable<T>`/`Lazy<T>`/`Func<T>`/`Owned<T>`.

Warns or fails only for features Desomnia mostly avoids: open generics **over value types** (fail at
runtime; reference types are fine), `RegisterGeneric`/`MakeGenericType` (warn), assembly scanning
(`RegisterAssemblyTypes`), `Meta<T,TMetadata>`, generated factories.
Source: <https://docs.autofac.org/en/latest/advanced/native-aot-trimming.html>

## Blocker list for a real (daemon) AOT build — ranked

1. **Tmds.DBus `CreateProxy<ILogin1Manager>()`** (`DBusManager.cs`) — uses `Reflection.Emit`. **Hard
   blocker**, no rooting can fix it. Must migrate to `Tmds.DBus.Protocol` + `Tmds.DBus.SourceGenerator`.
   Daemon-only, bounded scope (login1/inhibitor path).
2. **Runtime plugin loading** (`PluginLoadingExtensions.cs`, `AssemblyLoadContext.LoadFromAssemblyPath`)
   — architecturally incompatible with a JIT-free binary. Gate out with `#if` for AOT. Plugins are
   not needed for the daemon's built-in modules.
3. **Config binding** `ConfigurationBinder.Get<T>(BindNonPublicProperties = true)`
   (`ConfigurableModule<T>.Build`) — reflective. The AOT-safe source-gen binder does **not** support
   non-public properties, so config models must expose public settable properties. Touches every module.
4. **Open-generic logger** (`NetworkContext.RegisterContextAwareLogger`, `Context.Scope`) —
   `MakeGenericType` over reference types. Probably works (per Autofac docs); if not, swap to a lambda
   registration.
5. **Own reflection helpers** — `ReflectionExt` (`GetProperties/Fields/Methods`), `EventSource`
   (`GetEvents`), `FilterContext.RegisterType<F>` — need `[DynamicallyAccessedMembers]` annotations
   so the required members survive.
6. **XML config source** EncryptedXml/XSLT `IL3050` — benign (no encrypted XML in use); suppressible.
7. **Service-only** (not daemon-relevant): `Assembly.Location` in `Program.cs` → use
   `AppContext.BaseDirectory`; `Marshal.PtrToStructure(ptr, Type)` in `WTS_API.cs` → use the generic
   `PtrToStructure<T>` overload; WMI/ETW ship extra native interop DLLs that Linux won't carry.

## Final AOT binary dependencies

Single native ELF/exe linking only standard system libs (`libc`, `libm`, `libgcc_s`, `libstdc++`,
`libz`) plus the app's own P/Invoke target **`libpcap`** (SharpPcap). No .NET runtime install. DBus is
spoken over a socket by pure-managed Tmds.DBus (no native lib). ICU is gone (Invariant).

## Reproducing the build

- Publish profile: `DesomniaService/Properties/PublishProfiles/AotTest.pubxml`.
- Helper: `DesomniaService/publish-aot-test.ps1` (fixes the vswhere-on-PATH issue).
- Output: `DesomniaService/bin/aot-test/DesomniaService.exe`.
- CLI: `dotnet publish DesomniaService\DesomniaService.csproj -p:PublishProfile=AotTest`
- **Local prerequisite:** VS "Desktop development with C++" workload; `vswhere.exe` must be on PATH
  (VS Installer dir) or the native link step fails with `MSB3073` (link.exe exit 123).

## Project state left behind

- `DesomniaDaemon.csproj`: GC + Invariant settings (**keepers**).
- `<IsAotCompatible>true</IsAotCompatible>` added to 7 projects (daemon + service trees) — **diagnostic
  only**, enables the trim/AOT Roslyn analyzers. Adds IL warnings to normal builds; harmless. Remove or
  gate behind a condition once the expedition ends.
