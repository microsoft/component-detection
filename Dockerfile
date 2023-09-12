FROM mcr.microsoft.com/dotnet/sdk:6.0-cbl-mariner2.0@sha256:0a55184c1bea8da25a6b9ff0333f5e72aca18a4e76c85e8bcec3ebcf789f1bed AS build
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

FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-cbl-mariner2.0@sha256:adfb344b77d80f3cf3a6b024c4d6dffbe2f667fbe07c42cfce1a59ba69a2c2d2 AS runtime
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
