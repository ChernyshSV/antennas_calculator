using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;

namespace Fresnel.Core.Dem;

/// <summary>
/// Simple SRTM .hgt reader (SRTM1 3601x3601 or SRTM3 1201x1201). Bilinear interpolation. Elevations in meters.
/// Expects files in a given root directory, named like N50E030.hgt, S10W123.hgt, etc.
/// </summary>
public sealed class HgtDemProvider : IDemProvider, IDisposable
{
	private readonly string _root;
	private readonly ConcurrentDictionary<string, Tile> _cache = new();

	public HgtDemProvider(string rootDirectory)
	{
		_root = rootDirectory ?? throw new ArgumentNullException(nameof(rootDirectory));
	}

	public double GetElevation(double latitudeDeg, double longitudeDeg)
	{
		// Normalize lon into [-180, 180), lat into [-90, 90]
		var lat = Math.Max(-90, Math.Min(90, latitudeDeg));
		var lon = longitudeDeg;
		while (lon < -180) lon += 360;
		while (lon >= 180) lon -= 360;

		int latFloor = (int)Math.Floor(lat);
		int lonFloor = (int)Math.Floor(lon);
		string name = TileName(latFloor, lonFloor);
		var tile = LoadTile(name);
		if (tile is null) return 0; // fallback if missing

		double dy = (lat - latFloor);
		double dx = (lon - lonFloor);

		// In .hgt, row 0 is north, col 0 is west. dy increases southward, dx increases eastward.
		// Convert to pixel space:
		double rowFloat = (1 - dy) * (tile.Size - 1);
		double colFloat = dx * (tile.Size - 1);

		int r0 = Clamp((int)Math.Floor(rowFloat), 0, tile.Size - 1);
		int c0 = Clamp((int)Math.Floor(colFloat), 0, tile.Size - 1);
		int r1 = Clamp(r0 + 1, 0, tile.Size - 1);
		int c1 = Clamp(c0 + 1, 0, tile.Size - 1);

		double fr = rowFloat - r0;
		double fc = colFloat - c0;

		var z00 = tile[r0, c0];
		var z10 = tile[r1, c0];
		var z01 = tile[r0, c1];
		var z11 = tile[r1, c1];

		if (z00 == Tile.NoData || z10 == Tile.NoData || z01 == Tile.NoData || z11 == Tile.NoData)
			return 0;

		// Bilinear
		double z0 = z00 * (1 - fc) + z01 * fc;
		double z1 = z10 * (1 - fc) + z11 * fc;
		return z0 * (1 - fr) + z1 * fr;
	}

	private Tile? LoadTile(string name)
	{
		return _cache.GetOrAdd(name, n =>
		{
			string path = Path.Combine(_root, n + ".hgt");
			if (!File.Exists(path)) return Tile.Empty;
			try
			{
				var bytes = File.ReadAllBytes(path);
				int size = bytes.Length switch
				{
					1201 * 1201 * 2 => 1201,
					3601 * 3601 * 2 => 3601,
					_ => 0
				};
				if (size == 0) return Tile.Empty;
				short[] data = new short[size * size];
				// .hgt is big-endian signed 16-bit
				int idx = 0;
				for (int i = 0; i < data.Length; i++)
				{
					data[i] = (short)((bytes[idx] << 8) | bytes[idx + 1]);
					idx += 2;
				}
				return new Tile(size, data);
			}
			catch
			{
				return Tile.Empty;
			}
		});
	}

	private static string TileName(int lat, int lon)
	{
		char ns = lat >= 0 ? 'N' : 'S';
		char ew = lon >= 0 ? 'E' : 'W';
		int alat = Math.Abs(lat);
		int alon = Math.Abs(lon);
		return string.Create(7, (ns, ew, alat, alon), (span, state) =>
		{
			span[0] = state.ns;
			state.alat.ToString("00", CultureInfo.InvariantCulture).AsSpan().CopyTo(span.Slice(1, 2));
			span[3] = state.ew;
			state.alon.ToString("000", CultureInfo.InvariantCulture).AsSpan().CopyTo(span.Slice(4, 3));
		});
	}

	private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);

	public void Dispose()
	{
		_cache.Clear();
	}

	private sealed class Tile
	{
		public const short NoData = short.MinValue;
		public static readonly Tile Empty = new(0, Array.Empty<short>());

		public int Size { get; }
		private readonly short[] _data;
		public Tile(int size, short[] data) { Size = size; _data = data; }
		public short this[int r, int c] => Size == 0 ? NoData : _data[r * Size + c];
	}
}