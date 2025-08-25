using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;

namespace AntennasCalculator.WpfApp.Services
{
	/// <summary>
	///     Simple downloader that tries a few common URL patterns for .hgt tiles (zipped) from public mirrors.
	///     Defaults target: Viewfinder Panoramas dem3.
	/// </summary>
	public static class HgtDownloadService
	{
		// You can override (comma-separated) via env var HGT_URL_PATTERNS, e.g. "https://example/{name}.hgt.zip"
		private static readonly string[] DefaultPatterns = new[]
		{
			"https://viewfinderpanoramas.org/dem3/{name}.hgt.zip",
			"http://viewfinderpanoramas.org/dem3/{name}.hgt.zip",
			// Some mirrors might use plain .zip without the inner .hgt extension in name
			"https://viewfinderpanoramas.org/dem3/{name}.zip",
			"http://viewfinderpanoramas.org/dem3/{name}.zip"
		};

		public static IReadOnlyList<string> GetPatterns()
		{
			var env = Environment.GetEnvironmentVariable("HGT_URL_PATTERNS");
			if (!string.IsNullOrWhiteSpace(env))
				return env.Split(new[]
					{
						',', ';', ' '
					}, StringSplitOptions.RemoveEmptyEntries)
					.Select(s => s.Trim())
					.ToArray();
			return DefaultPatterns;
		}

		/// <summary>
		///     Download all missing tiles into <paramref name="demFolder" />. Returns list of successfully saved tile names.
		/// </summary>
		public static async Task<IReadOnlyList<string>> DownloadMissingAsync(IEnumerable<string> missingTileNames, string demFolder,
			IProgress<string>? progress = null, CancellationToken ct = default)
		{
			Directory.CreateDirectory(demFolder);
			var ok = new List<string>();
			using var http = new HttpClient(new HttpClientHandler
			{
				AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
			});
			http.Timeout = TimeSpan.FromSeconds(60);

			foreach (var name in missingTileNames.Distinct(StringComparer.OrdinalIgnoreCase))
			{
				ct.ThrowIfCancellationRequested();
				var destHgt = Path.Combine(demFolder, name + ".hgt");
				if (File.Exists(destHgt))
				{
					ok.Add(name);
					continue;
				}

				var patterns = GetPatterns();
				var saved = false;
				foreach (var pattern in patterns)
				{
					var url = pattern.Replace("{name}", name);
					try
					{
						progress?.Report($"Downloading {name} ...");
						using var resp = await http.GetAsync(url, ct).ConfigureAwait(false);
						if (!resp.IsSuccessStatusCode)
							continue;
						await using var zipStream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
						// Save to temp file first
						var tmpZip = Path.Combine(Path.GetTempPath(), $"{name}-{Guid.NewGuid():N}.zip");
						await using (var fs = File.Create(tmpZip))
						{
							await zipStream.CopyToAsync(fs, ct).ConfigureAwait(false);
						}

						// Try to extract .hgt with matching name; otherwise take first .hgt inside
						using (var za = ZipFile.OpenRead(tmpZip))
						{
							var entry = za.Entries
											.FirstOrDefault(e => e.FullName.EndsWith(".hgt", StringComparison.OrdinalIgnoreCase)
																 && Path.GetFileNameWithoutExtension(e.FullName)
																	 .Equals(name, StringComparison.OrdinalIgnoreCase))
										?? za.Entries.FirstOrDefault(e =>
											e.FullName.EndsWith(".hgt", StringComparison.OrdinalIgnoreCase));
							if (entry is null)
							{
								File.Delete(tmpZip);
								continue;
							}

							await using var es = entry.Open();
							await using var outFs = File.Create(destHgt);
							await es.CopyToAsync(outFs, ct).ConfigureAwait(false);
						}

						try { File.Delete(tmpZip); }
						catch
						{
							/* ignore */
						}

						saved = true;
						ok.Add(name);
						progress?.Report($"Saved {name}.hgt");
						break;
					}
					catch
					{
						// try next pattern
					}
				}

				if (!saved)
					progress?.Report($"Failed to download {name} via known patterns.");
			}

			return ok;
		}

		/// <summary>
		///     Attempt to download required tiles using Viewfinder Panoramas bundle zips like 'L36.zip' which contain multiple
		///     .hgt.
		///     We infer a bundle name from each tile (NxxEyyy etc.), group by bundle, and download once per bundle.
		/// </summary>
		public static async Task<IReadOnlyList<string>> DownloadMissingFromBundlesAsync(IEnumerable<string> missingTileNames,
			string demFolder, IProgress<string>? progress = null, CancellationToken ct = default)
		{
			Directory.CreateDirectory(demFolder);
			var remaining = new HashSet<string>(missingTileNames, StringComparer.OrdinalIgnoreCase);
			var saved = new List<string>();

			// Group missing tiles by inferred bundle code
			var byBundle = remaining
				.Select(name => (name, bundle: InferBundleForTile(name)))
				.Where(x => x.bundle is not null)
				.GroupBy(x => x.bundle!)
				.ToDictionary(g => g.Key, g => g.Select(x => x.name).ToList(), StringComparer.OrdinalIgnoreCase);

			if (byBundle.Count == 0) return Array.Empty<string>();

			using var http = new HttpClient(new HttpClientHandler
			{
				AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
			});
			http.Timeout = TimeSpan.FromSeconds(90);

			foreach (var (bundle, names) in byBundle)
			{
				// Skip if all names already present
				var todo = names.Where(n => !File.Exists(Path.Combine(demFolder, n + ".hgt"))).ToList();
				if (todo.Count == 0) continue;

				// Try HTTPS then HTTP
				var urls = new[]
				{
					$"https://viewfinderpanoramas.org/dem3/{bundle}.zip"
					//$"http://viewfinderpanoramas.org/dem3/{bundle}.zip"
				};

				var okBundle = false;
				foreach (var url in urls)
				{
					ct.ThrowIfCancellationRequested();
					try
					{
						//progress?.Report($"Downloading bundle {bundle} ...");
						//using var resp = await http.GetAsync(url, ct).ConfigureAwait(false);
						//if (!resp.IsSuccessStatusCode) { continue; }
						//var tmpZip = Path.Combine(Path.GetTempPath(), $"{bundle}-{Guid.NewGuid():N}.zip");
						//await using (var fs = File.Create(tmpZip))
						//{
						//	await resp.Content.CopyToAsync(fs, ct).ConfigureAwait(false);
						//}
						//using (var za = ZipFile.OpenRead(tmpZip))
						var localZip = Path.Combine(demFolder, $"{bundle}.zip");
						if (!File.Exists(localZip))
						{
							progress?.Report($"Downloading bundle {bundle} ...");
							using var resp = await http.GetAsync(url, ct).ConfigureAwait(false);
							if (!resp.IsSuccessStatusCode)
								continue;
							await using (var fs = File.Create(localZip))
							{
								await resp.Content.CopyToAsync(fs, ct).ConfigureAwait(false);
							}
						}

						using (var za = ZipFile.OpenRead(localZip))
						{
							foreach (var name in todo.ToList())
							{
								var entry = za.Entries.FirstOrDefault(e =>
									e.FullName.EndsWith(".hgt", StringComparison.OrdinalIgnoreCase) &&
									Path.GetFileNameWithoutExtension(e.FullName)
										.Equals(name, StringComparison.OrdinalIgnoreCase));
								if (entry is null) continue;
								var dest = Path.Combine(demFolder, name + ".hgt");
								await using var es = entry.Open();
								await using var outFs = File.Create(dest);
								await es.CopyToAsync(outFs, ct).ConfigureAwait(false);
								saved.Add(name);
								remaining.Remove(name);
								todo.Remove(name);
								progress?.Report($"Saved {name}.hgt from {bundle}.zip");
							}
						}

						//try { File.Delete(tmpZip); } catch { /* ignore */ }
						okBundle = true;
						break;
					}
					catch
					{
						// try the next URL
					}
				}

				if (!okBundle)
					progress?.Report($"Failed to download bundle {bundle}.zip");
			}

			return saved;
		}

		/// <summary>
		///     Heuristic inference of Viewfinder Panoramas bundle code like 'L36' from an HGT tile name 'N48E031'/'S10W123'.
		///     The scheme groups longitudes by 6° (number = group+6). Latitude is grouped in 4° bands ('A' + floor(lat/4) for N; S
		///     is clamped to 0).
		///     Works well for mid-latitudes in the Northern Hemisphere (e.g., Europe, incl. Ukraine).
		/// </summary>
		public static string? InferBundleForTile(string tileName)
		{
			if (string.IsNullOrWhiteSpace(tileName) || tileName.Length < 7) return null;
			try
			{
				// Parse N/Sxx E/Wxxx
				var ns = tileName[0];
				var lat = int.Parse(tileName.Substring(1, 2));
				var ew = tileName[3];
				var lon = int.Parse(tileName.Substring(4));

				// Longitude group of 6 degrees, bundle number = group + 6
				var lonGroup = (int)Math.Floor(lon / 6.0) * 6;
				var number = lonGroup + 6;

				// Latitude 4-degree bands; for Southern hemisphere clamp to 0 for our simple mapping
				var latAbs = lat;
				var bandIndex = (int)Math.Floor(latAbs / 4.0);
				var letter = (char)('A' + Math.Max(0, bandIndex));
				return $"{letter}{number}";
			}
			catch
			{
				return null;
			}
		}
	}
}