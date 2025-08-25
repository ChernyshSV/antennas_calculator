namespace Fresnel.Core.Dem
{
	public sealed class FlatDemProvider : IDemProvider
	{
		// Заглушка: рівна земля (0 м).
		public double GetElevation(double latitudeDeg, double longitudeDeg)
		{
			return 0.0;
		}
	}
}