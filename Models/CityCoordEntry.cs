using System.Text.Json.Serialization;

namespace SolarSync.Models;

/// <summary>
/// 城市坐标条目，对应 JSON 数据库中的一条记录。
/// </summary>
public sealed class CityCoordEntry
{
    /// <summary>省份 / 直辖市 / 自治区名称</summary>
    [JsonPropertyName("province")]
    public string Province { get; set; } = "";

    /// <summary>城市名称</summary>
    [JsonPropertyName("city")]
    public string City { get; set; } = "";

    /// <summary>纬度（WGS84）</summary>
    [JsonPropertyName("lat")]
    public double Latitude { get; set; }

    /// <summary>经度（WGS84）</summary>
    [JsonPropertyName("lng")]
    public double Longitude { get; set; }
}
