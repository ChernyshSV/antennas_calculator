using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AntennasCalculator.WpfApp.Services;

/// <summary>
/// Simple downloader that tries a few common URL patterns for .hgt tiles (zipped) from public mirrors.
/// Defaults target: Viewfinder Panoramas dem3.
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
		{
			return env.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
					  .Select(s => s.Trim())
					  .ToArray();
		}
		return DefaultPatterns;
	}

	/// <summary>
	/// Download all missing tiles into <paramref name="demFolder"/>. Returns list of successfully saved tile names.
	/// </summary>
	public static async Task<IReadOnlyList<string>> DownloadMissingAsync(IEnumerable<string> missingTileNames, string demFolder, IProgress<string>? progress = null, CancellationToken ct = default)
	{
		Directory.CreateDirectory(demFolder);
		var ok = new List<string>();
		using var http = new HttpClient(new HttpClientHandler
		{
			AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
		});
		http.Timeout = TimeSpan.FromSeconds(60);

		foreach (var name in missingTileNames.Distinct(StringComparer.OrdinalIgnoreCase))
		{
			ct.ThrowIfCancellationRequested();
			var destHgt = Path.Combine(demFolder, name + ".hgt");
			if (File.Exists(destHgt)) { ok.Add(name); continue; }

			var patterns = GetPatterns();
			bool saved = false;
			foreach (var pattern in patterns)
			{
				var url = pattern.Replace("{name}", name);
				try
				{
					progress?.Report($"Downloading {name} ...");
					using var resp = await http.GetAsync(url, ct).ConfigureAwait(false);
					if (!resp.IsSuccessStatusCode) { continue; }
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
								 ?? za.Entries.FirstOrDefault(e => e.FullName.EndsWith(".hgt", StringComparison.OrdinalIgnoreCase));
						if (entry is null)
						{
							File.Delete(tmpZip);
							continue;
						}
						await using var es = entry.Open();
						await using var outFs = File.Create(destHgt);
						await es.CopyToAsync(outFs, ct).ConfigureAwait(false);
					}
					try { File.Delete(tmpZip); } catch { /* ignore */ }
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
			{
				progress?.Report($"Failed to download {name} via known patterns.");
			}
		}
		return ok;
	}
}