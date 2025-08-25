namespace AntennasCalculator.WpfApp.ViewModels
{
	public sealed record DemTileStatus(
		string Name, // e.g., N50E030
		bool Exists,
		string? FullPath // null if missing
	);
}