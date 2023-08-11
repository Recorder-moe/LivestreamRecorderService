#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:7.0-alpine AS base
WORKDIR /app
RUN apk add --no-cache --virtual build-deps musl-dev gcc g++ python3-dev &&\
    apk add --no-cache aria2 ffmpeg py3-pip &&\
    pip install --upgrade yt-dlp &&\
    apk del build-deps

FROM mcr.microsoft.com/dotnet/sdk:7.0-alpine AS build
ARG DatabaseService="ApacheCouchDB"
WORKDIR /src
COPY ["LivestreamRecorderService.csproj", "."]
RUN dotnet restore "./LivestreamRecorderService.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "LivestreamRecorderService.csproj" -c ${DatabaseService}_Release -o /app/build

FROM build AS publish
RUN dotnet publish "LivestreamRecorderService.csproj" -c ${DatabaseService}_Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "LivestreamRecorderService.dll"]

RUN addgroup -g 1000 docker && \
    adduser -u 1000 -G docker -h /home/docker -s /bin/sh -D docker \
    && chown -R 1000:1000 .
USER docker
