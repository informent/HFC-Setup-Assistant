namespace HfcSetupAssistant.Core;

public sealed class WorkProfile
{
    public string Name { get; set; } = "My field targets";
    public decimal LowTargetDbmv { get; set; } = 28m;
    public decimal HighTargetDbmv { get; set; } = 38m;
    public decimal LowMinimumDbmv { get; set; } = 27m;
    public decimal LowMaximumDbmv { get; set; } = 29m;
    public decimal HighMinimumDbmv { get; set; } = 37m;
    public decimal HighMaximumDbmv { get; set; } = 39m;
    public decimal TiltMinimumDb { get; set; } = 9m;
    public decimal TiltMaximumDb { get; set; } = 11m;
    public string LowFrequencyMhz { get; set; } = "";
    public string HighFrequencyMhz { get; set; } = "";
    public string MeasurementPoint { get; set; } = "";
}

public sealed class PilotColumn : System.ComponentModel.INotifyPropertyChanged
{
    private string _label = "", _frequency = "", _type = "U", _stepDown = "0", _measured = "", _target = "", _calcInput = "";
    public string Label { get => _label; set => Set(ref _label, value); }
    public string FrequencyMhz { get => _frequency; set => Set(ref _frequency, value); }
    public string Type { get => _type; set => Set(ref _type, value); }
    public string StepDownDb { get => _stepDown; set => Set(ref _stepDown, value); }
    public string MeasuredDbmv { get => _measured; set { if (Set(ref _measured, value)) PropertyChanged?.Invoke(this, new(nameof(DeviationDb))); } }
    public string ConfiguredTargetDbmv { get => _target; set { if (Set(ref _target, value)) PropertyChanged?.Invoke(this, new(nameof(DeviationDb))); } }
    public string CalcInputDbmv { get => _calcInput; set => Set(ref _calcInput, value); }
    public string? DeviationDb => TryDecimal(MeasuredDbmv, out var measured) && TryDecimal(ConfiguredTargetDbmv, out var target)
        ? (measured - target).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
        : null;

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    private bool Set(ref string field, string value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (field == value) return false;
        field = value;
        PropertyChanged?.Invoke(this, new(name));
        return true;
    }

    private static bool TryDecimal(string value, out decimal result) => decimal.TryParse(value,
        System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result);
}

public sealed class ManualEntry
{
    public string Destination { get; set; } = "";
    public string Value { get; set; } = "";
    public string Unit { get; set; } = "";
    public string Basis { get; set; } = "Supervisor-approved manual entry";
}

public sealed class JobSession
{
    public int SchemaVersion { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
    public string EquipmentType { get; set; } = "Trunk";
    public string EquipmentModel { get; set; } = "";
    public string AssetId { get; set; } = "";
    public string Notes { get; set; } = "";
    public WorkProfile Profile { get; set; } = new();
    public string LowReadingDbmv { get; set; } = "";
    public string HighReadingDbmv { get; set; } = "";
    public PilotColumn[] PilotColumns { get; set; } = Enumerable.Range(0, 4).Select(i => new PilotColumn { Label = PilotExample.Labels[i] }).ToArray();
    public List<ManualEntry> ManualEntries { get; set; } = [];
}

public sealed record CheckResult(bool IsValid, string Message, decimal? LowErrorDb, decimal? HighErrorDb, decimal? ActualTiltDb, decimal? TiltErrorDb, bool? LowPass, bool? HighPass, bool? TiltPass, bool? OverallPass);
