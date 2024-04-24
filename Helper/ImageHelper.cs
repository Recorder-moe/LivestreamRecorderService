using Serilog;
using Xabe.FFmpeg;

namespace LivestreamRecorderService.Helper;

public static class ImageHelper
{
    public static async Task<string> ConvertToAvifAsync(string path)
    {
        IMediaInfo mediaInfo = await FFmpeg.GetMediaInfo(path);
        string outputPath = Path.ChangeExtension(path, ".avif");

        IConversion conversion = FFmpeg.Conversions.New()
                                       .AddStream(mediaInfo.Streams)
                                       .AddParameter("-c:v libaom-av1 -still-picture 1")
                                       .SetOutput(outputPath)
                                       .SetOverwriteOutput(true);

        conversion.OnProgress += (_, e)
            => Log.Verbose("Progress: {progress}%", e.Percent);

        conversion.OnDataReceived += (_, e)
            => Log.Verbose(e.Data ?? "");

        Log.Debug("FFmpeg arguments: {arguments}", conversion.Build());

        await conversion.Start();

        return outputPath;
    }
}
