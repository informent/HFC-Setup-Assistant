# HFC Setup Assistant

An offline Windows desktop tool for recording field meter readings, checking them against editable work targets, and keeping a four-column CATVisor Cadet Pilot reference record. It does not connect to CATVisor or program an amplifier.

## Open the app

Download the single `HFC Setup Assistant.exe` from **GitHub Releases** and run it to start a blank measurement session. The Windows x64 build is self-contained; no separate installer, .NET installation, account, network connection, subscription, or API key is needed. Saved jobs and profiles stay on this PC. Each successful Windows build also keeps a workflow artifact for verification.

## Field workflow

1. Select trunk or line extender, enter the model and node/address/asset ID.
2. Choose an existing work profile or select **New profile** to start from a copy of the current one. Edit the targets and limits, then use **Save profile** to persist it. Enter the low/high measurement frequencies and the meter measurement point/reference plane your crew uses.
3. Enter low and high readings in dBmV. The app shows each level error, high-minus-low tilt, tilt error, each status, and the overall status. Blank or invalid readings remain incomplete.
4. Use **Pilot reference** to record the observed fields in `HI MAIN`, `HI BACKUP`, `LO MAIN`, `LO BACKUP` order. **Load screenshot example** is a reference preset; it does not change the 28/38 work targets.
5. Save a `.hfcjob` file and optionally export a CSV field record. Local profiles are stored under `%LOCALAPPDATA%\InForment\HFC Setup Assistant`.

## Calculation conventions

The default work profile uses nominal low/high levels of 28/38 dBmV, inclusive level ranges of 27–29/37–39 dBmV, and inclusive tilt range of 9–11 dB. Tilt is `high reading − low reading`; level errors are reading minus the nominal target; tilt error is measured tilt minus nominal high-minus-low tilt. Values are calculated at full decimal precision and rounded for display. All limits are editable and are personal work defaults, not manufacturer specifications.

A check stays incomplete unless both frequencies are positive, a measurement point is recorded, all readings are valid, and the selected profile's nominal targets and limits form a valid set. Invalid profile text or contradictory limits blocks PASS and profile saving; the previous values are not silently treated as the newly entered values.

The screenshot preset records 711.00, 693.00, 141.00, and 153.00 MHz, `U`, zero step down, the observed readings, configured targets, and observed Calc. Input values. It computes measured-minus-configured-target deviations (0, 0, 0, −2.2 dB). `U` is retained without assigning a meaning. Calc. Input is recorded as an observation; no formula is inferred. The screenshot's configured targets remain separate from the 28/38 work profile.

Selecting **Line extender** on a blank Pilot form loads the user-confirmed manual entry set: the four screenshot frequencies, `U`, 0 dB step down, and current target levels of 39/39/29/29 dBmV in screenshot order. The preceding-amplifier High main target is 39.0 dBmV. New meter readings and Calc. Input observations stay blank. **Apply LE entry set** can reapply these values after confirmation if Pilot fields already contain data. This is the crew's supplied entry set; the app does not infer a model formula or program the amplifier.

The **Values to enter** panel shows the user-confirmed line extender destinations and values for manual entry. Calculated Pilot values remain unavailable until the actual equipment model, measurement reference, test-point compensation, and model-specific procedure are verified. No values are sent to the amplifier.

## Save, reopen, and export

Use **Save job** to create or update a `.hfcjob` JSON session. **Open…** restores it later. A failed save or invalid file shows an error and leaves the active session open. Use **Save profile** after editing limits to persist the selected profile locally. CSV export includes the timestamp, equipment identity, profile name and limits, frequencies, measurement point, readings, targets, calculations, each individual check status and overall status, Pilot values in screenshot order, manual entries, and notes.

Use **New profile** to copy the current profile before editing another set of targets. Saved profiles use an atomic local JSON write; unreadable profile files are reported without overwriting the damaged file.

## Build from source

Install the .NET 10 SDK and Windows Desktop targeting pack. In PowerShell, from the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Build-Windows.ps1
```

The executable is `artifacts/win-x64/HFC Setup Assistant.exe`. The single-file build is Windows x64 specific. Use a Windows machine to build and run it.
