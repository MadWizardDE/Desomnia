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
   view over the value-type `Order` property) → supply the view ourselves with a custom
   `AOTMetadataViewSource` (see blocker #5). Consumers keep using `Meta<A,B>` unchanged; the source is
   registered only when dynamic code is unavailable (`if (!RuntimeFeature.IsDynamicCodeSupported)` in
   `ApplicationBuilder.ConfigureContainer`). No `#if` and no compile symbol are needed for this — the
   check is a whole-program feature switch ILC evaluates at publish, so it works across the referenced
   projects even though the Service's `PublishAot` (in the `AotTest` pubxml) does not propagate to them.

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

## Blocker list — resolution status

1. **Tmds.DBus `CreateProxy<ILogin1Manager>()`** (`DBusManager.cs`) — `Reflection.Emit`, the one hard
   blocker. **Resolved by exclusion, not rewrite.** The D-Bus/logind path only exists to suspend the
   *local* machine, which an always-on device never does. `PlatformModule.Load` registers `DBusManager`
   only inside `if (Config.UseDBus && HasSystemDBus() && RuntimeFeature.IsDynamicCodeSupported)`, so
   under AOT (where that feature switch folds to `false`) the sysfs `SysPowerManager` fallback is used
   and `CreateProxy` is never *called*. Note: because the daemon assembly is rooted `preserve="all"`,
   `DBusManager`'s body is still compiled by ILC, so its `CreateProxy` call still emits an `IL3050`
   (harmless — unreached at runtime) and pulls some Tmds.DBus proxy code into the binary. A future
   tightening pass (drop the blanket rooting, or migrate to `Tmds.DBus.SourceGenerator`) removes both.
2. **Runtime plugin loading** (`PluginLoadingExtensions.cs`) — **Resolved.** `RegisterPluginModules()`
   is gated out under `#if !DESOMNIA_AOT`; the daemon's modules are compiled in.
3. **Config binding** `ConfigurationBinder.Get<T>(BindNonPublicProperties = true)` — **Resolved by
   rooting**, not by moving to the source-gen binder. `preserve="all"` keeps the (non-public) members
   the reflective binder needs. Worked because bound collections are reference-typed and `= []`
   initializers pre-instantiate the `List<T>`s.
4. **Open-generic logger** (`NetworkContext.RegisterContextAwareLogger`, `Context.Scope`) — **Works**
   (confirmed at runtime): `MakeGenericType` over reference types. No lambda fallback needed.
5. **`Meta<T,TMetadata>` strongly-typed metadata** — **Resolved.** Autofac builds the view via
   `MakeGenericMethod` over each property, which NativeAOT can't JIT for value types (`int Order`). A
   custom `AOTMetadataViewSource` (registered by one line in `ApplicationBuilder`, guarded by
   `if (!RuntimeFeature.IsDynamicCodeSupported)`) supplies `IEnumerable<Meta<A,B>>` itself, building the
   view by plain reflection. Consumers keep using `Meta<A,B>` unchanged — no `#if` at the call sites,
   and no compile symbol: the runtime guard is a whole-program feature switch, so it also swaps out the
   default source inside the referenced projects at ILC time. Verified with an Autofac harness (no
   duplicates, root and child scope) and on the Pi.
6. **CommandLineParser** (daemon `Program.cs`) — **left in, rooted** (`preserve="all"` on the
   `CommandLine` assembly). Untested under AOT (unannotated); if it throws an `IL3050` at runtime on the
   Pi, replace with a hand-rolled parse under `#if DESOMNIA_AOT`.
7. **Own reflection helpers** — `ReflectionExt`, `EventSource`, `FilterContext.RegisterType<F>` — covered
   by rooting so far; annotate with `[DynamicallyAccessedMembers]` if any surface at runtime.
8. **XML config source** EncryptedXml/XSLT `IL3050` — benign (no encrypted XML in use); suppressible.
9. **Service-only** (not daemon-relevant): `Assembly.Location` → `AppContext.BaseDirectory`;
   `Marshal.PtrToStructure(ptr, Type)` → generic overload; WMI/ETW ship extra native interop DLLs.

## Final AOT binary dependencies

Single native ELF/exe linking only standard system libs (`libc`, `libm`, `libgcc_s`, `libstdc++`,
`libz`) plus the app's own P/Invoke target **`libpcap`** (SharpPcap). No .NET runtime install. DBus is
spoken over a socket by pure-managed Tmds.DBus (no native lib). ICU is gone (Invariant).

## The always-on daemon build (the real target)

Because AOT precludes plugin loading, an AOT build is always a *reduced* variant that coexists with
the normal build — it is not a replacement. The AOT daemon targets **always-on ARM64 devices**: it
monitors the network and manages *other* hosts (WoL, sleep proxy) but never suspends itself, so the
D-Bus/logind machinery is simply excluded (see blocker #1). Devices Desomnia *suspends* have plenty of
RAM and keep the normal (non-AOT) build.

## Reproducing the build

**Service (win-x64, local sanity check):**
- `DesomniaService/publish-aot-test.ps1`, the `AotTest` profile, or **VS → Publish → AotTest** →
  `DesomniaService/bin/aot-test/DesomniaService.exe`. `PublishAot` lives in the pubxml, so all three work.
- Prereq: VS "Desktop development with C++"; `vswhere.exe` on PATH or the link step fails `MSB3073`.

**Daemon (linux-arm64, the target — run ON the Pi):**
- `DesomniaDaemon/publish-aot.sh` → `DesomniaDaemon/bin/aot-linux-arm64/desomniad`.
- Cross-compilation from Windows is unsupported; build on the Pi (AOT prereqs already present from a
  prior publish). The script prints `ldd` + how to measure `VmRSS`/`RssAnon`/`RssFile`.
- The script passes `-p:PublishAot=true` on the command line (a global property). The daemon *needs* it
  global — not just for ILC but because it defines `DESOMNIA_AOT` for the daemon project, which the sole
  remaining `#if` (static plugin registration, see blocker #2) reads, and because the conditional
  `FirewallKnockOperator` project reference keys off `PublishAot` too.

### glibc portability (the CI gotcha)

NativeAOT links the binary against the **build machine's** glibc, and glibc symbol versions are
**forward-incompatible**: a binary linked on glibc *X* runs only on glibc *≥ X*. There is no way to
lower the requirement after linking. Symptom on an older target:
`libc.so.6: version 'GLIBC_2.38' not found (required by ./desomniad)`.

Implication for CI: the `ubuntu-24.04-arm` runner ships **glibc 2.39**, too new for Raspberry Pi OS. The
fix is to build on an **older-glibc runner**: `build-linux-aot` runs on **`ubuntu-22.04-arm` (glibc
2.35)**, so the artifact runs on **Debian 12 "Bookworm" (glibc 2.36) and newer**.

Two blind alleys we ruled out getting here:
- **A Debian container.** .NET 10 ships **no Debian image** — `mcr.microsoft.com/dotnet/sdk:10.0` is now
  Ubuntu 24.04 "Noble" (glibc 2.39), and there is no `-bookworm`/`-jammy` tag. Building in it reproduces
  the exact 2.39 problem. (You *could* install the SDK by hand into a `debian:12`/`ubuntu:22.04` base, but
  the 22.04 runner gives the same glibc floor with none of the container faff.)
- **musl static.** A fully-static musl binary can't `dlopen` a glibc-linked `libpcap.so`, which SharpPcap
  loads at runtime — so musl is a dead end for this daemon regardless of its portability appeal.

Floors: Ubuntu 22.04 is the oldest .NET 10-supported apt runner (glibc 2.35), so **anything older than
Bookworm is out of reach with the .NET 10 SDK** — such targets would need an older SDK or an OS upgrade.
Building **locally on the Pi** always matches its own glibc and sidesteps the whole issue.

## Project state (branch `AOT`)

All work lives on branch **`AOT`** (main untouched) and is fully gated, so normal builds are unaffected —
only AOT publishes (`-p:PublishAot=true`) pick up the changes:
- Where the AOT/JIT choice is a *runtime* decision, it is made at runtime via
  `RuntimeFeature.IsDynamicCodeSupported` (a whole-program feature switch) — no compile symbol, so it
  works across referenced projects even when `PublishAot` is set only on the entry assembly (the Service
  pubxml). `#if DESOMNIA_AOT` survives in exactly one place: the daemon's static plugin registration,
  which can't be a runtime check because the plugin project reference itself is `PublishAot`-conditional.
- The `DESOMNIA_AOT` compile symbol **and** the trim/AOT analyzers are enabled only for AOT builds
  (`Directory.Build.props`, keyed off `PublishAot`), so normal builds are warning-free.
- Rooting is split into a shared descriptor plus per-entry descriptors
  (`build/Desomnia.TrimmerRoots{,.Daemon,.Service}.xml`) so neither build references a foreign assembly.
- Daemon runtime tuning (Workstation GC, `InvariantGlobalization`, 24 MiB GC hard limit, size-optimized
  ILC) lives in `DesomniaDaemon.csproj`.

**Result: 130 MB → 48 MB RSS** on the Pi (ARM64) — a self-contained single binary, no installed runtime,
full functionality (verified: starts, captures packets, runs the complex use-case chain).

**Deferred (to a later branch):** tightening the `preserve="all"` rooting for a further `RssFile` cut,
together with the ILC size diagnostics (`IlcGenerateMapFile`/`MstatFile`, removed here for a clean build).
