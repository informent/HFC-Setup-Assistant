using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using HfcSetupAssistant.Core;
using Microsoft.Win32;

namespace HfcSetupAssistant.Desktop;

public partial class MainWindow : Window
{
    private JobSession _session = new();
    private readonly List<WorkProfile> _profiles;
    private bool _loading;
    private bool _profileInputsValid = true;
    private string? _currentPath;
    private readonly ObservableCollection<ManualEntry> _entries = [];

    public MainWindow()
    {
        InitializeComponent();
        try { _profiles = LocalStore.LoadProfiles(); }
        catch (InvalidDataException ex)
        {
            _profiles = [new WorkProfile()];
            MessageBox.Show(ex.Message + "\nA default profile is loaded for this session; the saved file has not been overwritten.", "Profiles unavailable", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        _loading = true;
        ProfilePicker.ItemsSource = _profiles;
        ProfilePicker.DisplayMemberPath = nameof(WorkProfile.Name);
        ProfilePicker.SelectedIndex = 0;
        LoadSessionToUi(_session);
        _loading = false;
        Recalculate();
    }

    private WorkProfile SelectedProfile => (ProfilePicker.SelectedItem as WorkProfile) ?? _profiles[0];

    private void LoadSessionToUi(JobSession session)
    {
        _session = session;
        _loading = true;
        EquipmentType.SelectedIndex = session.EquipmentType == "Line extender" ? 1 : 0;
        EquipmentModel.Text = session.EquipmentModel;
        AssetId.Text = session.AssetId;
        LowReading.Text = session.LowReadingDbmv;
        HighReading.Text = session.HighReadingDbmv;
        LowFrequency.Text = session.Profile.LowFrequencyMhz;
        HighFrequency.Text = session.Profile.HighFrequencyMhz;
        MeasurementPoint.Text = session.Profile.MeasurementPoint;
        Notes.Text = session.Notes;
        var match = _profiles.FirstOrDefault(p => p.Name == session.Profile.Name);
        if (match is null) { match = CopyProfile(session.Profile); _profiles.Add(match); }
        ProfilePicker.SelectedItem = match;
        ShowProfile(match);
        PilotGrid.ItemsSource = session.PilotColumns;
        ShowPrecedingReference(session.PrecedingAmplifier);
        RefreshEntryValues();
        _entries.Clear();
        foreach (var item in session.ManualEntries) _entries.Add(item);
        EntriesList.ItemsSource = _entries;
        EntriesList.ItemTemplate = (DataTemplate)FindResource("EntryTemplate");
        _loading = false;
        Recalculate();
    }

    private static WorkProfile CopyProfile(WorkProfile p) => new()
    {
        Name = p.Name, LowTargetDbmv = p.LowTargetDbmv, HighTargetDbmv = p.HighTargetDbmv,
        LowMinimumDbmv = p.LowMinimumDbmv, LowMaximumDbmv = p.LowMaximumDbmv,
        HighMinimumDbmv = p.HighMinimumDbmv, HighMaximumDbmv = p.HighMaximumDbmv,
        TiltMinimumDb = p.TiltMinimumDb, TiltMaximumDb = p.TiltMaximumDb,
        LowFrequencyMhz = p.LowFrequencyMhz, HighFrequencyMhz = p.HighFrequencyMhz, MeasurementPoint = p.MeasurementPoint
    };

    private void ShowProfile(WorkProfile p)
    {
        _loading = true;
        ProfileName.Text = p.Name;
        ProfileLowTarget.Text = p.LowTargetDbmv.ToString(CultureInfo.InvariantCulture);
        ProfileHighTarget.Text = p.HighTargetDbmv.ToString(CultureInfo.InvariantCulture);
        LowMinimum.Text = p.LowMinimumDbmv.ToString(CultureInfo.InvariantCulture);
        LowMaximum.Text = p.LowMaximumDbmv.ToString(CultureInfo.InvariantCulture);
        HighMinimum.Text = p.HighMinimumDbmv.ToString(CultureInfo.InvariantCulture);
        HighMaximum.Text = p.HighMaximumDbmv.ToString(CultureInfo.InvariantCulture);
        TiltMinimum.Text = p.TiltMinimumDb.ToString(CultureInfo.InvariantCulture);
        TiltMaximum.Text = p.TiltMaximumDb.ToString(CultureInfo.InvariantCulture);
        LowFrequency.Text = p.LowFrequencyMhz;
        HighFrequency.Text = p.HighFrequencyMhz;
        MeasurementPoint.Text = p.MeasurementPoint;
        _profileInputsValid = TargetChecker.ValidateProfile(p) is null;
        _loading = false;
        Recalculate();
    }

    private void ProfilePicker_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || ProfilePicker.SelectedItem is not WorkProfile profile) return;
        _session.Profile = CopyProfile(profile);
        ShowProfile(profile);
    }

    private void ProfileEdited(object sender, TextChangedEventArgs e)
    {
        if (_loading || ProfilePicker.SelectedItem is not WorkProfile p) return;
        p.Name = string.IsNullOrWhiteSpace(ProfileName.Text) ? "Untitled profile" : ProfileName.Text.Trim();
        var lowOk = Try(ProfileLowTarget.Text, out var low);
        var highOk = Try(ProfileHighTarget.Text, out var high);
        var lowMinOk = Try(LowMinimum.Text, out var lowMin);
        var lowMaxOk = Try(LowMaximum.Text, out var lowMax);
        var highMinOk = Try(HighMinimum.Text, out var highMin);
        var highMaxOk = Try(HighMaximum.Text, out var highMax);
        var tiltMinOk = Try(TiltMinimum.Text, out var tiltMin);
        var tiltMaxOk = Try(TiltMaximum.Text, out var tiltMax);
        _profileInputsValid = lowOk && highOk && lowMinOk && lowMaxOk && highMinOk && highMaxOk && tiltMinOk && tiltMaxOk;
        if (_profileInputsValid)
        {
            p.LowTargetDbmv = low; p.HighTargetDbmv = high;
            p.LowMinimumDbmv = lowMin; p.LowMaximumDbmv = lowMax;
            p.HighMinimumDbmv = highMin; p.HighMaximumDbmv = highMax;
            p.TiltMinimumDb = tiltMin; p.TiltMaximumDb = tiltMax;
            _profileInputsValid = TargetChecker.ValidateProfile(p) is null;
        }
        _session.Profile = CopyProfile(p);
        ProfilePicker.Items.Refresh();
        Recalculate();
    }

    private void NewProfile_Click(object sender, RoutedEventArgs e)
    {
        var profile = CopyProfile(SelectedProfile);
        var rootName = "New profile";
        var name = rootName;
        var suffix = 2;
        while (_profiles.Any(existing => existing.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) name = $"{rootName} {suffix++}";
        profile.Name = name;
        _profiles.Add(profile);
        ProfilePicker.Items.Refresh();
        ProfilePicker.SelectedItem = profile;
        ShowProfile(profile);
        StatusText.Text = "New profile created · choose Save profile to persist it";
        ProfileName.Focus();
        ProfileName.SelectAll();
    }

    private void InputChanged(object sender, EventArgs e)
    {
        if (_loading) return;
        _session.EquipmentType = (EquipmentType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Trunk";
        _session.EquipmentModel = EquipmentModel.Text;
        _session.AssetId = AssetId.Text;
        _session.LowReadingDbmv = LowReading.Text;
        _session.HighReadingDbmv = HighReading.Text;
        _session.Notes = Notes.Text;
        var profile = SelectedProfile;
        profile.LowFrequencyMhz = LowFrequency.Text;
        profile.HighFrequencyMhz = HighFrequency.Text;
        profile.MeasurementPoint = MeasurementPoint.Text;
        _session.Profile = CopyProfile(profile);
        Recalculate();
        RefreshEntryValues();
    }

    private void EquipmentType_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        var type = (EquipmentType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Trunk";
        if (type == "Line extender" && _session.EquipmentType != "Line extender" && PilotRowsAreBlank())
        {
            PilotExample.LoadLineExtenderSetup(_session);
            PilotGrid.ItemsSource = null;
            PilotGrid.ItemsSource = _session.PilotColumns;
            ShowPrecedingReference(_session.PrecedingAmplifier);
        }
        InputChanged(sender, e);
    }

    private bool PilotRowsAreBlank() => _session.PilotColumns.All(column =>
        string.IsNullOrWhiteSpace(column.FrequencyMhz) && string.IsNullOrWhiteSpace(column.MeasuredDbmv) &&
        string.IsNullOrWhiteSpace(column.ConfiguredTargetDbmv) && string.IsNullOrWhiteSpace(column.CalcInputDbmv) &&
        column.Type == "U" && column.StepDownDb == "0") && _session.PrecedingAmplifier is null;

    private void LineExtenderPreset_Click(object sender, RoutedEventArgs e)
    {
        if (!PilotRowsAreBlank())
        {
            var answer = MessageBox.Show("Replace all four Pilot rows and the preceding-amplifier reference with the user-confirmed line extender entry set?", "Apply line extender entry set", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;
        }
        _loading = true;
        EquipmentType.SelectedIndex = 1;
        _loading = false;
        PilotExample.LoadLineExtenderSetup(_session);
        PilotGrid.ItemsSource = null;
        PilotGrid.ItemsSource = _session.PilotColumns;
        ShowPrecedingReference(_session.PrecedingAmplifier);
        RefreshEntryValues();
        StatusText.Text = "User-confirmed line extender entry set applied · meter readings remain blank";
    }

    private void PilotGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(RefreshEntryValues));
    }

    private void Recalculate()
    {
        if (LowReading is null || HighReading is null) return;
        var p = _session.Profile;
        NominalLow.Text = $"{p.LowTargetDbmv:0.##} dBmV";
        NominalHigh.Text = $"{p.HighTargetDbmv:0.##} dBmV";
        NominalTilt.Text = $"{p.HighTargetDbmv - p.LowTargetDbmv:0.##} dB";
        var result = _profileInputsValid
            ? TargetChecker.Evaluate(_session.LowReadingDbmv, _session.HighReadingDbmv, p)
            : new CheckResult(false, "Correct the profile values and limits before PASS is available.", null, null, null, null, null, null, null, null);
        LowError.Text = Format(result.LowErrorDb, "dB");
        HighError.Text = Format(result.HighErrorDb, "dB");
        ActualTilt.Text = Format(result.ActualTiltDb, "dB");
        TiltError.Text = Format(result.TiltErrorDb, "dB");
        SetCheck(LowStatus, LowStatusCard, "LOW", result.LowPass);
        SetCheck(HighStatus, HighStatusCard, "HIGH", result.HighPass);
        SetCheck(TiltStatus, TiltStatusCard, "TILT", result.TiltPass);
        OverallStatus.Text = result.Message;
        OverallCard.Background = result.OverallPass switch { true => Brush("#DFF2E9"), false => Brush("#FCE7E4"), _ => Brush("#EEF1F3") };
    }

    private static string Format(decimal? value, string unit) => value is null ? "—" : $"{value.Value:+0.##;-0.##;0} {unit}";
    private static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
    private static void SetCheck(TextBlock text, Border card, string label, bool? pass)
    {
        text.Text = $"{label} · {pass switch { true => "PASS", false => "FAIL", null => "Incomplete" }}";
        card.Background = pass switch { true => Brush("#DFF2E9"), false => Brush("#FCE7E4"), _ => Brush("#EEF1F3") };
    }
    private static bool Try(string text, out decimal value) => decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        var profileError = TargetChecker.ValidateProfile(SelectedProfile);
        if (!_profileInputsValid || profileError is not null)
        { MessageBox.Show(profileError ?? "Correct the invalid profile fields before saving.", "Profile values", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        try { LocalStore.SaveProfiles(_profiles); StatusText.Text = $"Profile saved locally · {DateTime.Now:t}"; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { ShowError("Could not save profile", ex); }
    }

    private void NewJob_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show("Start a blank job? Unsaved changes in this session will be cleared.", "New job", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;
        _currentPath = null;
        var session = new JobSession { Profile = CopyProfile(SelectedProfile), PilotColumns = Enumerable.Range(0, 4).Select(i => new PilotColumn { Label = PilotExample.Labels[i] }).ToArray() };
        LoadSessionToUi(session);
        StatusText.Text = "Blank job · readings are empty";
    }

    private void Example_Click(object sender, RoutedEventArgs e)
    {
        _loading = true;
        EquipmentType.SelectedIndex = 1;
        _loading = false;
        PilotExample.Load(_session);
        PilotGrid.ItemsSource = null;
        PilotGrid.ItemsSource = _session.PilotColumns;
        ShowPrecedingReference(_session.PrecedingAmplifier);
        RefreshEntryValues();
        StatusText.Text = "Loaded screenshot reference · work targets remain unchanged";
    }

    private void ShowPrecedingReference(PrecedingAmplifierReference? reference)
    {
        PrecedingReferenceText.Text = reference is null
            ? "No preceding-amplifier target is recorded in this job."
            : $"High main target: {reference.HighMainTargetDbmv} dBmV. “{reference.SyncControlLabel}” appears in the photo; {reference.SyncControlNote} {reference.CroppedSectionNote}";
    }

    private void RefreshEntryValues()
    {
        if (EntryValuesList is null) return;
        var entries = PilotExample.GetLineExtenderEntries(_session);
        EntryValuesList.ItemsSource = entries;
        EntryGuidance.Text = entries.Count == 0
            ? "Select Line extender or Apply LE entry set to load the user-confirmed manual values."
            : "User-confirmed line extender values for manual entry. Measurements and Calc. Input remain blank for each new job.";
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "HFC job (*.hfcjob)|*.hfcjob|JSON files (*.json)|*.json|All files (*.*)|*.*", Title = "Open HFC job" };
        if (dialog.ShowDialog(this) != true) return;
        try { var session = LocalStore.LoadJob(dialog.FileName); _currentPath = dialog.FileName; LoadSessionToUi(session); StatusText.Text = $"Opened local job · {Path.GetFileName(dialog.FileName)}"; }
        catch (InvalidDataException ex) { ShowError("Could not open job", ex); }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        SyncForm();
        if (string.IsNullOrWhiteSpace(_currentPath))
        {
            var dialog = new SaveFileDialog { Filter = "HFC job (*.hfcjob)|*.hfcjob", FileName = string.IsNullOrWhiteSpace(_session.AssetId) ? "Field job.hfcjob" : _session.AssetId + ".hfcjob", Title = "Save HFC job" };
            if (dialog.ShowDialog(this) != true) return;
            _currentPath = dialog.FileName;
        }
        try { LocalStore.SaveJob(_session, _currentPath); StatusText.Text = $"Saved locally · {DateTime.Now:t} · {Path.GetFileName(_currentPath)}"; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { ShowError("Could not save job", ex); }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        SyncForm();
        var dialog = new SaveFileDialog { Filter = "CSV file (*.csv)|*.csv", FileName = "HFC-field-record.csv", Title = "Export field record" };
        if (dialog.ShowDialog(this) != true) return;
        try { LocalStore.ExportCsv(_session, dialog.FileName); StatusText.Text = $"Exported CSV · {Path.GetFileName(dialog.FileName)}"; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { ShowError("Could not export CSV", ex); }
    }

    private void SyncForm()
    {
        _session.EquipmentType = (EquipmentType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Trunk";
        _session.EquipmentModel = EquipmentModel.Text; _session.AssetId = AssetId.Text;
        _session.LowReadingDbmv = LowReading.Text; _session.HighReadingDbmv = HighReading.Text; _session.Notes = Notes.Text;
        var p = SelectedProfile;
        p.LowFrequencyMhz = LowFrequency.Text; p.HighFrequencyMhz = HighFrequency.Text; p.MeasurementPoint = MeasurementPoint.Text;
        _session.Profile = CopyProfile(p);
        PilotGrid.CommitEdit(DataGridEditingUnit.Cell, true); PilotGrid.CommitEdit(DataGridEditingUnit.Row, true);
        _session.ManualEntries = _entries.ToList();
    }

    private void AddEntry_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EntryDestination.Text) || string.IsNullOrWhiteSpace(EntryValue.Text))
        { MessageBox.Show("Enter the exact destination field and the supervisor-approved value.", "Manual entry", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        _entries.Add(new ManualEntry { Destination = EntryDestination.Text.Trim(), Value = EntryValue.Text.Trim(), Unit = EntryUnit.Text.Trim() });
        _session.ManualEntries = _entries.ToList();
        EntryDestination.Clear(); EntryValue.Clear(); EntryUnit.Clear();
    }

    private void RemoveEntry_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ManualEntry entry }) _entries.Remove(entry);
        _session.ManualEntries = _entries.ToList();
    }

    private void ShowError(string title, Exception ex) => MessageBox.Show(ex.Message + "\n\nYour current in-memory session is still open.", title, MessageBoxButton.OK, MessageBoxImage.Error);
}
