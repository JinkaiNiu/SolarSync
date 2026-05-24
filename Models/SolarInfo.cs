namespace SolarSync.Models;

/// <summary>
/// 日出日落信息模型，存储指定日期和地点的日出、日落时间及辅助判断方法。
/// </summary>
public sealed class SolarInfo
{
    /// <summary>当天日出时间（本地时间）</summary>
    public DateTime Sunrise { get; init; }

    /// <summary>当天日落时间（本地时间）</summary>
    public DateTime Sunset { get; init; }

    /// <summary>计算日期</summary>
    public DateOnly Date { get; init; }

    /// <summary>计算所用的纬度</summary>
    public double Latitude { get; init; }

    /// <summary>计算所用的经度</summary>
    public double Longitude { get; init; }

    /// <summary>判断指定时间是否为白昼（日出 ≤ now < 日落）</summary>
    public bool IsDaytime(DateTime now) => now >= Sunrise && now < Sunset;

    /// <summary>距离下一次日出的时间间隔，若已过日出则返回 TimeSpan.Zero</summary>
    public TimeSpan TimeUntilSunrise(DateTime now) =>
        now < Sunrise ? Sunrise - now : TimeSpan.Zero;

    /// <summary>距离下一次日落的时间间隔，若不在白昼则返回 TimeSpan.Zero</summary>
    public TimeSpan TimeUntilSunset(DateTime now) =>
        now >= Sunrise && now < Sunset ? Sunset - now : TimeSpan.Zero;
}
