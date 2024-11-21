FROM mcr.microsoft.com/dotnet/sdk:6.0-cbl-mariner2.0@sha256:912393f5337880c39df2cfd8b1cf38979be046be32e26cf49345327fb5543b7d AS build
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

FROM mcr.microsoft.com/dotnet/runtime-deps:6.0-cbl-mariner2.0@sha256:46b9f8ea7b7744962a968b9fb7f64017a22a1f253545df6e78aa57c53433e608 AS runtime
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
