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

FROM mcr.microsoft.com/dotnet/runtime-deps:6.0-cbl-mariner2.0@sha256:fc41a2d7c09a9fe81745efabc5ef0881289ced54fec37a4d81f077d5ba18bdb1 AS runtime
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
