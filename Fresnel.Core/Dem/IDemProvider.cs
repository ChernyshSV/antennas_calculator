namespace Fresnel.Core.Dem;

public interface IDemProvider
{
	// Повертає абсолютну висоту над рівнем моря (м).
	// Реалізацію (SRTM/GDAL) додамо пізніше.
	double GetElevation(double latitudeDeg, double longitudeDeg);
}