using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using Fresnel.Core.Antennas;

namespace AntennasCalculator.WpfApp.ViewModels;

public sealed class LinkViewModel : INotifyPropertyChanged
{
	public ObservableCollection<AntennaSpec> AntennaCatalog { get; } = new();
	private double _freqGHz = 5.5;
	private double _clearancePct = 60.0;
	private string _band = "5 GHz";
	private string _technology = "AirMax";
	private int _channelWidthMHz = 40;
	private int _apGainDbi = 16;
	private int _staGainDbi = 16;
	private AntennaSpec? _selectedAp;
	private AntennaSpec? _selectedSta;


	public double FreqGHz
	{
		get => _freqGHz;
		set { if (_freqGHz != value) { _freqGHz = value; OnPropertyChanged(); } }
	}

	public AntennaSpec? SelectedApAntenna
	{
		get => _selectedAp;
		set
		{
			if (_selectedAp != value)
			{
				_selectedAp = value;
				if (value is not null) ApGainDbi = (int)System.Math.Round(value.Gain_dBi);
				OnPropertyChanged();
			}
		}
	}
	public AntennaSpec? SelectedStaAntenna
	{
		get => _selectedSta;
		set
		{
			if (_selectedSta != value)
			{
				_selectedSta = value;
				if (value is not null) StaGainDbi = (int)System.Math.Round(value.Gain_dBi);
				OnPropertyChanged();
			}
		}
	}

	public double ClearancePct
	{
		get => _clearancePct;
		set { if (_clearancePct != value) { _clearancePct = value; OnPropertyChanged(); } }
	}

	public string Band
	{
		get => _band;
		set { if (_band != value) { _band = value; OnPropertyChanged(); } }
	}

	public string Technology
	{
		get => _technology;
		set { if (_technology != value) { _technology = value; OnPropertyChanged(); } }
	}

	public int ChannelWidthMHz
	{
		get => _channelWidthMHz;
		set { if (_channelWidthMHz != value) { _channelWidthMHz = value; OnPropertyChanged(); } }
	}

	public int ApGainDbi
	{
		get => _apGainDbi;
		set { if (_apGainDbi != value) { _apGainDbi = value; OnPropertyChanged(); } }
	}

	public int StaGainDbi
	{
		get => _staGainDbi;
		set { if (_staGainDbi != value) { _staGainDbi = value; OnPropertyChanged(); } }
	}

	public event PropertyChangedEventHandler? PropertyChanged;
	private void OnPropertyChanged([CallerMemberName] string? name = null)
		=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}