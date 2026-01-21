# LivestreamRecorderService - Agent Instructions

## Project Overview

LivestreamRecorderService is a monitoring worker service for the [Recorder.moe](https://github.com/Recorder-moe) project. It is an advanced live stream recording system that uses containerization technology to achieve horizontal scalability, enabling monitoring and recording of an unlimited number of channels simultaneously across multiple platforms (YouTube, Twitch, Twitcasting, FC2).

## Technology Stack

- **Language**: C# 12 with .NET 8
- **Architecture**: Worker Service (BackgroundService pattern)
- **Database**: Azure CosmosDB or Apache CouchDB (conditional compilation with `#if COSMOSDB` / `#elif COUCHDB`)
- **Storage**: Azure Blob Storage or S3-compatible storage
- **Job Orchestration**: Azure Container Instance (ACI) or Kubernetes
- **Containerization**: Docker with multi-stage builds
- **Logging**: Serilog with structured logging
- **License**: GPLv3

## Project Structure

```
LivestreamRecorderService/
├── Program.cs                          # Application entry point and DI configuration
├── Workers/                            # BackgroundService implementations
│   ├── MonitorWorker.cs               # Main monitoring loop for all platforms
│   ├── RecordWorker.cs                # Handles recording job management
│   ├── UpdateChannelInfoWorker.cs     # Updates channel metadata
│   ├── UpdateVideoStatusWorker.cs     # Updates video statuses
│   └── HeartbeatWorker.cs             # Health check heartbeat
├── ScopedServices/
│   ├── PlatformService/               # Platform-specific implementations
│   │   ├── PlatformService.cs         # Abstract base class
│   │   ├── YoutubeService.cs
│   │   ├── TwitchService.cs
│   │   ├── TwitcastingService.cs
│   │   └── FC2Service.cs
│   ├── VideoService.cs
│   ├── ChannelService.cs
│   └── RssService.cs
├── SingletonServices/                  # Singleton service implementations
│   ├── ACIService.cs                  # Azure Container Instance job service
│   ├── KubernetesService.cs           # Kubernetes job service
│   ├── AbsService.cs                  # Azure Blob Storage
│   ├── S3Service.cs                   # S3 storage service
│   ├── RecordService.cs               # Recording orchestration
│   ├── DiscordService.cs              # Discord notifications
│   └── Downloader/                    # Download tool wrappers
├── DependencyInjection/               # DI extension methods
├── Interfaces/                        # Service interfaces
├── Models/                            # Data models and options
│   └── Options/                       # Configuration option classes
├── Helper/                            # Utility classes
├── Enums/                             # Enumerations
├── Json/                              # JSON serialization context
├── ARMTemplate/                       # Azure ARM templates for ACI
└── LivestreamRecorder.DB/             # Database access subproject (git submodule)
    ├── Models/                        # Entity models (Video, Channel, User)
    ├── Interfaces/                    # Repository interfaces
    ├── CosmosDB/                      # CosmosDB implementation
    └── CouchDB/                       # CouchDB implementation
```

## Build and Run

### Build Commands

```bash
# Restore dependencies
dotnet restore

# Build for CouchDB (default)
dotnet build -c ApacheCouchDB_Release

# Build for CosmosDB
dotnet build -c AzureCosmosDB_Release
```

### Docker Build

```bash
# Build CouchDB version (default)
docker build -t livestreamrecorderservice .

# Build CosmosDB version
docker build --build-arg BUILD_CONFIGURATION=AzureCosmosDB_Release -t livestreamrecorderservice .
```

### Configuration

Configuration is loaded from:
1. `appsettings.json`
2. `appsettings.Development.json` (non-release builds)
3. User secrets (non-release builds)
4. Environment variables

Key configuration sections in `appsettings.json`:
- `Service`: JobService, StorageService, DatabaseService selection
- `Azure`: ACI, Blob Storage, CosmosDB settings
- `S3`: S3-compatible storage settings
- `CouchDB`: CouchDB connection settings
- `Kubernetes`: K8s job settings
- `Discord`: Notification webhook settings
- `Twitch`: Twitch API credentials
- `Heartbeat`: Health check endpoint

## Coding Conventions

### General Style

- Use C# 12 features including primary constructors
- Use file-scoped namespaces
- Use `var` for obvious types, explicit types for clarity
- Use expression-bodied members where appropriate
- Comments and code annotations are written in **English**
- Use structured logging with Serilog (`LogContext.PushProperty`)

### Naming Conventions

- **Classes/Interfaces**: PascalCase (e.g., `YoutubeService`, `IJobService`)
- **Methods**: PascalCase with `Async` suffix for async methods
- **Private fields**: `_camelCase` with underscore prefix
- **Constants**: PascalCase (e.g., `DefaultRegistry`)
- **Configuration sections**: Use `ConfigurationSectionName` constant in option classes

### Dependency Injection

- Use primary constructors for DI injection
- Use `// ReSharper disable once SuggestBaseTypeForParameterInConstructor` when concrete type is intentionally required
- Register services in `DependencyInjection/` extension methods
- Use `IOptions<T>` pattern for configuration options

### Database Access

- Use Repository Pattern with Unit of Work
- Support both CosmosDB and CouchDB via conditional compilation
- Entity `id` property uses lowercase (CouchDB compatibility)
- Always call `_unitOfWork.Commit()` after modifications

### Conditional Compilation

The project uses preprocessor directives for database provider selection:
```csharp
#if COSMOSDB
using LivestreamRecorder.DB.CosmosDB;
#elif COUCHDB
using LivestreamRecorder.DB.CouchDB;
#endif
```

### Error Handling

- Use `ConfigurationErrorsException` for configuration validation failures
- Log errors with `logger.LogError()` or `logger.LogWarning()`
- Use structured logging with property values

### Async/Await

- Always use `CancellationToken` parameter for async methods
- Name parameter `cancellation` consistently
- Prefer `ConfigureAwait(false)` is not required (console app context)

## Key Patterns

### Platform Service Pattern

Each platform (YouTube, Twitch, etc.) implements `IPlatformService`:
```csharp
public interface IPlatformService
{
    string PlatformName { get; }
    int Interval { get; }
    Task UpdateVideosDataAsync(Channel channel, CancellationToken cancellation);
    Task UpdateVideoDataAsync(Video video, CancellationToken cancellation);
    Task UpdateChannelDataAsync(Channel channel, CancellationToken cancellation);
}
```

### Job Service Pattern

Recording jobs are managed through `IJobService`:
```csharp
public interface IJobService
{
    Task<bool> IsJobMissing(Video video, CancellationToken cancellation);
    Task<bool> IsJobFailedAsync(Video video, CancellationToken cancellation);
    Task<bool> IsJobSucceededAsync(Video video, CancellationToken cancellation);
    Task CreateInstanceAsync(...);
}
```

### Video Status Flow

```
Unknown → Scheduled → WaitingToRecord → Recording → WaitingToDownload → Downloading → Archived
                                                                                    ↓
                                                     Skipped/Expired/Missing/Error/Deleted
```

## External Dependencies

- **yt-dlp**: Video information extraction and downloading
- **ffmpeg/ffprobe**: Media processing
- **YoutubeDLSharp**: C# wrapper for yt-dlp
- **bgutil-pot**: YouTube POToken handling
- **Deno**: JavaScript runtime for yt-dlp plugins

## Important Notes

1. **Database Build Variants**: Always ensure you're building with the correct configuration for your database backend.

2. **Container Images**: The project uses container images from `ghcr.io/recorder-moe/` with fallback to `recordermoe/` on Docker Hub.

3. **ID Transformation**: Use `NameHelper.ChangeId` for converting between platform IDs and database IDs to prevent conflicts.

4. **Cookies Handling**: YouTube member-only content requires cookies file, mounted at `/cookies` in K8s or configured via ACI secrets.

5. **GPLv3 License**: The `LivestreamRecorder.DB` subproject uses GPLv3, which propagates to any project using it.
