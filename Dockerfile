FROM mcr.microsoft.com/dotnet/sdk:6.0-cbl-mariner2.0@sha256:071e407b3f629d9cb884d10a6d99ef9012e02149e61dd41746657648ba292da4 AS build
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

FROM mcr.microsoft.com/dotnet/runtime-deps:6.0-cbl-mariner2.0@sha256:a2e008628fbcd24e8c502eb8b4545d0fcbe4475dc76da1978cde8ccf9bde6ecc AS runtime
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
