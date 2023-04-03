namespace LivestreamRecorder.DB.Models;

public abstract class Entity
{
    /// <summary>
    /// Entity identifier
    /// </summary>
#pragma warning disable IDE1006 // 命名樣式
    public virtual string id { get; set; } = Guid.NewGuid().ToString();
#pragma warning restore IDE1006 // 命名樣式
}
