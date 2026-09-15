FROM mcr.microsoft.com/dotnet/sdk:8.0-cbl-mariner2.0@sha256:a26c5bf4c47186df2fd290b74a2512d9830796d98b644fb12857b57a5244507d AS build
WORKDIR /app
COPY . .
RUN dotnet publish -c Release -o out \
    -r linux-x64 \
    -p:MinVerSkip=true \
    --self-contained true \
    -p:PublishReadyToRun=false \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:PublishSingleFile=true \
    ./src/Microsoft.ComponentDetection

FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-cbl-mariner2.0@sha256:addba165ed9637fc02c63e9f66913deb7125dac90b5ca7d3f89d6d084c23f09b AS runtime
WORKDIR /app
COPY --from=build /app/out ./

RUN tdnf install -y \
    golang \
    moby-engine \
    maven \
    pnpm \
    poetry \
    python

ENTRYPOINT ["/app/Microsoft.ComponentDetection"]
