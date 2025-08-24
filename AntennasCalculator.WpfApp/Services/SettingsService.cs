using System;
using System.IO;
using System.Text.Json;
using AntennasCalculator.WpfApp.Models;

namespace AntennasCalculator.WpfApp.Services;

public static class SettingsService
{
	private static readonly JsonSerializerOptions _opts = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = true
	};

	public static string GetSettingsPath()
	{
		string baseDir;
		try
		{
			baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			if (string.IsNullOrWhiteSpace(baseDir))
				baseDir = AppContext.BaseDirectory;
		}
		catch
		{
			baseDir = AppContext.BaseDirectory;
		}
		var dir = Path.Combine(baseDir, "AntennasCalculator");
		Directory.CreateDirectory(dir);
		return Path.Combine(dir, "settings.json");
	}

	public static AppSettings Load()
	{
		try
		{
			var path = GetSettingsPath();
			if (!File.Exists(path)) return new AppSettings();
			var json = File.ReadAllText(path);
			return JsonSerializer.Deserialize<AppSettings>(json, _opts) ?? new AppSettings();
		}
		catch
		{
			return new AppSettings();
		}
	}

	public static void Save(AppSettings settings)
	{
		try
		{
			var path = GetSettingsPath();
			var json = JsonSerializer.Serialize(settings, _opts);
			File.WriteAllText(path, json);
		}
		catch
		{
			// ignore
		}
	}
}