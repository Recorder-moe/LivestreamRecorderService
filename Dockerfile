# syntax=docker/dockerfile:1
ARG UID=1654
ARG VERSION=EDGE
ARG RELEASE=0
ARG BUILD_CONFIGURATION=ApacheCouchDB_Release

########################################
# Debug stage
########################################
FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine AS debug

WORKDIR /app

# RUN mount cache for multi-arch: https://github.com/docker/buildx/issues/549#issuecomment-1788297892
ARG TARGETARCH
ARG TARGETVARIANT

RUN --mount=type=cache,id=apk-$TARGETARCH$TARGETVARIANT,sharing=locked,target=/var/cache/apk \
    --mount=from=ghcr.io/jim60105/static-ffmpeg-upx:7.1.1,source=/ffmpeg,target=/ffmpeg,rw \
    --mount=from=ghcr.io/jim60105/static-ffmpeg-upx:7.1.1,source=/ffprobe,target=/ffprobe,rw \
    apk update && apk add -u \
    # These branches follows the yt-dlp release
    -X "https://dl-cdn.alpinelinux.org/alpine/edge/main" \
    -X "https://dl-cdn.alpinelinux.org/alpine/edge/community" \
    yt-dlp && \
    # Copy the compressed ffmpeg and ffprobe and overwrite the apk installed ones
    cp /ffmpeg /usr/bin/ && \
    cp /ffprobe /usr/bin/

########################################
# Build stage
########################################
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build

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
FROM alpine:3 as final

# RUN mount cache for multi-arch: https://github.com/docker/buildx/issues/549#issuecomment-1788297892
ARG TARGETARCH
ARG TARGETVARIANT

ARG UID
ENV APP_UID=$UID

ENV DOTNET_RUNNING_IN_CONTAINER=true 
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true

RUN --mount=type=cache,id=apk-$TARGETARCH$TARGETVARIANT,sharing=locked,target=/var/cache/apk \
    apk update && apk add -u \
    ca-certificates-bundle libgcc libssl3 libstdc++ zlib 

# Create directories with correct permissions
RUN install -d -m 775 -o $UID -g 0 /app && \
    install -d -m 775 -o $UID -g 0 /licenses

# Copy licenses (OpenShift Policy)
COPY --link --chown=$UID:0 --chmod=775 LICENSE /licenses/LICENSE
ADD --link --chown=$UID:0 --chmod=775 https://raw.githubusercontent.com/yt-dlp/yt-dlp/master/LICENSE /licenses/yt-dlp.LICENSE

RUN --mount=type=cache,id=apk-$TARGETARCH$TARGETVARIANT,sharing=locked,target=/var/cache/apk \
    --mount=from=ghcr.io/jim60105/static-ffmpeg-upx:7.1.1,source=/ffmpeg,target=/ffmpeg,rw \
    --mount=from=ghcr.io/jim60105/static-ffmpeg-upx:7.1.1,source=/ffprobe,target=/ffprobe,rw \
    --mount=from=ghcr.io/jim60105/static-ffmpeg-upx:7.1.1,source=/dumb-init,target=/dumb-init,rw \
    apk update && apk add -u \
    # These branches follows the yt-dlp release
    -X "https://dl-cdn.alpinelinux.org/alpine/edge/main" \
    -X "https://dl-cdn.alpinelinux.org/alpine/edge/community" \
    yt-dlp && \
    # Copy the compressed ffmpeg and ffprobe and overwrite the apk installed ones
    cp /ffmpeg /usr/bin/ && \
    cp /ffprobe /usr/bin/ && \
    cp /dumb-init /usr/bin/

COPY --link --chown=$UID:0 --chmod=775 --from=publish /app /app

ENV PATH="/app:$PATH"

WORKDIR /app

USER $UID

STOPSIGNAL SIGINT

# Use dumb-init as PID 1 to handle signals properly
ENTRYPOINT [ "dumb-init", "--", "/app/LivestreamRecorderService" ]

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
