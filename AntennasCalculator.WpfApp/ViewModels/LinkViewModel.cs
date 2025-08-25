using Fresnel.Core.Antennas;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AntennasCalculator.WpfApp.ViewModels
{
	public sealed class LinkViewModel : INotifyPropertyChanged
	{
		private int _apGainDbi = 16;
		private double _apHeightM = 10;
		private string _band = "5 GHz";
		private int _channelWidthMHz = 40;
		private double _clearancePct = 60.0;
		private string _demFolder = string.Empty;
		private int _demScanMargin; // tiles padding around bbox
		private string _demScanSummary = string.Empty;
		private bool _forceDatelineWrap;

		// Radio physics
		private double _kFactor = 1.333; // 4/3 Earth by default
		private double _knifeEdgeLossDb = 0.0;

		// Link budget
		private double _txPowerApDbm = 20.0;
		private double _txPowerStaDbm = 20.0;
		private double _feedLossApDb = 1.0;
		private double _feedLossStaDb = 1.0;
		private double _otherLossesDb = 0.0;
		private double _prxDbm = 0.0;

		private double _freqGHz = 5.5;
		private double _minClearancePctActual;
		private string _profileMode = "Precise"; // "Precise" or "Coarse"
		private AntennaSpec? _selectedAp;
		private AntennaSpec? _selectedSta;
		private int _staGainDbi = 16;
		private double _staHeightM = 10;
		private string _technology = "AirMax";
		public ObservableCollection<AntennaSpec> AntennaCatalog { get; } = new();
		public ObservableCollection<DemTileStatus> DemScan { get; } = new();

		public double FreqGHz
		{
			get => _freqGHz;
			set
			{
				if (_freqGHz != value)
				{
					_freqGHz = value;
					OnPropertyChanged();
				}
			}
		}

		public double KFactor
		{
			get => _kFactor;
			set { if (Math.Abs(_kFactor - value) > 1e-9) { _kFactor = value; OnPropertyChanged(); } }
		}

		public double KnifeEdgeLossDb
		{
			get => _knifeEdgeLossDb;
			set { if (Math.Abs(_knifeEdgeLossDb - value) > 1e-9) { _knifeEdgeLossDb = value; OnPropertyChanged(); } }
		}

		public double TxPowerApDbm
		{
			get => _txPowerApDbm;
			set { if (Math.Abs(_txPowerApDbm - value) > 1e-9) { _txPowerApDbm = value; OnPropertyChanged(); } }
		}

		public double TxPowerStaDbm
		{
			get => _txPowerStaDbm;
			set { if (Math.Abs(_txPowerStaDbm - value) > 1e-9) { _txPowerStaDbm = value; OnPropertyChanged(); } }
		}

		public double FeedLossApDb
		{
			get => _feedLossApDb;
			set { if (Math.Abs(_feedLossApDb - value) > 1e-9) { _feedLossApDb = value; OnPropertyChanged(); } }
		}

		public double FeedLossStaDb
		{
			get => _feedLossStaDb;
			set { if (Math.Abs(_feedLossStaDb - value) > 1e-9) { _feedLossStaDb = value; OnPropertyChanged(); } }
		}

		public double OtherLossesDb
		{
			get => _otherLossesDb;
			set { if (Math.Abs(_otherLossesDb - value) > 1e-9) { _otherLossesDb = value; OnPropertyChanged(); } }
		}

		public double PrxDbm
		{
			get => _prxDbm;
			set { if (Math.Abs(_prxDbm - value) > 1e-9) { _prxDbm = value; OnPropertyChanged(); } }
		}


		public int DemScanMargin
		{
			get => _demScanMargin;
			set
			{
				if (_demScanMargin != value)
				{
					_demScanMargin = Math.Max(0, Math.Min(3, value));
					OnPropertyChanged();
				}
			}
		}

		/// <summary>
		///     When true, treat longitudes as wrapping across ±180 explicitly (useful near the dateline).
		///     When false, we auto-detect wrap if bbox width > 180°.
		/// </summary>
		public bool ForceDatelineWrap
		{
			get => _forceDatelineWrap;
			set
			{
				if (_forceDatelineWrap != value)
				{
					_forceDatelineWrap = value;
					OnPropertyChanged();
				}
			}
		}

		public string DemScanSummary
		{
			get => _demScanSummary;
			set
			{
				if (_demScanSummary != value)
				{
					_demScanSummary = value;
					OnPropertyChanged();
				}
			}
		}

		public string ProfileMode
		{
			get => _profileMode;
			set
			{
				if (_profileMode != value)
				{
					_profileMode = value;
					OnPropertyChanged();
				}
			}
		}

		public double ApHeightM
		{
			get => _apHeightM;
			set
			{
				if (_apHeightM != value)
				{
					_apHeightM = value;
					OnPropertyChanged();
				}
			}
		}

		public double StaHeightM
		{
			get => _staHeightM;
			set
			{
				if (_staHeightM != value)
				{
					_staHeightM = value;
					OnPropertyChanged();
				}
			}
		}

		public string DemFolder
		{
			get => _demFolder;
			set
			{
				if (_demFolder != value)
				{
					_demFolder = value;
					OnPropertyChanged();
				}
			}
		}

		/// <summary>Computed min clearance along path (percent of R1). Readonly for UI.</summary>
		public double MinClearancePctActual
		{
			get => _minClearancePctActual;
			set
			{
				if (Math.Abs(_minClearancePctActual - value) > 1e-6)
				{
					_minClearancePctActual = value;
					OnPropertyChanged();
				}
			}
		}


		public AntennaSpec? SelectedApAntenna
		{
			get => _selectedAp;
			set
			{
				if (_selectedAp != value)
				{
					_selectedAp = value;
					if (value is not null) ApGainDbi = (int)Math.Round(value.Gain_dBi);
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
					if (value is not null) StaGainDbi = (int)Math.Round(value.Gain_dBi);
					OnPropertyChanged();
				}
			}
		}

		public double ClearancePct
		{
			get => _clearancePct;
			set
			{
				if (_clearancePct != value)
				{
					_clearancePct = value;
					OnPropertyChanged();
				}
			}
		}

		public string Band
		{
			get => _band;
			set
			{
				if (_band != value)
				{
					_band = value;
					OnPropertyChanged();
				}
			}
		}

		public string Technology
		{
			get => _technology;
			set
			{
				if (_technology != value)
				{
					_technology = value;
					OnPropertyChanged();
				}
			}
		}

		public int ChannelWidthMHz
		{
			get => _channelWidthMHz;
			set
			{
				if (_channelWidthMHz != value)
				{
					_channelWidthMHz = value;
					OnPropertyChanged();
				}
			}
		}

		public int ApGainDbi
		{
			get => _apGainDbi;
			set
			{
				if (_apGainDbi != value)
				{
					_apGainDbi = value;
					OnPropertyChanged();
				}
			}
		}

		public int StaGainDbi
		{
			get => _staGainDbi;
			set
			{
				if (_staGainDbi != value)
				{
					_staGainDbi = value;
					OnPropertyChanged();
				}
			}
		}

		public event PropertyChangedEventHandler? PropertyChanged;

		public void UpdateDemScanSummary()
		{
			if (DemScan.Count == 0)
			{
				DemScanSummary = "";
				return;
			}

			var ok = DemScan.Count(t => t.Exists);
			DemScanSummary = $"Tiles: {DemScan.Count}, present: {ok}, missing: {DemScan.Count - ok}";
		}

		private void OnPropertyChanged([CallerMemberName] string? name = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}
	}
}