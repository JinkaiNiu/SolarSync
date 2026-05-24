using System.Text.Json.Serialization;

namespace SolarSync.Models;

/// <summary>
/// IP 地址信息模型，存储从 IP 定位 API 获取的公网 IP、地理位置及经纬度信息。
/// </summary>
public sealed class IpInfo
{
    /// <summary>公网 IP 地址</summary>
    [JsonPropertyName("ip")]
    public string? Ip { get; set; }

    /// <summary>省份 / 直辖市名称</summary>
    [JsonPropertyName("pro")]
    public string? Province { get; set; }

    /// <summary>城市名称</summary>
    [JsonPropertyName("city")]
    public string? City { get; set; }

    /// <summary>完整地址描述</summary>
    [JsonPropertyName("addr")]
    public string? Address { get; set; }

    /// <summary>纬度（从城市坐标库匹配）</summary>
    [JsonIgnore]
    public double Latitude { get; set; }

    /// <summary>经度（从城市坐标库匹配）</summary>
    [JsonIgnore]
    public double Longitude { get; set; }

    /// <summary>是否来自本地缓存（离线模式）</summary>
    [JsonIgnore]
    public bool FromCache { get; set; }

    /// <summary>格式化显示的城市名称</summary>
    [JsonIgnore]
    public string DisplayName =>
        string.IsNullOrEmpty(Province) ? City ?? "未知" :
        string.IsNullOrEmpty(City) ? Province :
        $"{Province} · {City}";
}
