# Kayle's Steps Recorder

A production-grade Windows step recorder for QA workflows. Captures user actions with screenshots of the correct target window, identifies UI elements via UI Automation, and exports polished HTML/Markdown reports.

## Requirements

- **Windows 10 / 11** (x64)
- **.NET 8 SDK** - https://dotnet.microsoft.com/download/dotnet/8.0
- **Visual Studio 2022** or **Visual Studio Code** with C# Dev Kit (optional, for development)

## Solution Structure

```
KaylesStepsRecorder.sln
└── src/
    ├── KaylesStepsRecorder.App          # WPF UI (entry point)
    ├── KaylesStepsRecorder.Core         # Models, interfaces, enums
    ├── KaylesStepsRecorder.Hooks        # Global mouse/keyboard hooks
    ├── KaylesStepsRecorder.Capture      # Screenshot + DPI handling
    ├── KaylesStepsRecorder.Automation   # UI Automation inspection
    ├── KaylesStepsRecorder.Engine       # Recording orchestrator
    ├── KaylesStepsRecorder.Storage      # Session persistence
    └── KaylesStepsRecorder.Export       # HTML/Markdown export
```

## Build & Run

### From Visual Studio 2022
1. Open `KaylesStepsRecorder.sln`
2. Set `KaylesStepsRecorder.App` as the startup project
3. Press **F5** to build and run

### From the command line
```powershell
# Restore dependencies
dotnet restore

# Debug build
dotnet build

# Run the app
dotnet run --project src\KaylesStepsRecorder.App
```

### Publish as single-file EXE
```powershell
dotnet publish src\KaylesStepsRecorder.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The EXE will be produced at `src\KaylesStepsRecorder.App\bin\Release\net8.0-windows\win-x64\publish\KaylesStepsRecorder.exe`.

## Features

### Recording
- Global low-level mouse/keyboard hooks (`WH_MOUSE_LL`, `WH_KEYBOARD_LL`)
- Event debouncing to suppress duplicate events
- Auto-pause on excluded applications
- Configurable capture delay to let the UI settle before screenshotting

### Screenshots
- Window-only capture via `PrintWindow` with `PW_RENDERFULLCONTENT` (handles DWM compositing)
- Fallback to `BitBlt` for legacy apps
- Per-monitor DPI awareness via `GetDpiForWindow`
- `DwmGetWindowAttribute` + `DWMWA_EXTENDED_FRAME_BOUNDS` for shadow-free bounds
- Multi-monitor support
- Click highlight overlays
- Redaction for sensitive content

### Element Identification
- `AutomationElement.FromPoint` via Windows UI Automation
- Extracts control name, type, AutomationId, value
- Graceful fallback to "Clicked at (X,Y) in [Window]" if UIA fails
- 2-second timeout to prevent hangs on unresponsive apps

### Step Editor
- Reorder, delete, rename steps
- Add notes, flag Important/Bug/Expected/Actual
- Redact individual screenshots
- Full session metadata (bug title, build version, tester, environment, etc.)

### Export
- **HTML Full** - Professional report with gradient header, metadata cards, step gallery
- **HTML Compact** - Streamlined single-page layout
- **Markdown** - For Jira, GitHub Issues, or documentation
- Base64-embedded images for self-contained files
- Ready to attach to bug tickets

### Reliability
- JSON-based session persistence at `%LOCALAPPDATA%\KaylesStepsRecorder\Sessions\{sessionId}`
- Autosave timer (default every 30 seconds)
- Crash recovery via `.lock` file detection on startup
- All screenshot I/O happens async, off the UI thread

## Architecture

**Service-oriented** with dependency injection via `Microsoft.Extensions.DependencyInjection`:

- `IInputHookService` - global hook install/uninstall + event dispatch
- `IScreenCaptureService` - screenshot/thumbnail/highlight/redaction
- `IElementInspector` - UI Automation lookup at coordinates
- `IWindowTracker` - foreground/point-based window resolution + exclusion rules
- `IStepDescriptionBuilder` - human-readable descriptions from action + element + window
- `ISessionStorage` - create/save/load/recover sessions
- `IExportService` - HTML and Markdown exporters (multiple registered)
- `IRecordingEngine` - orchestrates the whole pipeline

**MVVM** on the WPF side with:
- `ViewModelBase` (INotifyPropertyChanged)
- `RelayCommand` / `AsyncRelayCommand`
- Child view models: `RecordingViewModel`, `StepEditorViewModel`, `ExportViewModel`, `SettingsViewModel`

## Settings Location

- **Sessions**: `%LOCALAPPDATA%\KaylesStepsRecorder\Sessions\`
- **Crash log**: `%LOCALAPPDATA%\KaylesStepsRecorder\Sessions\crash.log`

## Privacy

- Add processes to **excluded list** in Settings to prevent capture
- Enable **auto-pause** when an excluded app is focused
- **Per-step redaction** lets you blur sensitive regions before export
- Export happens **entirely locally** - no network calls

## Troubleshooting

### Build errors after pull
```powershell
dotnet restore
dotnet build
```

### UI Automation not working
Some apps (especially those running elevated) require the recorder itself to run elevated to read their element tree. Right-click the EXE and choose **Run as administrator**.

### Screenshots are black on certain apps
Some apps (e.g., games using exclusive fullscreen, hardware-accelerated video) cannot be captured by `PrintWindow`. The capture service automatically falls back to `BitBlt`, but some apps may still produce black frames. Try switching to full-screen capture mode in Settings.

### Hooks don't fire in some apps
Global low-level hooks require the app to be running at or below the privilege level of the target app. Elevated target apps (e.g., Task Manager) require the recorder to also run elevated.
