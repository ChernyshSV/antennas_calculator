namespace AntennasCalculator.WpfApp.Models
{
	public sealed class AppSettings
	{
		public double FreqGHz { get; set; } = 5.5;
		public double ClearancePct { get; set; } = 60.0;
		public string Band { get; set; } = "5 GHz";
		public string Technology { get; set; } = "AirMax";
		public int ChannelWidthMHz { get; set; } = 40;

		public double ApHeightM { get; set; } = 10;
		public double StaHeightM { get; set; } = 10;
		public string? DemFolder { get; set; }

		// Antenna IDs (prefer strong keys if available; fall back to Brand+Model)
		public string? ApAntennaCode { get; set; }
		public string? ApAntennaBrand { get; set; }
		public string? ApAntennaModel { get; set; }

		public string? StaAntennaCode { get; set; }
		public string? StaAntennaBrand { get; set; }
		public string? StaAntennaModel { get; set; }
	}
}