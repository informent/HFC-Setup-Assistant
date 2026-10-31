using HfcSetupAssistant.Core;

var checks = new List<(string Name, Action Run)>
{
    ("nominal targets pass", () => Check("28", "38", true)),
    ("inclusive lower boundaries pass", () => Check("27", "37", true)),
    ("inclusive upper boundaries pass", () => Check("29", "39", true)),
    ("level passes do not hide excessive tilt", () => Check("27", "39", false, true, true, false)),
    ("level passes do not hide insufficient tilt", () => Check("29", "37", false, true, true, false)),
    ("low and tilt fail for 26.8 / 39", () => Check("26.8", "39", false, false, true, false)),
    ("blank readings are incomplete", () => Invalid("", "38")),
    ("nonnumeric readings are incomplete", () => Invalid("oops", "38")),
    ("NaN readings are incomplete", () => Invalid("NaN", "38")),
    ("infinite readings are incomplete", () => Invalid("Infinity", "38")),
    ("missing frequency blocks PASS", MissingFrequency),
    ("invalid frequency blocks PASS", InvalidFrequency),
    ("missing reference point blocks PASS", MissingPoint),
    ("screenshot example keeps configured targets and deviations", ScreenshotExample),
    ("job save and reopen preserves profile, inputs, and column order", JobRoundTrip),
    ("CSV retains units and Pilot order", CsvOrder)
};

var failures = 0;
foreach (var (name, run) in checks)
{
    try { run(); Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failures++; Console.Error.WriteLine($"FAIL {name}: {ex.Message}"); }
}
Console.WriteLine($"{checks.Count - failures}/{checks.Count} checks passed.");
return failures == 0 ? 0 : 1;

static void Check(string low, string high, bool expectedOverall, bool? expectedLow = null, bool? expectedHigh = null, bool? expectedTilt = null)
{
    var r = TargetChecker.Evaluate(low, high, ValidProfile());
    Equal(true, r.IsValid);
    Equal(expectedOverall, r.OverallPass);
    if (expectedLow is not null) Equal(expectedLow, r.LowPass);
    if (expectedHigh is not null) Equal(expectedHigh, r.HighPass);
    if (expectedTilt is not null) Equal(expectedTilt, r.TiltPass);
}
static void Invalid(string low, string high)
{
    var r = TargetChecker.Evaluate(low, high, new WorkProfile());
    Equal(false, r.IsValid); Equal(null, r.OverallPass); Equal(null, r.ActualTiltDb);
}
static void MissingFrequency()
{
    var p = ValidProfile(); p.LowFrequencyMhz = "";
    var r = TargetChecker.Evaluate("28", "38", p);
    Equal(false, r.IsValid); Equal(null, r.OverallPass); Equal(null, r.LowPass);
}
static void InvalidFrequency()
{
    var p = ValidProfile(); p.HighFrequencyMhz = "0";
    var r = TargetChecker.Evaluate("28", "38", p);
    Equal(false, r.IsValid); Equal(null, r.OverallPass);
}
static void MissingPoint()
{
    var p = ValidProfile(); p.MeasurementPoint = " ";
    var r = TargetChecker.Evaluate("28", "38", p);
    Equal(false, r.IsValid); Equal(null, r.OverallPass);
}
static WorkProfile ValidProfile() => new() { LowFrequencyMhz = "141", HighFrequencyMhz = "711", MeasurementPoint = "Verified point" };
static void ScreenshotExample()
{
    var job = new JobSession();
    PilotExample.Load(job);
    Equal("38", job.Profile.HighTargetDbmv.ToString()); Equal("28", job.Profile.LowTargetDbmv.ToString());
    Equal("0", job.PilotColumns[0].DeviationDb); Equal("0", job.PilotColumns[1].DeviationDb);
    Equal("0", job.PilotColumns[2].DeviationDb); Equal("-2.2", job.PilotColumns[3].DeviationDb);
    Equal("HI MAIN", job.PilotColumns[0].Label); Equal("HI BACKUP", job.PilotColumns[1].Label);
    Equal("LO MAIN", job.PilotColumns[2].Label); Equal("LO BACKUP", job.PilotColumns[3].Label);
}
static void JobRoundTrip()
{
    var path = Path.Combine(Path.GetTempPath(), $"hfc-check-{Guid.NewGuid():N}.hfcjob");
    try
    {
        var job = new JobSession { EquipmentModel = "Test model", AssetId = "NODE-7", LowReadingDbmv = "27.5", HighReadingDbmv = "37.5" };
        job.Profile.MeasurementPoint = "test point"; PilotExample.Load(job);
        LocalStore.SaveJob(job, path);
        var loaded = LocalStore.LoadJob(path);
        Equal(job.EquipmentModel, loaded.EquipmentModel); Equal(job.AssetId, loaded.AssetId);
        Equal("test point", loaded.Profile.MeasurementPoint); Equal("711.00", loaded.PilotColumns[0].FrequencyMhz);
        Equal("LO BACKUP", loaded.PilotColumns[3].Label); Equal("26.8", loaded.PilotColumns[3].MeasuredDbmv);
    }
    finally { if (File.Exists(path)) File.Delete(path); }
}
static void CsvOrder()
{
    var path = Path.Combine(Path.GetTempPath(), $"hfc-check-{Guid.NewGuid():N}.csv");
    try
    {
        var job = new JobSession { LowReadingDbmv = "27", HighReadingDbmv = "39" };
        job.Profile.LowFrequencyMhz = "141"; job.Profile.HighFrequencyMhz = "711"; job.Profile.MeasurementPoint = "Verified test point";
        PilotExample.Load(job); LocalStore.ExportCsv(job, path);
        var csv = File.ReadAllText(path);
        var labels = new[] { "HI MAIN", "HI BACKUP", "LO MAIN", "LO BACKUP" };
        var positions = labels.Select(label => csv.IndexOf(label, StringComparison.Ordinal)).ToArray();
        Equal(true, positions.SequenceEqual(positions.Order()));
        Equal(true, csv.Contains("dBmV") && csv.Contains("MHz") && csv.Contains("dB"));
        Equal(true, csv.Contains("\"-2.2\",\"dB\""));
        Equal(true, csv.Contains("\"Low level\",\"PASS\""));
        Equal(true, csv.Contains("\"High level\",\"PASS\""));
        Equal(true, csv.Contains("\"Tilt\",\"FAIL\""));
        Equal(true, csv.Contains("\"Low minimum\",\"27\",\"dBmV\""));
        Equal(true, csv.Contains("\"Profile\",\"Name\",\"My field targets\""));
    }
    finally { if (File.Exists(path)) File.Delete(path); }
}
static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
}
