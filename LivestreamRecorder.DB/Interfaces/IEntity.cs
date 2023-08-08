namespace LivestreamRecorder.DB.Interfaces;

public interface IEntity
{
#pragma warning disable IDE1006 // 命名樣式
    string id { get; set; }
#pragma warning restore IDE1006 // 命名樣式

    string Id => id;
}