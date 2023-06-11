using LivestreamRecorderService.Interfaces;
using System.Net;
using FileInfo = LivestreamRecorderService.Models.FileInfo;
using NFS.Client;

namespace LivestreamRecorderService.SingletonServices;

public class NFSService : ISharedVolumeService
{
    public Task<FileInfo?> GetVideoFileInfoByPrefixAsync(string prefix, TimeSpan delay, CancellationToken cancellation = default)
    {
        // 設定 NFS 位置和驗證資訊
        string nfsPath = @"\\nfs-server\path\to\file.txt";
        NetworkCredential credentials = new NetworkCredential("username", "password");

        // 建立文件系統
        IFileSystem fileSystem = new FileSystem(credentials);

        // 讀取檔案內容
        string fileContent = fileSystem.File.ReadAllText(nfsPath);
        Console.WriteLine(fileContent);
    }
}
