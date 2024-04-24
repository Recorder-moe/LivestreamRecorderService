﻿using System.Text.Json.Serialization;
#nullable disable
#pragma warning disable CS8632 // 可為 Null 的參考型別註釋應只用於 '#nullable' 註釋內容中的程式碼。
#pragma warning disable IDE1006 // 命名樣式

namespace LivestreamRecorderService.Models;

// https://json2csharp.com/

public class AutomaticCaptions
{
}

public class Chapter
{
    [JsonPropertyName("start_time")]
    public double? StartTime { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("end_time")]
    public double? EndTime { get; set; }
}

public class DownloaderOptions
{
    [JsonPropertyName("http_chunk_size")]
    public int? HttpChunkSize { get; set; }
}

public class Format
{
    [JsonPropertyName("format_id")]
    public string FormatId { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("manifest_url")]
    public string ManifestUrl { get; set; }

    [JsonPropertyName("tbr")]
    public double? Tbr { get; set; }

    [JsonPropertyName("ext")]
    public string Ext { get; set; }

    [JsonPropertyName("fps")]
    public double? Fps { get; set; }

    [JsonPropertyName("protocol")]
    public string Protocol { get; set; }

    [JsonPropertyName("quality")]
    public double? Quality { get; set; }

    [JsonPropertyName("width")]
    public int? Width { get; set; }

    [JsonPropertyName("height")]
    public int? Height { get; set; }

    [JsonPropertyName("vcodec")]
    public string Vcodec { get; set; }

    [JsonPropertyName("acodec")]
    public string Acodec { get; set; }

    [JsonPropertyName("dynamic_range")]
    public string DynamicRange { get; set; }

    [JsonPropertyName("video_ext")]
    public string VideoExt { get; set; }

    [JsonPropertyName("audio_ext")]
    public string AudioExt { get; set; }

    [JsonPropertyName("vbr")]
    public double? Vbr { get; set; }

    [JsonPropertyName("abr")]
    public double? Abr { get; set; }

    [JsonPropertyName("format")]
    public string _Format { get; set; }

    [JsonPropertyName("resolution")]
    public string Resolution { get; set; }

    [JsonPropertyName("http_headers")]
    public HttpHeaders HttpHeaders { get; set; }

    [JsonPropertyName("format_note")]
    public string FormatNote { get; set; }

    [JsonPropertyName("rows")]
    public int? Rows { get; set; }

    [JsonPropertyName("columns")]
    public int? Columns { get; set; }

    [JsonPropertyName("fragments")]
    public List<Fragment> Fragments { get; set; }

    [JsonPropertyName("asr")]
    public int? Asr { get; set; }

    [JsonPropertyName("filesize")]
    public long? Filesize { get; set; }

    [JsonPropertyName("source_preference")]
    public int? SourcePreference { get; set; }

    [JsonPropertyName("audio_channels")]
    public int? AudioChannels { get; set; }

    [JsonPropertyName("has_drm")]
    public bool? HasDrm { get; set; }

    [JsonPropertyName("language")]
    public string Language { get; set; }

    [JsonPropertyName("language_preference")]
    public int? LanguagePreference { get; set; }

    [JsonPropertyName("downloader_options")]
    public DownloaderOptions DownloaderOptions { get; set; }

    [JsonPropertyName("container")]
    public string Container { get; set; }

    [JsonPropertyName("preference")]
    public int? Preference { get; set; }

    [JsonPropertyName("filesize_approx")]
    public long? FilesizeApprox { get; set; }
}

public class Fragment
{
    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("duration")]
    public double? Duration { get; set; }
}

public class HttpHeaders
{
    [JsonPropertyName("User-Agent")]
    public string UserAgent { get; set; }

    [JsonPropertyName("Accept")]
    public string Accept { get; set; }

    [JsonPropertyName("Accept-Language")]
    public string AcceptLanguage { get; set; }

    [JsonPropertyName("Sec-Fetch-Mode")]
    public string SecFetchMode { get; set; }
}

public class LiveChat
{
    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("video_id")]
    public string VideoId { get; set; }

    [JsonPropertyName("ext")]
    public string Ext { get; set; }

    [JsonPropertyName("protocol")]
    public string Protocol { get; set; }
}

public class YtdlpVideoData
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("formats")]
    public List<Format> Formats { get; set; }

    [JsonPropertyName("thumbnails")]
    public List<Thumbnail> Thumbnails { get; set; }

    [JsonPropertyName("thumbnail")]
    public string Thumbnail { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("uploader")]
    public string Uploader { get; set; }

    [JsonPropertyName("uploader_id")]
    public string UploaderId { get; set; }

    [JsonPropertyName("uploader_url")]
    public string UploaderUrl { get; set; }

    [JsonPropertyName("channel_id")]
    public string ChannelId { get; set; }

    [JsonPropertyName("channel_url")]
    public string ChannelUrl { get; set; }

    [JsonPropertyName("view_count")]
    public int? ViewCount { get; set; }

    [JsonPropertyName("age_limit")]
    public int? AgeLimit { get; set; }

    [JsonPropertyName("webpage_url")]
    public string WebpageUrl { get; set; }

    [JsonPropertyName("categories")]
    public List<string> Categories { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; }

    [JsonPropertyName("playable_in_embed")]
    public bool? PlayableInEmbed { get; set; }

    [JsonPropertyName("live_status")]
    public string LiveStatus { get; set; }

    [JsonPropertyName("release_timestamp")]
    public long? ReleaseTimestamp { get; set; }

    [JsonPropertyName("automatic_captions")]
    public AutomaticCaptions AutomaticCaptions { get; set; }

    [JsonPropertyName("subtitles")]
    public Subtitles Subtitles { get; set; }

    [JsonPropertyName("like_count")]
    public int? LikeCount { get; set; }

    [JsonPropertyName("concurrent_view_count")]
    public int? ConcurrentViewCount { get; set; }

    [JsonPropertyName("channel")]
    public string Channel { get; set; }

    [JsonPropertyName("channel_follower_count")]
    public int? ChannelFollowerCount { get; set; }

    [JsonPropertyName("upload_date")]
    public string UploadDate { get; set; }

    [JsonPropertyName("availability")]
    public string Availability { get; set; }

    [JsonPropertyName("webpage_url_basename")]
    public string WebpageUrlBasename { get; set; }

    [JsonPropertyName("webpage_url_domain")]
    public string WebpageUrlDomain { get; set; }

    [JsonPropertyName("extractor")]
    public string Extractor { get; set; }

    [JsonPropertyName("extractor_key")]
    public string ExtractorKey { get; set; }

    [JsonPropertyName("display_id")]
    public string DisplayId { get; set; }

    [JsonPropertyName("fulltitle")]
    public string Fulltitle { get; set; }

    [JsonPropertyName("release_date")]
    public string ReleaseDate { get; set; }

    [JsonPropertyName("is_live")]
    public bool? IsLive { get; set; }

    [JsonPropertyName("was_live")]
    public bool? WasLive { get; set; }

    [JsonPropertyName("format_id")]
    public string FormatId { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("manifest_url")]
    public string ManifestUrl { get; set; }

    [JsonPropertyName("tbr")]
    public double? Tbr { get; set; }

    [JsonPropertyName("ext")]
    public string Ext { get; set; }

    [JsonPropertyName("fps")]
    public double? Fps { get; set; }

    [JsonPropertyName("protocol")]
    public string Protocol { get; set; }

    [JsonPropertyName("quality")]
    public double? Quality { get; set; }

    [JsonPropertyName("width")]
    public int? Width { get; set; }

    [JsonPropertyName("height")]
    public int? Height { get; set; }

    [JsonPropertyName("vcodec")]
    public string Vcodec { get; set; }

    [JsonPropertyName("acodec")]
    public string Acodec { get; set; }

    [JsonPropertyName("dynamic_range")]
    public string DynamicRange { get; set; }

    [JsonPropertyName("video_ext")]
    public string VideoExt { get; set; }

    [JsonPropertyName("audio_ext")]
    public string AudioExt { get; set; }

    [JsonPropertyName("vbr")]
    public double? Vbr { get; set; }

    [JsonPropertyName("abr")]
    public double? Abr { get; set; }

    [JsonPropertyName("format")]
    public string Format { get; set; }

    [JsonPropertyName("resolution")]
    public string Resolution { get; set; }

    [JsonPropertyName("http_headers")]
    public HttpHeaders HttpHeaders { get; set; }

    [JsonPropertyName("epoch")]
    public long? Epoch { get; set; }

    [JsonPropertyName("_type")]
    public string Type { get; set; }

    [JsonPropertyName("_version")]
    public Version Version { get; set; }

    [JsonPropertyName("duration")]
    public long? Duration { get; set; }

    [JsonPropertyName("comment_count")]
    public int? CommentCount { get; set; }

    [JsonPropertyName("chapters")]
    public List<Chapter> Chapters { get; set; }

    [JsonPropertyName("duration_string")]
    public string DurationString { get; set; }

    [JsonPropertyName("format_note")]
    public string FormatNote { get; set; }

    [JsonPropertyName("filesize_approx")]
    public long? FilesizeApprox { get; set; }

    [JsonPropertyName("asr")]
    public int? Asr { get; set; }

    [JsonPropertyName("audio_channels")]
    public int? AudioChannels { get; set; }
}

public class Subtitles
{
    [JsonPropertyName("live_chat")]
    public List<LiveChat> LiveChat { get; set; }
}

public class Thumbnail
{
    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("preference")]
    public int? Preference { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("height")]
    public int? Height { get; set; }

    [JsonPropertyName("width")]
    public int? Width { get; set; }

    [JsonPropertyName("resolution")]
    public string Resolution { get; set; }
}

public class Version
{
    [JsonPropertyName("version")]
    public string _Version { get; set; }

    [JsonPropertyName("release_git_head")]
    public string ReleaseGitHead { get; set; }

    [JsonPropertyName("repository")]
    public string Repository { get; set; }
}

