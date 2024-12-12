FROM mcr.microsoft.com/dotnet/sdk:6.0-cbl-mariner2.0@sha256:30469ff2d507d346c46389e961e12dde6c8f61b9dc1ca15ed3ca243aec1f3837 AS build
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

FROM mcr.microsoft.com/dotnet/runtime-deps:6.0-cbl-mariner2.0@sha256:5307890a3beeda0de3fb60e5534d3c43b7f5bfd772ccc70b9cec06f44af753f5 AS runtime
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
