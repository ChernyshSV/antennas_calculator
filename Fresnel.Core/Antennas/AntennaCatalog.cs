using System.Text.Json;

namespace Fresnel.Core.Antennas
{
	public record AntennaSpec(
		string Brand,
		string Model,
		string Code,
		string Type,
		string Band,
		double Gain_dBi,
		double BeamwidthDegAz,
		double BeamwidthDegEl,
		string Polarization,
		string Connector,
		string Source
	);

	public static class AntennaCatalog
	{
		public static IReadOnlyList<AntennaSpec> LoadFromJson(string path)
		{
			using var s = File.OpenRead(path);
			var opts = new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			};
			var list = JsonSerializer.Deserialize<List<AntennaSpec>>(s, opts) ?? new List<AntennaSpec>();
			return list;
		}
	}
}