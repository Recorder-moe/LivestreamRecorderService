using Microsoft.EntityFrameworkCore;

namespace LivestreamRecorderService.DB.Models;

[PrimaryKey(nameof(id))]
public abstract class Entity
{
    /// <summary>
    /// Entity identifier
    /// </summary>
#pragma warning disable IDE1006 // 命名樣式
    public string id { get; set; } = Guid.NewGuid().ToString();
#pragma warning restore IDE1006 // 命名樣式
}
