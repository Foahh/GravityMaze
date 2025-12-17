# ELEC4547 — Gravity Maze

A Unity-based VR maze game that combines **procedural maze generation** with **physical hardware control**.

- You play by navigating the maze in VR (Google Cardboard-style).
- An **Arduino + MPU9250 IMU “controller cube”** can rotate the maze in real time.
- A **desktop controller app** forwards IMU orientation and sends control commands to the Unity app over **UDP** (iOS/Android).

> Licensed assets were removed from this repository (e.g. MKGlass maze material, All in 1 VFX Toolkit particle effects). If you want the exact original visuals, you'll need to provide your own substitutes.

![Flat Maze Game Screenshot](Documents/2.gif "Flat Maze Screenshot on iPhone")

## Detailed Documentation

- **Maze generator**: [MazeGenerator.md](./MazeGenerator.md)

- **Unity scripts / game logic**: [Game.md](./Game.md)

- **Controller app**: [GUI.md](./GUI.md)

## What's in here

- **Unity project**: VR scene, gameplay scripts, UDP server, and maze generation
- **Desktop controller**: .NET/Avalonia GUI app (`Controller/`)
- **Arduino firmware**: IMU reader for MPU9250 (`Arduino/`)

## Features

- **Procedural maze generation**:
  - **Disc / flat maze** (classic 2D grid)
  - **Cube maze** (6 faces, connected across edges)
- **VR support** (Google Cardboard XR plugin):
  - Enter VR mode and recenter
  - First-person maze navigation
- **Hardware integration**:
  - IMU-driven real-time maze orientation
- **Networking**:
  - UDP-based command/control and sensor forwarding
- **Camera system**:
  - Auto framing based on maze size, with configurable angles and optional smoothing

![Unity Editor Preview](Documents/4.gif "Unity Editor Preview")

## Requirements

### Unity

- **Unity version**: 6000.0.62f1 (Unity 6)
- **Target platforms**: iOS (primary), Android

### Unity packages

- Google Cardboard XR Plugin
- TextMesh Pro
- [Easy Text Effects](https://github.com/LeiQiaoZhi/Easy-Text-Effects-for-Unity)

### Hardware (Optional)

- **Arduino-compatible board**
- **MPU9250** (9-axis IMU)
- **Google Cardboard-compatible headset** (for VR mode)

![Hardware](Documents/1.jpeg "IMU Overview")

### Software

- **Arduino IDE** (to upload the firmware)
- **Desktop controller**: .NET/Avalonia GUI app (requires the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0))

![Game Controller](Documents/3.png "Remote Controller")

## Quick start (Unity + desktop controller)

### 1. Run the Unity project

1. Open the project in Unity Hub (select the `ELEC4547` folder).
2. Open the main scene: `Assets/Scenes/VR.unity`
3. Press **Play**.
4. Note the **IP address** and **UDP port** shown in the in-game UI (the default port is commonly `5005`).

### 2. Flash the Arduino IMU cube (Optional)

1. Open `Arduino/Arduino.ino` in Arduino IDE.
2. Select your board and serial port, then upload.
3. Install any Arduino libraries required by the sketch (as referenced by `#include` lines).
4. Connect the MPU9250 over I2C (SDA/SCL) and power as required.
5. Validate output in Serial Monitor at **115200 baud** (should output quaternion/orientation data).

### 3. Run the desktop controller (UDP remote control)

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).
2. From a terminal:
   - `cd Controller`
   - `dotnet run`
3. In the app:
   - Enter the Unity instance **IP** and **port**
   - Click **Connect**
4. (Optional) Connect to the Arduino:
   - Click **Arduino Connect**
   - On success you should see a detected serial port (e.g. `COM4` on Windows or `/dev/cu.usbmodem*` on macOS)

> Tip: If Unity is running on a phone headset build, connect to the phone's **LAN IP** (not `localhost`). Ensure your PC and phone are on the same Wi‑Fi network and that your firewall allows UDP traffic.

## Networking / protocol notes

- **Transport**: UDP
- **Payload**: ASCII text commands
- **Command prefixes**:
  - `MAZE:` — generate/configure a maze
  - `CAM:` — camera configuration
  - `MOVE:` — movement input
  - `CALIBRATE` — sensor calibration
  - `RECENTER` — VR recenter

## Troubleshooting

- **Unity doesn't receive UDP commands**
  - Make sure the controller app is pointing at the Unity device's **LAN IP** (not `localhost`).
  - Confirm both devices are on the same network and that your firewall allows UDP on the configured port.
- **Arduino won't connect from the desktop controller**
  - Use a **data-capable** USB cable and confirm the sketch is uploaded.
  - Close any other program using the serial port (e.g. Arduino Serial Monitor).
- **Project opens but visuals differ from screenshots**
  - Some licensed assets were removed from the repo; replace missing materials/VFX with your own equivalents.

## Project structure (high level)

```
ELEC4547/
├── Assets/
│   ├── MazeGenerator/          # Core maze generation system
│   │   ├── Core/               # Base classes and utilities
│   │   ├── Cube/               # Cube maze implementation
│   │   ├── Flat/               # Flat/disc maze implementation
│   │   └── Scripts/            # Runtime generator component
│   ├── Scripts/                # Main game scripts
│   │   ├── GameController.cs   # Central game state manager
│   │   ├── PlayerController.cs # Camera positioning controller
│   │   ├── MazeController.cs   # Maze rotation/orientation handler
│   │   ├── NetworkManager.cs   # UDP networking layer
│   │   ├── CardboardController.cs # VR mode controller
│   │   └── GoalController.cs   # Goal detection and collision
│   ├── Scenes/
│   │   └── VR.unity            # Main VR scene
│   ├── Prefabs/                # Reusable game objects
│   ├── Materials/              # Material assets
│   ├── Sounds/                 # Audio assets
│   └── XR/                     # XR/VR configuration
├── Controller/                 # Desktop GUI app for remote control (.NET/Avalonia)
├── Arduino/                    # Arduino firmware for MPU9250
└── ProjectSettings/            # Unity project configuration
```
