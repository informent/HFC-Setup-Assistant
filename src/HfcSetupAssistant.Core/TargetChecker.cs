using System.Globalization;

namespace HfcSetupAssistant.Core;

public static class TargetChecker
{
    public static CheckResult Evaluate(string lowText, string highText, WorkProfile profile)
    {
        if (!TryFiniteDecimal(lowText, out var low) || !TryFiniteDecimal(highText, out var high))
            return new(false, "Enter valid finite low and high readings to calculate.", null, null, null, null, null, null, null, null);

        var profileError = ValidateProfile(profile);
        if (profileError is not null)
            return new(false, profileError, null, null, null, null, null, null, null, null);

        var actualTilt = high - low;
        var lowError = low - profile.LowTargetDbmv;
        var highError = high - profile.HighTargetDbmv;
        var tiltError = actualTilt - (profile.HighTargetDbmv - profile.LowTargetDbmv);
        if (!TryFiniteDecimal(profile.LowFrequencyMhz, out var lowFrequency) || lowFrequency <= 0 ||
            !TryFiniteDecimal(profile.HighFrequencyMhz, out var highFrequency) || highFrequency <= 0 ||
            string.IsNullOrWhiteSpace(profile.MeasurementPoint))
            return new(false, "Enter valid positive low and high frequencies and the measurement point before PASS is available.",
                lowError, highError, actualTilt, tiltError, null, null, null, null);

        var lowPass = low >= profile.LowMinimumDbmv && low <= profile.LowMaximumDbmv;
        var highPass = high >= profile.HighMinimumDbmv && high <= profile.HighMaximumDbmv;
        var tiltPass = actualTilt >= profile.TiltMinimumDb && actualTilt <= profile.TiltMaximumDb;
        return new(true, lowPass && highPass && tiltPass ? "PASS — all three checks are within profile limits." : "FAIL — one or more checks are outside profile limits.",
            lowError, highError, actualTilt, tiltError, lowPass, highPass, tiltPass, lowPass && highPass && tiltPass);
    }

    public static string? ValidateProfile(WorkProfile profile)
    {
        if (profile.LowMinimumDbmv > profile.LowMaximumDbmv || profile.HighMinimumDbmv > profile.HighMaximumDbmv || profile.TiltMinimumDb > profile.TiltMaximumDb)
            return "Profile limits are invalid. Correct each minimum and maximum before checking readings.";
        if (profile.LowTargetDbmv < profile.LowMinimumDbmv || profile.LowTargetDbmv > profile.LowMaximumDbmv ||
            profile.HighTargetDbmv < profile.HighMinimumDbmv || profile.HighTargetDbmv > profile.HighMaximumDbmv)
            return "Each nominal level target must fall within its accepted level range.";
        var targetTilt = profile.HighTargetDbmv - profile.LowTargetDbmv;
        if (targetTilt < profile.TiltMinimumDb || targetTilt > profile.TiltMaximumDb)
            return "The difference between nominal high and low targets must fall within the accepted tilt range.";
        return null;
    }

    private static bool TryFiniteDecimal(string text, out decimal value)
    {
        return decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}

public static class PilotExample
{
    public static readonly string[] Labels = ["HI MAIN", "HI BACKUP", "LO MAIN", "LO BACKUP"];

    public static List<PilotSettingEntry> GetLineExtenderEntries(JobSession session)
    {
        if (session.EquipmentType != "Line extender") return [];
        var entries = new List<PilotSettingEntry>();
        foreach (var column in session.PilotColumns)
        {
            AddIfPresent(entries, $"{column.Label} / Frequency", column.FrequencyMhz, "MHz");
            AddIfPresent(entries, $"{column.Label} / Type", column.Type, "");
            AddIfPresent(entries, $"{column.Label} / Step down", column.StepDownDb, "dB");
            AddIfPresent(entries, $"{column.Label} / Current target level", column.ConfiguredTargetDbmv, "dBmV");
        }
        if (session.PrecedingAmplifier is { } preceding)
            AddIfPresent(entries, "Preceding amplifier target levels / High main", preceding.HighMainTargetDbmv, "dBmV");
        return entries;
    }

    private static void AddIfPresent(List<PilotSettingEntry> entries, string destination, string value, string unit)
    {
        if (!string.IsNullOrWhiteSpace(value)) entries.Add(new() { Destination = destination, Value = value, Unit = unit });
    }

    public static void LoadLineExtenderSetup(JobSession session)
    {
        session.EquipmentType = "Line extender";
        session.PilotColumns =
        [
            new() { Label = Labels[0], FrequencyMhz = "711.00", Type = "U", StepDownDb = "0", MeasuredDbmv = "", ConfiguredTargetDbmv = "39.0", CalcInputDbmv = "" },
            new() { Label = Labels[1], FrequencyMhz = "693.00", Type = "U", StepDownDb = "0", MeasuredDbmv = "", ConfiguredTargetDbmv = "39.0", CalcInputDbmv = "" },
            new() { Label = Labels[2], FrequencyMhz = "141.00", Type = "U", StepDownDb = "0", MeasuredDbmv = "", ConfiguredTargetDbmv = "29.0", CalcInputDbmv = "" },
            new() { Label = Labels[3], FrequencyMhz = "153.00", Type = "U", StepDownDb = "0", MeasuredDbmv = "", ConfiguredTargetDbmv = "29.0", CalcInputDbmv = "" }
        ];
        session.PrecedingAmplifier = new();
    }

    public static void Load(JobSession session)
    {
        LoadLineExtenderSetup(session);
        session.PilotColumns =
        [
            new() { Label = Labels[0], FrequencyMhz = "711.00", Type = "U", StepDownDb = "0", MeasuredDbmv = "39.0", ConfiguredTargetDbmv = "39.0", CalcInputDbmv = "20" },
            new() { Label = Labels[1], FrequencyMhz = "693.00", Type = "U", StepDownDb = "0", MeasuredDbmv = "39.0", ConfiguredTargetDbmv = "39.0", CalcInputDbmv = "21" },
            new() { Label = Labels[2], FrequencyMhz = "141.00", Type = "U", StepDownDb = "0", MeasuredDbmv = "29.0", ConfiguredTargetDbmv = "29.0", CalcInputDbmv = "20" },
            new() { Label = Labels[3], FrequencyMhz = "153.00", Type = "U", StepDownDb = "0", MeasuredDbmv = "26.8", ConfiguredTargetDbmv = "29.0", CalcInputDbmv = "17" }
        ];
    }
}
