// ============================================================================
// 朝夕·光色 - IP 定位服务
// 开发者: JinkaiNiu (niujinkai1997@qq.com)
// 主页: https://kaneniu.com
// 版本: 1.0.3.0
// 说明: 通过 myip.ipip.net 获取公网 IP 及地理位置，
//       再匹配内置城市坐标库获取经纬度以计算日出日落。
// ============================================================================

using System.Text.Json;
using SolarSync.Models;

namespace SolarSync.Services;

/// <summary>
/// IP 定位服务。通过 myip.ipip.net 获取公网 IP 及地理位置信息，
/// 匹配内置城市坐标数据库获取经纬度，并支持本地缓存以离线使用。
/// </summary>
public sealed class IpLocationService : IDisposable
{
    /// <summary>IPIP.net 国内 IP 定位 API 地址</summary>
    private const string ApiUrl = "https://myip.ipip.net";

    /// <summary>本地缓存文件路径（%LOCALAPPDATA%/SolarSync/ip_cache.json）</summary>
    private static readonly string CachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SolarSync", "ip_cache.json");

    /// <summary>常见运营商关键词，用于从返回文本中过滤掉 ISP 信息</summary>
    private static readonly HashSet<string> IspKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "电信", "联通", "移动", "铁通", "广电", "长城宽带", "教育网", "科技网", "宽带通"
    };

    private readonly HttpClient _http;
    private readonly Dictionary<string, (double Lat, double Lng)> _cityCoords;
    private IpInfo? _cachedInfo;

    public IpLocationService()
    {
        // 初始化 HTTP 客户端：8 秒超时，设置 User-Agent 绕过防爬
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

        _cityCoords = LoadCityCoords();
        _cachedInfo = LoadCache();
    }

    /// <summary>获取 IP 地理位置信息，优先在线查询，失败后使用缓存</summary>
    public async Task<IpInfo?> GetLocationAsync()
    {
        try
        {
            // 调用 myip.ipip.net 获取原始文本响应
            var text = await _http.GetStringAsync(ApiUrl).ConfigureAwait(false);
            var info = ParseText(text);
            if (info != null)
            {
                TryLookupCoords(info);
                SaveCache(info);
                return info;
            }
        }
        catch
        {
            // 网络异常，继续尝试使用缓存
        }

        // 在线查询失败时返回本地缓存
        if (_cachedInfo != null)
        {
            _cachedInfo.FromCache = true;
            return _cachedInfo;
        }

        return null;
    }

    /// <summary>解析 myip.ipip.net 的文本响应，提取 IP、省份和城市</summary>
    /// <param name="text">响应文本，格式示例："当前 IP：222.90.87.119  来自于：中国 陕西 西安  电信"</param>
    private static IpInfo? ParseText(string text)
    {
        // 提取 IP 地址（"当前 IP" 后的第一个词）
        var ip = ExtractValue(text, "当前 IP");
        if (string.IsNullOrEmpty(ip)) return null;

        // 提取地理位置文本（"来自于" 后的完整内容）
        var location = ExtractAfter(text, "来自于");

        string? province = null;
        string? city = null;

        if (!string.IsNullOrEmpty(location))
        {
            // 按空格分割，过滤掉"中国"和运营商关键词
            var parts = location.Split(new[] { ' ', '\t', '\u3000' },
                StringSplitOptions.RemoveEmptyEntries);

            var chinaIdx = Array.FindIndex(parts, p => p == "中国");
            var startIdx = chinaIdx >= 0 ? chinaIdx + 1 : 0;

            var nonIspParts = parts.Skip(startIdx)
                .Where(p => !IspKeywords.Contains(p)).ToList();

            // 普通城市：province city；直辖市：仅 province
            if (nonIspParts.Count >= 2)
            {
                province = nonIspParts[0];
                city = nonIspParts[1];
            }
            else if (nonIspParts.Count == 1)
            {
                province = nonIspParts[0];
            }
        }

        province = province?.Replace("省", "").Replace("市", "").Trim();
        city = city?.Replace("市", "").Trim();

        return new IpInfo
        {
            Ip = ip,
            Province = province,
            City = city,
            Address = $"{province ?? ""} {city ?? ""}".Trim()
        };
    }

    /// <summary>从文本中提取指定前缀后的第一个单词（用于提取 IP）</summary>
    private static string? ExtractValue(string text, string prefix)
    {
        var idx = text.IndexOf(prefix, StringComparison.Ordinal);
        if (idx < 0) return null;
        var start = idx + prefix.Length;
        // 跳过分隔符（中文冒号、英文冒号、空格）
        while (start < text.Length && (text[start] == '：' || text[start] == ':'
            || text[start] == ' ' || text[start] == '\t'))
            start++;
        var end = start;
        while (end < text.Length && text[end] != ' ' && text[end] != '\t'
            && text[end] != '\n' && text[end] != '\r')
            end++;
        return start < end ? text[start..end].Trim() : null;
    }

    /// <summary>从文本中提取指定前缀后的完整内容（用于提取地理位置）</summary>
    private static string? ExtractAfter(string text, string prefix)
    {
        var idx = text.IndexOf(prefix, StringComparison.Ordinal);
        if (idx < 0) return null;
        var start = idx + prefix.Length;
        while (start < text.Length && (text[start] == '：' || text[start] == ':'
            || text[start] == ' ' || text[start] == '\t'))
            start++;
        var end = text.Length;
        while (end > start && (text[end - 1] == ' ' || text[end - 1] == '\t'
            || text[end - 1] == '\n' || text[end - 1] == '\r'))
            end--;
        return start < end ? text[start..end].Trim() : null;
    }

    /// <summary>根据省份和城市名称从内置坐标库中匹配经纬度</summary>
    private void TryLookupCoords(IpInfo info)
    {
        var key = BuildKey(info.Province, info.City);
        if (key != null && _cityCoords.TryGetValue(key, out var coord))
        {
            info.Latitude = coord.Lat;
            info.Longitude = coord.Lng;
            return;
        }

        // 精确匹配失败时尝试用省份兜底（使用省会坐标）
        if (!string.IsNullOrEmpty(info.Province))
        {
            var fallbackKey = BuildKey(info.Province, null);
            if (fallbackKey != null && _cityCoords.TryGetValue(fallbackKey,
                out var fallback))
            {
                info.Latitude = fallback.Lat;
                info.Longitude = fallback.Lng;
            }
        }
    }

    /// <summary>构造城市坐标查询键："省份|城市" 或纯表示省份</summary>
    private static string? BuildKey(string? province, string? city)
    {
        var p = province?.Replace("省", "").Replace("市", "")
            .Replace("自治区", "").Replace("特别行政区", "").Trim();
        var c = city?.Replace("市", "").Trim();
        if (string.IsNullOrEmpty(p)) return null;
        return string.IsNullOrEmpty(c) ? p : $"{p}|{c}";
    }

    /// <summary>将在线查询结果缓存到本地文件</summary>
    private void SaveCache(IpInfo info)
    {
        try
        {
            _cachedInfo = info;
            var dir = Path.GetDirectoryName(CachePath);
            if (dir != null) Directory.CreateDirectory(dir);
            var data = new
            {
                info.Ip, info.Province, info.City,
                info.Address, info.Latitude, info.Longitude
            };
            File.WriteAllText(CachePath, JsonSerializer.Serialize(data));
        }
        catch { }
    }

    /// <summary>从本地加载上次缓存的 IP 信息</summary>
    private static IpInfo? LoadCache()
    {
        try
        {
            if (!File.Exists(CachePath)) return null;
            var json = File.ReadAllText(CachePath);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return new IpInfo
            {
                Ip = TryGetString(root, "Ip"),
                Province = TryGetString(root, "Province"),
                City = TryGetString(root, "City"),
                Address = TryGetString(root, "Address"),
                Latitude = TryGetDouble(root, "Latitude"),
                Longitude = TryGetDouble(root, "Longitude"),
                FromCache = true
            };
        }
        catch { return null; }
    }

    private static string? TryGetString(JsonElement root, string name)
    {
        if (root.TryGetProperty(name, out var prop)) return prop.GetString();
        return null;
    }

    private static double TryGetDouble(JsonElement root, string name)
    {
        if (root.TryGetProperty(name, out var prop)) return prop.GetDouble();
        return 0;
    }

    /// <summary>从嵌入式资源加载城市坐标 JSON 数据库</summary>
    private static Dictionary<string, (double Lat, double Lng)> LoadCityCoords()
    {
        var asm = typeof(IpLocationService).Assembly;
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.Contains("city_coords"));
        if (name == null) return [];

        using var stream = asm.GetManifestResourceStream(name);
        if (stream == null) return [];

        var db = JsonSerializer.Deserialize<CityCoordDb>(stream);
        if (db?.Cities == null) return [];

        var dict = new Dictionary<string, (double, double)>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var entry in db.Cities)
        {
            var key = BuildKey(entry.Province, entry.City);
            if (key != null && !dict.ContainsKey(key))
                dict[key] = (entry.Latitude, entry.Longitude);
        }
        return dict;
    }

    public void Dispose() => _http.Dispose();
}
