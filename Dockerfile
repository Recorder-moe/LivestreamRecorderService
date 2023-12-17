### Debug image
### Setup the same as base image but used dotnet/runtime
FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine AS debug

WORKDIR /app
RUN apk add --no-cache python3 && \
    apk add --no-cache --virtual build-deps musl-dev gcc g++ python3-dev py3-pip && \
    python3 -m venv /venv && \
    source /venv/bin/activate && \
    pip install --no-cache-dir yt-dlp && \
    pip uninstall -y setuptools pip && \
    apk del build-deps

ENV PATH="/venv/bin:$PATH"

COPY --link --from=mwader/static-ffmpeg:6.0 /ffmpeg /usr/local/bin/ffmpeg

### Base image for yt-dlp
FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-alpine AS base
WORKDIR /app
RUN apk add --no-cache python3 && \
    apk add --no-cache --virtual build-deps musl-dev gcc g++ python3-dev py3-pip && \
    python3 -m venv /venv && \
    source /venv/bin/activate && \
    pip install --no-cache-dir yt-dlp && \
    pip uninstall -y setuptools pip && \
    apk del build-deps

ENV PATH="/venv/bin:$PATH"

COPY --link --from=mwader/static-ffmpeg:6.0 /ffmpeg /usr/local/bin/ffmpeg

### Build .NET
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
ARG BUILD_CONFIGURATION=ApacheCouchDB_Release
ARG TARGETARCH
WORKDIR /src

COPY ["LivestreamRecorderService.csproj", "."]
COPY ["LivestreamRecorder.DB/LivestreamRecorder.DB.csproj", "LivestreamRecorder.DB/"]
RUN dotnet restore -a $TARGETARCH "LivestreamRecorderService.csproj"

FROM build AS publish
COPY . .
ARG BUILD_CONFIGURATION=ApacheCouchDB_Release
ARG TARGETARCH
RUN dotnet publish "LivestreamRecorderService.csproj" -a $TARGETARCH -c $BUILD_CONFIGURATION -o /app/publish --self-contained true

### Final image
FROM base AS final

ENV PATH="/app:$PATH"

COPY --link --from=publish --chown=$APP_UID:$APP_UID /app/publish /app

USER $APP_UID
ENTRYPOINT ["/app/LivestreamRecorderService"]