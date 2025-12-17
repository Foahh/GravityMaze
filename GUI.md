# Controller (Desktop GUI)

This folder (`Controller/`) contains a small desktop app you can run on your computer to control the Unity/VR project.

- **What it does**:
  - Sends control/settings messages to Unity over **UDP** (network messages).
  - Optionally connects to an **Arduino over USB (serial)** and forwards each line it receives to Unity over UDP.
  - Gives you buttons/sliders for maze settings, camera settings, and quick actions like calibrate/recenter.

- **What you do *not* need to know**:
  - You don’t need to know “.NET”, “Avalonia”, or “MVVM” to run it. Those are just the technologies used to build it.

## Quick start (run it)

### 1. Install the required runtime (one-time)
This app is built with **.NET 10**. You need the **.NET SDK** installed to build/run it from source.

- Install **.NET 10 SDK** (preview is OK for this repo) from Microsoft’s site: [Download .NET](https://dotnet.microsoft.com/download)
- After installing, verify it works:

```bash
dotnet --version
```

If `dotnet` isn’t found, your install didn’t complete or your terminal needs to be restarted.

### 2. Run the app
From the repo root:

```bash
dotnet restore Controller/Controller.csproj
dotnet run --project Controller/Controller.csproj
```

Notes:
- The repo includes `Controller/global.json`, which can request a specific SDK version (including previews). If your installed SDK is “close but not exact”, install the version it asks for.
- On Windows the app runs as a windowed app (no console window). On macOS/Linux you’ll still run it from a terminal the same way.

## Using the app (what to click)

### Connect to Unity (UDP)
In the **Connection** section:
- Set **TargetHost** to the machine running Unity (often `127.0.0.1` if Unity is on the same computer).
- Set **TargetPort** to the UDP port Unity is listening on.

Then use:
- **Generate Maze** to send maze settings (and camera settings).
- **Calibrate** to send a calibrate command (and camera settings).
- **Recenter VR** to send a recenter command.

### Optional: Connect to Arduino (serial → UDP bridge)
If you’re using an Arduino:
- Plug it in via USB.
- Click the Arduino connect/disconnect button in the **Connection** section.

What happens when it connects:
- The app opens the serial port at **115200 baud**.
- It waits briefly for the board to reset (common on connect).
- Any newline-delimited line the Arduino prints is forwarded to Unity as a UDP message.

## Common troubleshooting (non-developer friendly)

- **Unity doesn’t react**
  - Make sure Unity is actually listening on the same **host/port** you set in the GUI.
  - Firewalls can block UDP; try localhost first (`127.0.0.1`) with Unity + GUI on the same machine.

- **W/A/S/D does nothing**
  - Click inside the GUI window first so it has keyboard focus.
  - Only `W`, `A`, `S`, `D` are handled.

- **Arduino says “Not found” or won’t connect**
  - Make sure your OS can see the device as a serial port (drivers/cable).
  - On Linux you may need serial permissions (e.g. `dialout` group).
  - On macOS, port names usually look like `tty.usb*`; this app will still try the “first available port” if it doesn’t match the usual Windows/Linux patterns.

- **Arduino repeatedly reconnects**
  - The app automatically retries if the connection fails.
  - Check the USB cable, correct baud rate (115200), and whether another program is already using the port.

---

## For developers (how it’s built)

### Tech stack (only if you’re editing the GUI)
- **.NET**: `net10.0`
- **UI framework**: Avalonia `11.3.9` (cross-platform desktop UI)
- **MVVM helpers**: `CommunityToolkit.Mvvm 8.4.0`
- **Serial**: `System.IO.Ports 9.0.0`

### Project layout
- `Controller/Program.cs`: app entry point; starts the desktop UI.
- `Controller/App.axaml`, `Controller/App.axaml.cs`: app theme/styling and bootstrapping.
- `Controller/MainWindow.axaml`, `Controller/MainWindow.axaml.cs`: the main window UI + small code-behind (lifecycle + keyboard).
- `Controller/MainWindowViewModel.cs`: app logic (state, commands, UDP send, Arduino loop, cleanup).
- `Controller/LabeledControl.axaml`, `Controller/LabeledControl.axaml.cs`: reusable “label above input” UI control.

### Networking (UDP)
- Uses `UdpClient` to send **UTF-8** strings to `(TargetHost, TargetPort)`.
- This app is **send-only** (no UDP receive).

#### Message formats
All messages are plain strings:
- **Maze settings**
  - Prefix: `MAZE:`
  - Format:
    - `MAZE:shape=<Disc|Cube>,gridSize=<int>,cellSize=<double>,wallHeight=<double>,wallThickness=<double>,lidThickness=<double>,useRandomSeed=<true|false>,seed=<int>,goalDistance=<double>,deadEndRemoval=<double>`
  - Values are formatted using `InvariantCulture`.
- **Camera settings**
  - Prefix: `CAM:`
  - Format: `CAM:angle=<double>,distance=<double>`
- **Move keys**
  - Prefix: `MOVE:`
  - Format: `MOVE:<keys>`
  - Sent by the view’s `KeyDown` handler for `W/A/S/D`.
- **Commands**
  - `CALIBRATE`
  - `RECENTER`

#### When messages are sent
- **Generate Maze** → sends `MAZE:...` then sends `CAM:...`.
- **Calibrate** → sends `CALIBRATE` then sends `CAM:...`.
- **Recenter VR** → sends `RECENTER`.
- Changing camera fields → automatically sends `CAM:...` via property change hooks.

### Arduino serial bridge (serial → UDP)
- Toggled by `ToggleArduinoCommand`.
- Serial loop runs on a background task.

#### Port selection
- Prefers:
  - `COM*` (Windows)
  - contains `ttyUSB` or `ttyACM` (Linux)
- Otherwise falls back to the first available port.
- Remembers the last successful port and tries it again on the next connect attempt.

#### Connection behavior
- Baud: `115200`
- Timeouts: `ReadTimeout = WriteTimeout = 1000ms`
- `DtrEnable` + `RtsEnable` set to `true`
- Waits `2000ms` after open (Arduino reset delay)
- On successful connect: updates UI status and sends UDP `CALIBRATE`

#### Read/forward loop
- Reads newline-delimited lines via `SerialPort.ReadLine()`
- `TimeoutException` is treated as “no data yet”
- Non-empty lines are trimmed and forwarded over UDP verbatim

### Lifecycle and cleanup
- `MainWindow` calls `_viewModel.Dispose()` on window close.
- View model implements `IDisposable` and `IAsyncDisposable`:
  - Cancels the Arduino loop
  - Closes/disposes the serial port
  - Disposes the UDP client
  - Async dispose awaits the serial task if running
