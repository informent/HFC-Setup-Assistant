# HFC Setup Assistant

An offline Windows desktop tool for recording field meter readings, checking them against editable work targets, and keeping a four-column CATVisor Cadet Pilot reference record. It does not connect to CATVisor or program an amplifier.

## Open the app

Run `HFC-Setup-Assistant.exe` and start a blank measurement session. The Windows x64 build is self-contained; no .NET installation, account, network connection, subscription, or API key is needed. After the source is published, the latest successful **Windows build** workflow run provides the executable as a downloadable artifact. Saved jobs and profiles stay on this PC.

## Field workflow

1. Select trunk or line extender, enter the model and node/address/asset ID.
2. Choose an existing work profile or select **New profile** to start from a copy of the current one. Edit the targets and limits, then use **Save profile** to persist it. Enter the low/high measurement frequencies and the meter measurement point/reference plane your crew uses.
3. Enter low and high readings in dBmV. The app shows each level error, high-minus-low tilt, tilt error, each status, and the overall status. Blank or invalid readings remain incomplete.
4. Use **Pilot reference** to record the observed fields in `HI MAIN`, `HI BACKUP`, `LO MAIN`, `LO BACKUP` order. **Load screenshot example** is a reference preset; it does not change the 28/38 work targets.
5. Save a `.hfcjob` file and optionally export a CSV field record. Local profiles are stored under `%LOCALAPPDATA%\InForment\HFC Setup Assistant`.

## Calculation conventions

The default work profile uses nominal low/high levels of 28/38 dBmV, inclusive level ranges of 27–29/37–39 dBmV, and inclusive tilt range of 9–11 dB. Tilt is `high reading − low reading`; level errors are reading minus the nominal target; tilt error is measured tilt minus nominal high-minus-low tilt. Values are calculated at full decimal precision and rounded for display. All limits are editable and are personal work defaults, not manufacturer specifications.

The screenshot preset records 711.00, 693.00, 141.00, and 153.00 MHz, `U`, zero step down, the observed readings, configured targets, and observed Calc. Input values. It computes measured-minus-configured-target deviations (0, 0, 0, −2.2 dB). `U` is retained without assigning a meaning. Calc. Input is recorded as an observation; no formula is inferred. The screenshot's configured targets remain separate from the 28/38 work profile.

The **Values to enter** panel stays at “Needs verified equipment procedure.” Confirm the actual equipment model, measurement frequencies/reference plane, test-point compensation, destination fields, and model-specific procedure before relying on Pilot-entry calculations. No Pilot values are guessed, and manually recorded supervisor-approved entries are clearly labeled.

## Save, reopen, and export

Use **Save job** to create or update a `.hfcjob` JSON session. **Open…** restores it later. A failed save or invalid file shows an error and leaves the active session open. Use **Save profile** after editing limits to persist the selected profile locally. CSV export includes the timestamp, equipment identity, profile name and limits, frequencies, measurement point, readings, targets, calculations, each individual check status and overall status, Pilot values in screenshot order, manual entries, and notes.

## Build from source

Install the .NET 10 SDK and Windows Desktop targeting pack. In PowerShell, from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Build-Windows.ps1
```

The executable is `artifacts/win-x64/HFC Setup Assistant.exe`. The single-file build is Windows x64 specific. Use a Windows machine to build and run it.
