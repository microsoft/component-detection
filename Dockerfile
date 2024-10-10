FROM mcr.microsoft.com/dotnet/sdk:6.0-cbl-mariner2.0@sha256:8cf4893e985177f32170b6b97e356e0ccae01f4be56b79f3f9924864c83c612c AS build
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

FROM mcr.microsoft.com/dotnet/runtime-deps:6.0-cbl-mariner2.0@sha256:f20754136dadaad00294114af6df528a417807d2724b83d099f2eb202dc0ec9f AS runtime
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
