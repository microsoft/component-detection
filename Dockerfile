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

FROM mcr.microsoft.com/dotnet/runtime-deps:6.0-cbl-mariner2.0@sha256:251e0a62de0f03f602e89f02f1f9b3f620ac234cff3596393fb812c0ba8e5982 AS runtime
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
