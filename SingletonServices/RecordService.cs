using LivestreamRecorder.DB.Enums;
using LivestreamRecorder.DB.Models;
using LivestreamRecorderService.Interfaces.Job;
using LivestreamRecorderService.Interfaces.Job.Downloader;
using LivestreamRecorderService.ScopedServices;
using LivestreamRecorderService.Workers;
using Serilog.Context;

namespace LivestreamRecorderService.SingletonServices;

public class RecordService(
    ILogger<RecordWorker> logger,
    IJobService jobService,
    IYtarchiveService ytarchiveService,
    IYtdlpService ytdlpService,
    ITwitcastingRecorderService twitcastingRecorderService,
    IStreamlinkService streamlinkService,
    IFC2LiveDLService fC2LiveDLService,
    DiscordService discordService)
{
    /// <summary>
    /// Handled failed jobs.
    /// </summary>
    /// <param name="videoService"></param>
    /// <param name="stoppingToken"></param>
    /// <returns></returns>
    public async Task HandledFailedJobsAsync(VideoService videoService, CancellationToken stoppingToken)
    {
        var videos = videoService.GetVideosByStatus(VideoStatus.Recording)
             .Concat(videoService.GetVideosByStatus(VideoStatus.Downloading))
             // Only check videos that started recording/download more than 3 minutes ago
             // to avoid checking videos that are not finished deployment yet.
             .Where(p => null != p.Timestamps.ActualStartTime
                         && DateTime.Now.Subtract(p.Timestamps.ActualStartTime.Value).TotalMinutes >= 3)
             .ToList();

        if (videos.Count > 0)
            logger.LogInformation("Get {count} videos recording/downloading: {videoIds}", videos.Count, string.Join(", ", videos.Select(p => p.id).ToList()));
        else
            logger.LogTrace("No videos recording/downloading");

        foreach (var video in videos)
        {
            if (await jobService.IsJobFailedAsync(video, stoppingToken))
            {
                switch (video.Source)
                {
                    case "Youtube":
                        await videoService.UpdateVideoStatusAsync(video, VideoStatus.Pending);
                        logger.LogWarning("{videoId} is failed. Set status to {status}", video.id, video.Status);
                        break;
                    default:
                        await videoService.UpdateVideoStatusAsync(video, VideoStatus.Error);
                        await videoService.UpdateVideoNoteAsync(video, $"This recording FAILED! Please contact admin if you see this message.");
                        logger.LogWarning("{videoId} is failed.", video.id);
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Create jobs to record videos.
    /// </summary>
    /// <param name="videoService"></param>
    /// <param name="stoppingToken"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public async Task CreateStartRecordJobAsync(VideoService videoService, ChannelService channelService, CancellationToken stoppingToken)
    {
        logger.LogDebug("Getting videos to record");
        var videos = videoService.GetVideosByStatus(VideoStatus.WaitingToRecord);

        // Livestream will start recording immediately when detected goes live.
        // So in fact these cases will only be executed when HandledFailedJobsAsync() occured.
        if (videos.Count > 0)
            logger.LogInformation("Get {count} videos to record: {videoIds}", videos.Count, string.Join(", ", videos.Select(p => p.id).ToList()));
        else
            logger.LogTrace("No videos waiting to record");

        foreach (var video in videos)
        {
            using var _ = LogContext.PushProperty("videoId", video.id);
            var channel = await channelService.GetByChannelIdAndSource(video.ChannelId, video.Source);
            logger.LogInformation("Start to create recording job: {videoId}", video.id);
            try
            {
                switch (video.Source)
                {
                    case "Youtube":
                        await ytarchiveService.InitJobAsync(url: video.id,
                                                             video: video,
                                                             useCookiesFile: channel?.UseCookiesFile == true,
                                                             cancellation: stoppingToken);
                        break;
                    case "Twitcasting":
                        await twitcastingRecorderService.InitJobAsync(url: video.id,
                                                                       video: video,
                                                                       useCookiesFile: false,
                                                                       cancellation: stoppingToken);
                        break;
                    case "Twitch":
                        await streamlinkService.InitJobAsync(url: video.id,
                                                              video: video,
                                                              useCookiesFile: false,
                                                              cancellation: stoppingToken);
                        break;
                    case "FC2":
                        await fC2LiveDLService.InitJobAsync(url: video.id,
                                                             video: video,
                                                             useCookiesFile: channel?.UseCookiesFile == true,
                                                             cancellation: stoppingToken);
                        break;

                    default:
                        logger.LogError("Job deployment FAILED, Source not support: {source}", video.Source);
                        throw new NotSupportedException($"Source {video.Source} not supported");
                }

                var filename = video.Filename;

                await videoService.UpdateVideoFilenameAsync(video, filename);
                await videoService.UpdateVideoStatusAsync(video, VideoStatus.Recording);

                logger.LogInformation("Job deployed: {videoId} ", video.id);
                logger.LogInformation("Start to record {videoId}", video.id);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Job deployment FAILED: {videoId}", video.id);
                await videoService.UpdateVideoStatusAsync(video, VideoStatus.Error);
                await videoService.UpdateVideoNoteAsync(video, "Exception happened when starting recording job. Please contact admin if you see this message");
            }
        }
    }

    /// <summary>
    /// Create jobs to download videos.
    /// </summary>
    /// <param name="videoService"></param>
    /// <param name="stoppingToken"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public async Task CreateStartDownloadJobAsync(VideoService videoService, ChannelService channelService, CancellationToken stoppingToken)
    {
        logger.LogDebug("Getting videos to download");
        var videos = videoService.GetVideosByStatus(VideoStatus.WaitingToDownload);
        if (videos.Count > 0)
            logger.LogInformation("Get {count} videos to download: {videoIds}", videos.Count, string.Join(", ", videos.Select(p => p.id).ToList()));
        else
            logger.LogTrace("No videos waiting to download");

        foreach (var video in videos)
        {
            using var _ = LogContext.PushProperty("videoId", video.id);
            var channel = await channelService.GetByChannelIdAndSource(video.ChannelId, video.Source);
            logger.LogInformation("Start to create downloading job: {videoId}", video.id);
            try
            {
                switch (video.Source)
                {
                    case "Youtube":
                        await ytdlpService.InitJobAsync(
                            url: $"https://youtu.be/{video.id[1..]}",
                            video: video,
                            useCookiesFile: channel?.UseCookiesFile == true,
                            cancellation: stoppingToken);
                        break;
                    case "Twitcasting":
                        await ytdlpService.InitJobAsync(
                            url: $"https://twitcasting.tv/{video.ChannelId}/movie/{video.id}",
                            video: video,
                            useCookiesFile: false,
                            cancellation: stoppingToken);
                        break;
                    case "Twitch":
                        var id = video.id.TrimStart('v');
                        await ytdlpService.InitJobAsync(
                            url: $"https://www.twitch.tv/videos/{id}",
                            video: video,
                            useCookiesFile: false,
                            cancellation: stoppingToken);
                        break;
                    case "FC2":
                        await ytdlpService.InitJobAsync(
                            url: $"https://video.fc2.com/content/{video.id}",
                            video: video,
                            useCookiesFile: channel?.UseCookiesFile == true,
                            cancellation: stoppingToken);
                        break;

                    default:
                        logger.LogError("Job deployment FAILED, Source not support: {source}", video.Source);
                        throw new NotSupportedException($"Source {video.Source} not supported");
                }

                var filename = video.Filename;

                await videoService.UpdateVideoFilenameAsync(video, filename);
                await videoService.UpdateVideoStatusAsync(video, VideoStatus.Downloading);

                logger.LogInformation("Job deployed: {videoId} ", video.id);
                logger.LogInformation("Start to download {videoId}", video.id);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Job deployment FAILED: {videoId}", video.id);
                await videoService.UpdateVideoStatusAsync(video, VideoStatus.Error);
                await videoService.UpdateVideoNoteAsync(video, "Exception happened when starting downloading job. Please contact admin if you see this message");
            }
        }
    }


    /// <summary>
    /// Check recordings and downloading status and return finished videos
    /// </summary>
    /// <param name="videoService"></param>
    /// <param name="cancellation"></param>
    /// <returns>Videos that finish uploading.</returns>
    public async Task<List<Video>> MonitorRecordingDownloadingVideosAsync(VideoService videoService, CancellationToken cancellation = default)
    {
        var result = new List<Video>();
        var videos = videoService.GetVideosByStatus(VideoStatus.Recording)
             .Concat(videoService.GetVideosByStatus(VideoStatus.Downloading));
        foreach (var video in videos)
        {
            using var _ = LogContext.PushProperty("videoId", video.id);

            var successed = await jobService.IsJobSucceededAsync(video, cancellation);
            if (successed)
            {
                logger.LogInformation("Video recording finish {videoId}", video.id);
                result.Add(video);
            }
        }
        return result;
    }

    /// <summary>
    /// Check recordings status and return finished videos
    /// </summary>
    /// <param name="videoService"></param>
    /// <returns>Videos that finish recording.</returns>
    public async Task<List<Video>> MonitorUploadedVideosAsync(VideoService videoService, CancellationToken cancellation = default)
    {
        var result = new List<Video>();
        var videos = videoService.GetVideosByStatus(VideoStatus.Uploading);
        foreach (var video in videos)
        {
            using var _ = LogContext.PushProperty("videoId", video.id);

            var successed = await jobService.IsJobSucceededAsync(video, cancellation);
            if (successed)
            {
                logger.LogInformation("Video uploaded finish {videoId}", video.id);
                result.Add(video);
            }
        }
        return result;
    }

    public async Task PcocessFinishedVideoAsync(VideoService videoService, ChannelService channelService, Video video, CancellationToken stoppingToken = default)
    {
        using var __ = LogContext.PushProperty("videoId", video.id);

        await channelService.UpdateChannelLatestVideoAsync(video);

        await videoService.UpdateVideoArchivedTimeAsync(video);

        // Fire and forget
        _ = videoService.TransferVideoFromSharedVolumeToStorageAsync(video, stoppingToken).ConfigureAwait(false);

        try
        {
            await jobService.RemoveCompletedJobsAsync(video, stoppingToken);
        }
        catch (Exception e)
        {
            await videoService.UpdateVideoStatusAsync(video, VideoStatus.Error);
            await videoService.UpdateVideoNoteAsync(video, $"This recording is FAILED! Please contact admin if you see this message.");
            logger.LogError(e, "Recording FAILED: {videoId}", video.id);
            return;
        }
    }

    public async Task ProcessUploadedVideoAsync(VideoService videoService, ChannelService channelService, Video video, CancellationToken stoppingToken = default)
    {
        using var _ = LogContext.PushProperty("videoId", video.id);

        await discordService.SendArchivedMessage(video, await channelService.GetByChannelIdAndSource(video.ChannelId, video.Source));
        logger.LogInformation("Video {videoId} is successfully uploaded to Storage.", video.id);
        await videoService.UpdateVideoStatusAsync(video, VideoStatus.Archived);

        try
        {
            await jobService.RemoveCompletedJobsAsync(video, stoppingToken);
        }
        catch (Exception e)
        {
            await videoService.UpdateVideoStatusAsync(video, VideoStatus.Error);
            await videoService.UpdateVideoNoteAsync(video, $"This recording is FAILED! Please contact admin if you see this message.");
            logger.LogError(e, "Uploading FAILED: {videoId}", video.id);
            return;
        }
    }

}
