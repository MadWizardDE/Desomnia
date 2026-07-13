# Desomnia ships two container flavours from this single file, selected with `docker build --target`:
#
#   * standard  - the framework-dependent daemon on the .NET runtime image. Multi-arch
#                 (amd64/arm64/arm) and supports loadable plugins. This is the default target.
#   * native    - the self-contained NativeAOT daemon (linux-arm64 only). It needs no .NET runtime,
#                 so it swaps that base for the much smaller `runtime-deps` image. Plugins cannot be
#                 loaded at runtime, so none are copied in.
#
# Both stages expect the matching `desomniad` binary in the build context: CI publishes a
# framework-dependent one for `standard` and a NativeAOT one for `native`. `standard` additionally
# expects a `plugins/` directory.

# ---------------------------------------------------------------------------
# standard: framework-dependent, multi-arch, plugin-capable (the default target)
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/runtime:10.0-noble AS standard

RUN apt-get update \
 && apt-get install -y --no-install-recommends \
    libpcap0.8t64 \
    iproute2 \
    ethtool \
 && rm -rf /var/lib/apt/lists/*

COPY plugins/ /usr/lib/desomnia/plugins/

COPY desomniad /usr/sbin/desomniad
RUN chmod +x /usr/sbin/desomniad

USER root

ENTRYPOINT ["desomniad"]

# ---------------------------------------------------------------------------
# native: self-contained NativeAOT daemon, linux-arm64 only, no .NET runtime
# ---------------------------------------------------------------------------
# runtime-deps is Microsoft's base for self-contained / NativeAOT apps: it carries the native C
# libraries such a binary links (glibc, libgcc, libstdc++, zlib) but NOT the .NET runtime, so it is
# much smaller than the `runtime` image above while still running the AOT daemon. Its glibc (2.39,
# from Ubuntu 24.04) comfortably satisfies the binary's >= 2.35 requirement; the container carries its
# own glibc, so the host's version is irrelevant here. Being noble-based, the package names match the
# standard stage. (A `-noble-chiseled` variant is smaller still, but has no apt for the net tools.)
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble AS native

RUN apt-get update \
 && apt-get install -y --no-install-recommends \
    libpcap0.8t64 \
    iproute2 \
    ethtool \
 && rm -rf /var/lib/apt/lists/*

COPY desomniad /usr/sbin/desomniad
RUN chmod +x /usr/sbin/desomniad

USER root

ENTRYPOINT ["desomniad"]
