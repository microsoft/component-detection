FROM mcr.microsoft.com/dotnet/sdk:10.0-azurelinux3.0@sha256:ffd76bb46785445d2d029809009d35b522619b97df88c0abaa05d549db697b43 AS build
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

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-azurelinux3.0@sha256:d33971c79f36e1d539de998823ae930bf085c4e9812c4cdebd9a1571e300fdb3 AS runtime
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
