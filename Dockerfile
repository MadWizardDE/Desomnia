FROM mcr.microsoft.com/dotnet/runtime:10.0-noble

RUN apt-get update \
 && apt-get install -y --no-install-recommends \
    libpcap0.8 \
    iproute2 \
 && rm -rf /var/lib/apt/lists/*

COPY plugins/ /usr/lib/desomnia/plugins/

COPY desomniad /usr/sbin/desomniad
RUN chmod +x /usr/sbin/desomniad

USER root

ENTRYPOINT ["desomniad"]