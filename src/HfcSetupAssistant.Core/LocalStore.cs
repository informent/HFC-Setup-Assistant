using System.Text;
using System.Text.Json;

namespace HfcSetupAssistant.Core;

public static class LocalStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InForment", "HFC Setup Assistant");
    public static string ProfilesPath => Path.Combine(DataDirectory, "profiles.json");

    public static List<WorkProfile> LoadProfiles()
    {
        try
        {
            if (!File.Exists(ProfilesPath)) return [new WorkProfile()];
            var profiles = JsonSerializer.Deserialize<List<WorkProfile>>(File.ReadAllText(ProfilesPath), JsonOptions);
            return profiles is { Count: > 0 } ? profiles : [new WorkProfile()];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new InvalidDataException($"Could not read saved profiles: {ex.Message}", ex);
        }
    }

    public static void SaveProfiles(IEnumerable<WorkProfile> profiles)
    {
        Directory.CreateDirectory(DataDirectory);
        AtomicWrite(ProfilesPath, JsonSerializer.Serialize(profiles, JsonOptions));
    }

    public static void SaveJob(JobSession session, string path)
    {
        session.UpdatedAt = DateTimeOffset.Now;
        AtomicWrite(path, JsonSerializer.Serialize(session, JsonOptions));
    }

    public static JobSession LoadJob(string path)
    {
        try
        {
            var session = JsonSerializer.Deserialize<JobSession>(File.ReadAllText(path), JsonOptions);
            return session is { SchemaVersion: 1 } ? session : throw new InvalidDataException("This is not a supported HFC Setup Assistant job file.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new InvalidDataException($"Could not open job file: {ex.Message}", ex);
        }
    }

    public static void ExportCsv(JobSession session, string path)
    {
        session.UpdatedAt = DateTimeOffset.Now;
        var lines = new List<string> { "Section,Field,Value,Unit" };
        Add(lines, "Job", "Updated", session.UpdatedAt.ToString("O"), "local time");
        Add(lines, "Equipment", "Type", session.EquipmentType, "");
        Add(lines, "Equipment", "Model", session.EquipmentModel, "");
        Add(lines, "Equipment", "Asset ID", session.AssetId, "");
        Add(lines, "Profile", "Name", session.Profile.Name, "");
        Add(lines, "Measurement", "Low frequency", session.Profile.LowFrequencyMhz, "MHz");
        Add(lines, "Measurement", "High frequency", session.Profile.HighFrequencyMhz, "MHz");
        Add(lines, "Measurement", "Measurement point", session.Profile.MeasurementPoint, "");
        Add(lines, "Measurement", "Low reading", session.LowReadingDbmv, "dBmV");
        Add(lines, "Measurement", "High reading", session.HighReadingDbmv, "dBmV");
        Add(lines, "Target", "Low nominal", Number(session.Profile.LowTargetDbmv), "dBmV");
        Add(lines, "Target", "Low minimum", Number(session.Profile.LowMinimumDbmv), "dBmV");
        Add(lines, "Target", "Low maximum", Number(session.Profile.LowMaximumDbmv), "dBmV");
        Add(lines, "Target", "High nominal", Number(session.Profile.HighTargetDbmv), "dBmV");
        Add(lines, "Target", "High minimum", Number(session.Profile.HighMinimumDbmv), "dBmV");
        Add(lines, "Target", "High maximum", Number(session.Profile.HighMaximumDbmv), "dBmV");
        Add(lines, "Target", "Tilt minimum", Number(session.Profile.TiltMinimumDb), "dB");
        Add(lines, "Target", "Tilt maximum", Number(session.Profile.TiltMaximumDb), "dB");
        var result = TargetChecker.Evaluate(session.LowReadingDbmv, session.HighReadingDbmv, session.Profile);
        Add(lines, "Calculation", "Low error", result.LowErrorDb?.ToString() ?? "", "dB");
        Add(lines, "Calculation", "High error", result.HighErrorDb?.ToString() ?? "", "dB");
        Add(lines, "Calculation", "Actual tilt", result.ActualTiltDb?.ToString() ?? "", "dB");
        Add(lines, "Calculation", "Tilt error", result.TiltErrorDb?.ToString() ?? "", "dB");
        Add(lines, "Validation", "Low level", Status(result.LowPass), "");
        Add(lines, "Validation", "High level", Status(result.HighPass), "");
        Add(lines, "Validation", "Tilt", Status(result.TiltPass), "");
        Add(lines, "Validation", "Overall", result.OverallPass?.ToString() ?? "Incomplete", "");
        Add(lines, "Validation", "Status", result.Message, "");
        for (var i = 0; i < session.PilotColumns.Length; i++)
        {
            var p = session.PilotColumns[i];
            var prefix = $"Pilot {i + 1} {p.Label}";
            Add(lines, prefix, "Frequency", p.FrequencyMhz, "MHz");
            Add(lines, prefix, "Type", p.Type, "observed label");
            Add(lines, prefix, "Step down", p.StepDownDb, "dB");
            Add(lines, prefix, "Measured", p.MeasuredDbmv, "dBmV");
            Add(lines, prefix, "Configured target", p.ConfiguredTargetDbmv, "dBmV");
            Add(lines, prefix, "Measured minus configured target", p.DeviationDb ?? "", "dB");
            Add(lines, prefix, "Calc. Input (observed)", p.CalcInputDbmv, "dBmV");
        }
        foreach (var entry in session.ManualEntries) Add(lines, "Manual entry", entry.Destination, $"{entry.Value} ({entry.Basis})", entry.Unit);
        Add(lines, "Notes", "Notes", session.Notes, "");
        AtomicWrite(path, string.Join(Environment.NewLine, lines));
    }

    private static void Add(List<string> lines, string section, string field, string value, string unit) =>
        lines.Add(string.Join(',', new[] { section, field, value, unit }.Select(Escape)));

    private static string Number(decimal value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    private static string Status(bool? status) => status switch { true => "PASS", false => "FAIL", null => "Incomplete" };

    private static string Escape(string value) => "\"" + value.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";

    private static void AtomicWrite(string path, string content)
    {
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var temp = full + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temp, content, new UTF8Encoding(false));
            File.Move(temp, full, true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
