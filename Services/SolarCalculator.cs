// ============================================================================
// 朝夕·光色 - 日出日落计算服务
// 开发者: JinkaiNiu (niujinkai1997@qq.com)
// 主页: https://kaneniu.com
// 版本: 1.0.3.0
// 说明: 基于 NOAA（美国国家海洋和大气管理局）太阳位置算法，
//       根据经纬度和日期精确计算日出日落时间。
// ============================================================================

using SolarSync.Models;

namespace SolarSync.Services;

/// <summary>
/// 日出日落计算器。使用 NOAA 太阳位置算法，
/// 根据经纬度和日期计算精确的日出日落时间（中国时区 UTC+8）。
/// 纯数学计算，无需网络请求，单次计算耗时 &lt; 1ms。
/// </summary>
public static class SolarCalculator
{
    private const double DegToRad = Math.PI / 180.0;
    private const double RadToDeg = 180.0 / Math.PI;

    /// <summary>
    /// 计算指定日期、经纬度的日出日落时间。
    /// </summary>
    /// <param name="latitude">纬度（WGS84，北正南负）</param>
    /// <param name="longitude">经度（WGS84，东正西负）</param>
    /// <param name="date">计算日期（仅取 Date 部分）</param>
    /// <param name="timeZone">时区偏移（中国为 8）</param>
    /// <returns>包含日出日落时间的 SolarInfo 对象</returns>
    public static SolarInfo Calculate(
        double latitude, double longitude, DateTime date, int timeZone = 8)
    {
        var d = date.Date;

        // ---- 第 1 步：计算儒略日 (Julian Day) ----
        var jd = JulianDay(d.Year, d.Month, d.Day);

        // ---- 第 2 步：计算儒略世纪数 (Julian Century) ----
        var jc = (jd - 2451545.0) / 36525.0;

        // ---- 第 3 步：太阳几何平均经度 ----
        var geomMeanLong = NormalizeAngle(
            280.46646 + jc * (36000.76983 + jc * 0.0003032));

        // ---- 第 4 步：太阳几何平均异常 ----
        var geomMeanAnom = NormalizeAngle(
            357.52911 + jc * (35999.05029 - 0.0001537 * jc));

        // ---- 第 5 步：地球轨道离心率 ----
        var eccent = 0.016708634 - jc * (0.000042037 + 0.0000001267 * jc);

        // ---- 第 6 步：太阳中心方程 ----
        var sunEqOfCtr = Math.Sin(geomMeanAnom * DegToRad)
                * (1.914602 - jc * (0.004817 + 0.000014 * jc))
            + Math.Sin(2 * geomMeanAnom * DegToRad)
                * (0.019993 - 0.000101 * jc)
            + Math.Sin(3 * geomMeanAnom * DegToRad) * 0.000289;

        // ---- 第 7 步：太阳真经度 / 真异常 ----
        var sunTrueLong = geomMeanLong + sunEqOfCtr;
        var sunTrueAnom = geomMeanAnom + sunEqOfCtr;

        // ---- 第 8 步：太阳视经度（修正章动和光行差） ----
        var sunAppLong = sunTrueLong
            - 0.00569
            - 0.00478 * Math.Sin(
                NormalizeAngle(125.04 - 1934.136 * jc) * DegToRad);

        // ---- 第 9 步：黄赤交角（平均 / 修正） ----
        var meanObliqEcliptic = 23.0
            + (26.0 + (21.448 - jc * (46.815 + jc * (0.00059 - jc * 0.001813)))
                / 60.0) / 60.0;
        var obliqCorr = meanObliqEcliptic
            + 0.00256 * Math.Cos(
                NormalizeAngle(125.04 - 1934.136 * jc) * DegToRad);

        // ---- 第 10 步：太阳赤纬 ----
        var sunDeclination = RadToDeg * Math.Asin(
            Math.Sin(obliqCorr * DegToRad)
            * Math.Sin(sunAppLong * DegToRad));

        // ---- 第 11 步：均时差 (Equation of Time) ----
        var varY = Math.Tan((obliqCorr / 2.0) * DegToRad);
        varY *= varY;
        var eqOfTime = 4.0 * RadToDeg * (
            varY * Math.Sin(2.0 * geomMeanLong * DegToRad)
            - 2.0 * eccent * Math.Sin(geomMeanAnom * DegToRad)
            + 4.0 * eccent * varY * Math.Sin(geomMeanAnom * DegToRad)
                * Math.Cos(2.0 * geomMeanLong * DegToRad)
            - 0.5 * varY * varY * Math.Sin(4.0 * geomMeanLong * DegToRad)
            - 1.25 * eccent * eccent * Math.Sin(2.0 * geomMeanAnom * DegToRad)
        );

        // ---- 第 12 步：日出时角 (Hour Angle) ----
        // 90.833° = 90°50' = 官方日出/日落天顶角（含大气折射修正）
        var haSunrise = RadToDeg * Math.Acos(
            (Math.Cos(90.833 * DegToRad)
                - Math.Sin(latitude * DegToRad)
                    * Math.Sin(sunDeclination * DegToRad))
            / (Math.Cos(latitude * DegToRad)
                * Math.Cos(sunDeclination * DegToRad))
        );

        // ---- 第 13 步：计算日出/日落本地时间 ----
        // 太阳中天时刻（含均时差和经度修正）
        var noon = 12.0 - (timeZone * 15.0 - longitude) / 15.0
            - eqOfTime / 60.0;

        var sunriseHour = noon - haSunrise / 15.0;
        var sunsetHour = noon + haSunrise / 15.0;

        var sunrise = d.AddHours(sunriseHour);
        var sunset = d.AddHours(sunsetHour);

        return new SolarInfo
        {
            Sunrise = sunrise,
            Sunset = sunset,
            Date = DateOnly.FromDateTime(d),
            Latitude = latitude,
            Longitude = longitude
        };
    }

    /// <summary>计算儒略日 (Julian Day Number)</summary>
    private static double JulianDay(int year, int month, int day)
    {
        if (month <= 2) { year--; month += 12; }
        var a = year / 100;
        var b = 2 - a + a / 4;
        return Math.Floor(365.25 * (year + 4716.0))
             + Math.Floor(30.6001 * (month + 1.0))
             + day + b - 1524.5;
    }

    /// <summary>将角度归一化到 [0, 360) 范围</summary>
    private static double NormalizeAngle(double angle)
    {
        angle %= 360.0;
        if (angle < 0) angle += 360.0;
        return angle;
    }
}
