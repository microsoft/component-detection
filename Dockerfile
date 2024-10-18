FROM mcr.microsoft.com/dotnet/sdk:6.0-cbl-mariner2.0@sha256:6b3587f5043f49c3fee190317d9f5bf4f872aa3312cf24de1b644699e62d961a AS build
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

FROM mcr.microsoft.com/dotnet/runtime-deps:6.0-cbl-mariner2.0@sha256:2afcfa84f9b8118f92f9bef1ee220d8192ab283ad2fe1ca6b5dc835d97c906eb AS runtime
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
