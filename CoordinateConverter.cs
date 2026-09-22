namespace OvkmlToKml;

internal readonly record struct GeoPoint(double Lon, double Lat);

/// <summary>
/// GCJ-02 / BD-09 / WGS84 互转。CGCS2000 按 WGS84 处理（差异在厘米级）。
/// </summary>
internal static class CoordinateConverter
{
    private const double Pi = Math.PI;
    private const double A = 6378245.0;
    private const double Ee = 0.00669342162296594323;
    private const double BdPi = Math.PI * 3000.0 / 180.0;

    public static GeoPoint ToWgs84(GeoPoint point, CoordSystem source)
    {
        return source switch
        {
            CoordSystem.Gcj02 => Gcj02ToWgs84(point),
            CoordSystem.Bd09 => Bd09ToWgs84(point),
            _ => point
        };
    }

    public static GeoPoint Gcj02ToWgs84(GeoPoint gcj)
    {
        var magic = Transform(gcj);
        return new GeoPoint(gcj.Lon * 2 - magic.Lon, gcj.Lat * 2 - magic.Lat);
    }

    public static GeoPoint Bd09ToGcj02(GeoPoint bd)
    {
        double x = bd.Lon - 0.0065;
        double y = bd.Lat - 0.006;
        double z = Math.Sqrt(x * x + y * y) - 0.00002 * Math.Sin(y * BdPi);
        double theta = Math.Atan2(y, x) - 0.000003 * Math.Cos(x * BdPi);
        return new GeoPoint(z * Math.Cos(theta), z * Math.Sin(theta));
    }

    public static GeoPoint Bd09ToWgs84(GeoPoint bd) => Gcj02ToWgs84(Bd09ToGcj02(bd));

    public static GeoPoint Wgs84ToGcj02(GeoPoint wgs) => Transform(wgs);

    private static GeoPoint Transform(GeoPoint point)
    {
        if (OutOfChina(point.Lat, point.Lon))
        {
            return point;
        }

        double dLat = TransformLat(point.Lon - 105.0, point.Lat - 35.0);
        double dLon = TransformLon(point.Lon - 105.0, point.Lat - 35.0);
        double radLat = point.Lat / 180.0 * Pi;
        double magic = Math.Sin(radLat);
        magic = 1 - Ee * magic * magic;
        double sqrtMagic = Math.Sqrt(magic);
        dLat = (dLat * 180.0) / ((A * (1 - Ee)) / (magic * sqrtMagic) * Pi);
        dLon = (dLon * 180.0) / (A / sqrtMagic * Math.Cos(radLat) * Pi);
        return new GeoPoint(point.Lon + dLon, point.Lat + dLat);
    }

    private static bool OutOfChina(double lat, double lon)
    {
        return lon < 72.004 || lon > 137.8347 || lat < 0.8293 || lat > 55.8271;
    }

    private static double TransformLat(double x, double y)
    {
        double ret = -100.0 + 2.0 * x + 3.0 * y + 0.2 * y * y + 0.1 * x * y
            + 0.2 * Math.Sqrt(Math.Abs(x));
        ret += (20.0 * Math.Sin(6.0 * x * Pi) + 20.0 * Math.Sin(2.0 * x * Pi)) * 2.0 / 3.0;
        ret += (20.0 * Math.Sin(y * Pi) + 40.0 * Math.Sin(y / 3.0 * Pi)) * 2.0 / 3.0;
        ret += (160.0 * Math.Sin(y / 12.0 * Pi) + 320 * Math.Sin(y * Pi / 30.0)) * 2.0 / 3.0;
        return ret;
    }

    private static double TransformLon(double x, double y)
    {
        double ret = 300.0 + x + 2.0 * y + 0.1 * x * x + 0.1 * x * y
            + 0.1 * Math.Sqrt(Math.Abs(x));
        ret += (20.0 * Math.Sin(6.0 * x * Pi) + 20.0 * Math.Sin(2.0 * x * Pi)) * 2.0 / 3.0;
        ret += (20.0 * Math.Sin(x * Pi) + 40.0 * Math.Sin(x / 3.0 * Pi)) * 2.0 / 3.0;
        ret += (150.0 * Math.Sin(x / 12.0 * Pi) + 300.0 * Math.Sin(x / 30.0 * Pi)) * 2.0 / 3.0;
        return ret;
    }
}
