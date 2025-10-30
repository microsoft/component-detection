FROM mcr.microsoft.com/dotnet/sdk:8.0-cbl-mariner2.0@sha256:34d121e8963bcefecab6f07ebcef1515d9ad9ffc502c8c11f378d42f70ba9f39 AS build
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

FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-cbl-mariner2.0@sha256:e8f1e989198980266e6c2e17f6bf24c6c35b2f39c24e1bbb083f48a1b1d29c26 AS runtime
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
