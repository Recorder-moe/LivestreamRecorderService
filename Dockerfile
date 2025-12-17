# syntax=docker/dockerfile:1
ARG UID=1654
ARG VERSION=EDGE
ARG RELEASE=0
ARG BUILD_CONFIGURATION=ApacheCouchDB_Release
ARG YTDLP_VERSION=2025.12.08

########################################
# Debug stage
########################################
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS debug

WORKDIR /app

# RUN mount cache for multi-arch: https://github.com/docker/buildx/issues/549#issuecomment-1788297892
ARG TARGETARCH
ARG TARGETVARIANT

ARG UID

# Create directories with correct permissions
RUN install -d -m 775 -o $UID -g 0 /etc/yt-dlp-plugins/bgutil-ytdlp-pot-provider && \
    install -d -m 775 -o $UID -g 0 /deno-dir

# ffmpeg and ffprobe
COPY --link --from=ghcr.io/jim60105/static-ffmpeg-upx:8.0 /ffmpeg /usr/bin/
COPY --link --from=ghcr.io/jim60105/static-ffmpeg-upx:8.0 /ffprobe /usr/bin/

# dumb-init
COPY --link --from=ghcr.io/jim60105/static-ffmpeg-upx:8.0 /dumb-init /usr/bin/

# Copy POToken server (bgutil-pot)
COPY --link --chown=$UID:0 --chmod=775 --from=ghcr.io/jim60105/bgutil-pot:latest /bgutil-pot /usr/bin/

# Copy POToken client plugin
COPY --link --chown=$UID:0 --chmod=775 --from=ghcr.io/jim60105/bgutil-pot:latest /client /etc/yt-dlp-plugins/bgutil-ytdlp-pot-provider

# yt-dlp (using Linux build for Debian with glibc)
ARG YTDLP_VERSION
ADD --link --chown=root:0 --chmod=755 https://github.com/yt-dlp/yt-dlp/releases/download/${YTDLP_VERSION}/yt-dlp_linux /usr/bin/yt-dlp

# Deno JS runtime for yt-dlp
ENV DENO_USE_CGROUPS=1 \
    DENO_DIR=/deno-dir/ \
    DENO_INSTALL_ROOT=/usr/local

COPY --link --chown=$UID:0 --chmod=775 --from=docker.io/denoland/deno:bin /deno /usr/bin/

########################################
# Build stage
########################################
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /source

ARG TARGETARCH
RUN --mount=source=LivestreamRecorderService.csproj,target=LivestreamRecorderService.csproj \
    --mount=source=LivestreamRecorder.DB/LivestreamRecorder.DB.csproj,target=LivestreamRecorder.DB/LivestreamRecorder.DB.csproj \
    dotnet restore -a $TARGETARCH "LivestreamRecorderService.csproj"

########################################
# Publish stage
########################################
FROM build AS publish

ARG BUILD_CONFIGURATION

ARG TARGETARCH
RUN --mount=source=.,target=.,rw \
    dotnet publish "LivestreamRecorderService.csproj" -a $TARGETARCH -c $BUILD_CONFIGURATION -o /app --self-contained true

########################################
# Final stage
########################################
FROM mcr.microsoft.com/dotnet/runtime-deps:8.0 AS final

# RUN mount cache for multi-arch: https://github.com/docker/buildx/issues/549#issuecomment-1788297892
ARG TARGETARCH
ARG TARGETVARIANT

ARG UID
ENV APP_UID=$UID

ENV DOTNET_RUNNING_IN_CONTAINER=true 
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true

# Create directories with correct permissions
RUN install -d -m 775 -o $UID -g 0 /app && \
    install -d -m 775 -o $UID -g 0 /licenses && \
    install -d -m 775 -o $UID -g 0 /.cache && \
    install -d -m 775 -o $UID -g 0 /etc/yt-dlp-plugins/bgutil-ytdlp-pot-provider && \
    install -d -m 775 -o $UID -g 0 /deno-dir

# Copy licenses (OpenShift Policy)
COPY --link --chown=$UID:0 --chmod=775 LICENSE /licenses/LICENSE
ADD --link --chown=$UID:0 --chmod=775 https://raw.githubusercontent.com/yt-dlp/yt-dlp/master/LICENSE /licenses/yt-dlp.LICENSE

# ffmpeg and ffprobe
COPY --link --from=ghcr.io/jim60105/static-ffmpeg-upx:8.0 /ffmpeg /usr/bin/
COPY --link --from=ghcr.io/jim60105/static-ffmpeg-upx:8.0 /ffprobe /usr/bin/

# dumb-init
COPY --link --from=ghcr.io/jim60105/static-ffmpeg-upx:8.0 /dumb-init /usr/bin/

# Copy POToken server (bgutil-pot)
COPY --link --chown=$UID:0 --chmod=775 --from=ghcr.io/jim60105/bgutil-pot:latest /bgutil-pot /usr/bin/

# Copy POToken client plugin
COPY --link --chown=$UID:0 --chmod=775 --from=ghcr.io/jim60105/bgutil-pot:latest /client /etc/yt-dlp-plugins/bgutil-ytdlp-pot-provider

# yt-dlp (using Linux build for Debian with glibc)
ARG YTDLP_VERSION
ADD --link --chown=$UID:0 --chmod=775 https://github.com/yt-dlp/yt-dlp/releases/download/${YTDLP_VERSION}/yt-dlp_linux /usr/bin/yt-dlp

# Deno JS runtime for yt-dlp
ENV DENO_USE_CGROUPS=1 \
    DENO_DIR=/deno-dir/ \
    DENO_INSTALL_ROOT=/usr/local

COPY --link --chown=$UID:0 --chmod=775 --from=docker.io/denoland/deno:bin /deno /usr/bin/

COPY --link --chown=$UID:0 --chmod=775 --from=publish /app /app

ENV PATH="/app:$PATH"

WORKDIR /app

USER $UID

STOPSIGNAL SIGINT

# Use dumb-init as PID 1 to handle signals properly
# Start bgutil-pot server in background and LivestreamRecorderService in foreground
ENTRYPOINT [ "dumb-init", "--", "sh", "-c", "/usr/bin/bgutil-pot & exec /app/LivestreamRecorderService" ]

ARG VERSION
ARG RELEASE
LABEL name="Recorder-moe/LivestreamRecorderService" \
    # Authors for LivestreamRecorderService
    vendor="Recorder-moe" \
    # Maintainer for this docker image
    maintainer="jim60105" \
    # Dockerfile source repository
    url="https://github.com/Recorder-moe/LivestreamRecorderService" \
    version=${VERSION} \
    # This should be a number, incremented with each change
    release=${RELEASE} \
    io.k8s.display-name="LivestreamRecorderService" \
    summary="LivestreamRecorderService: The monitoring worker service for the Recorder.moe project." \
    description="Recorder.moe is an advanced live stream recording system. We utilize containerization technology to achieve horizontal scalability, enabling us to monitor and record an unlimited number of channels simultaneously. For more information about this tool, please visit the following website: https://github.com/Recorder-moe"
