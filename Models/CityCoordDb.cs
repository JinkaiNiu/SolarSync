using System.Text.Json.Serialization;

namespace SolarSync.Models;

/// <summary>
/// 城市坐标数据库容器，对应 JSON 文件的根结构。
/// </summary>
public sealed class CityCoordDb
{
    /// <summary>城市坐标列表，覆盖全国 350+ 地级市及区县</summary>
    [JsonPropertyName("cities")]
    public List<CityCoordEntry> Cities { get; set; } = [];
}
