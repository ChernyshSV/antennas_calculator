// Reusable Fresnel/geo utilities for the demo
using System;

namespace Fresnel.Core;

public record GeoPoint(double LatitudeDeg, double LongitudeDeg);

public interface IFresnelCalculator
{
    double RadiusN(int n, double frequencyHz, double d1Meters, double d2Meters);
    double Radius1(double frequencyHz, double d1Meters, double d2Meters) => RadiusN(1, frequencyHz, d1Meters, d2Meters);
}

public static class GeoUtils
{
    public const double EarthRadiusMeters = 6371008.7714;
    public static double ToRad(double deg) => deg * Math.PI / 180.0;

    public static double HaversineMeters(GeoPoint a, GeoPoint b)
    {
        var dLat = ToRad(b.LatitudeDeg - a.LatitudeDeg);
        var dLon = ToRad(b.LongitudeDeg - a.LongitudeDeg);
        var lat1 = ToRad(a.LatitudeDeg);
        var lat2 = ToRad(b.LatitudeDeg);
        var sinDLat = Math.Sin(dLat / 2);
        var sinDLon = Math.Sin(dLon / 2);
        var h = sinDLat * sinDLat + Math.Cos(lat1) * Math.Cos(lat2) * sinDLon * sinDLon;
        return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    public static GeoPoint Lerp(GeoPoint a, GeoPoint b, double t)
        => new GeoPoint(a.LatitudeDeg + (b.LatitudeDeg - a.LatitudeDeg) * t,
                        a.LongitudeDeg + (b.LongitudeDeg - a.LongitudeDeg) * t);
}

public sealed class FresnelCalculator : IFresnelCalculator
{
    private const double C = 299_792_458.0; // m/s

    public double RadiusN(int n, double frequencyHz, double d1Meters, double d2Meters)
    {
        if (n <= 0) throw new ArgumentOutOfRangeException(nameof(n));
        if (frequencyHz <= 0) throw new ArgumentOutOfRangeException(nameof(frequencyHz));
        if (d1Meters < 0 || d2Meters < 0) throw new ArgumentOutOfRangeException("Distances must be >= 0");

        var lambda = C / frequencyHz;
        return Math.Sqrt(n * lambda * d1Meters * d2Meters / (d1Meters + d2Meters));
    }
}
