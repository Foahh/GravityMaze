## Unity Scripts Technical Notes (`Assets/Scripts`)

This document describes the Unity runtime scripts in `Assets/Scripts/` and how they fit together (game flow, maze rotation, camera framing, UDP networking, and VR/Cardboard).

## Script map (what each component does)

### `GameController`
**Role:** Central coordinator/state machine.

- **Responsibilities**
  - Owns the game state (`Idle/Playing/Finished/Paused`) and the run timer.
  - Subscribes to `NetworkManager` events and forwards actions to the correct subsystem.
  - Regenerates the maze via `MazeGenerator.Scripts.DynamicMazeGenerator` when a maze command is received.
  - Responds to `GoalController` trigger to finish the run (UI + SFX + particles).

- **Key references (Inspector)**
  - `dynamicMazeGenerator`: MazeGenerator component that actually builds the maze.
  - `networkManager`: UDP receiver/event source.
  - `mazeController`: Applies orientation to the maze transform.
  - `playerController`: Positions the camera to frame the maze; also applies remote move input.
  - `cardboardController`: VR entry/exit and recenter.
  - `goalController`, `goalRenderObject`, `goalBoomEffect`: Finish target logic + visuals.
  - `timerText` (TMP) and `finishUI`.

- **Notable behaviors**
  - **Maze regeneration:** on `MAZE:` command, it copies material fields from the current generator settings into the new settings (`PreserveMaterialSettings`) before calling `GenerateMaze()`, then calls `ResetGame()`.
  - **On maze generated:** calls `playerController.RepositionForMaze()`.
  - **Finish:** disables goal collider + goal visuals, plays particles, enables finish UI, plays audio.

### `NetworkManager`
**Role:** Pure UDP networking layer → raises Unity-safe events.

- **Responsibilities**
  - Runs a UDP receive loop on a background thread.
  - Parses payloads into either:
    - commands (calibrate, recenter, maze settings, camera settings, move), or
    - orientation quaternion samples.
  - Queues data into thread-safe pending state; dispatches events on the Unity main thread in `Update()`.
  - Tracks “connected” clients by `IPEndPoint` activity and prunes stale clients.
  - Displays local IPs and port on a TMP UI element.

- **Threading model (important)**
  - Background thread reads UDP packets and **only writes pending state**.
  - Unity main thread (`Update`) calls `ProcessPendingOperations()` to invoke events.

- **Default port**
  - `serverPort` defaults to **5005**.

### `MazeController`
**Role:** Applies sensor orientation to the maze transform.

- **Responsibilities**
  - Receives quaternion samples via `SetOrientation(Quaternion)`.
  - Applies axis remapping (invert/swap options) to match sensor conventions.
  - Applies calibration offset (`Calibrate()` makes current orientation “zero”).
  - Smooths rotation with exponential smoothing.
  - Optionally auto-levels back to identity if no data arrives for `dataTimeoutSeconds`.

### `PlayerController`
**Role:** Positions and orients the camera to frame the maze; supports small “pan” offsets.

- **Responsibilities**
  - Computes a distance based on maze size and shape (Cube vs Flat/Disc) from `dynamicMazeGenerator.Settings`.
  - Positions camera at a fixed vertical angle (`fixedAngle`) and horizontal bearing (`fixedHorizontalAngle`).
  - Smoothly eases position/rotation if enabled.
  - Applies remote 2D move input (`ApplyMoveInput(Vector2)`) as a small world-space offset.

- **Notes**
  - Movement is **camera offset only** (not a physics player). The actual player ball is separate.
  - The script warns and does nothing if `dynamicMazeGenerator` is not assigned.

### `GoalController`
**Role:** Detects reaching the goal.

- **Responsibilities**
  - On trigger enter with a collider tagged **`PlayerBall`**, invokes `OnGoalTriggered` exactly once.
  - `ResetGoal()` reenables triggering.
  - `SetGoalColliderEnabled(bool)` allows `GameController` to disable the goal after completion.

### `CardboardController`
**Role:** Toggle Cardboard XR and handle recenter.

- **Responsibilities**
  - Enters VR on initial touch if not already in XR.
  - Exits VR when Cardboard close button is pressed.
  - Recenter via `Recenter()` (also callable by network command through `GameController`).
  - Manages XR loader lifecycle using `UnityEngine.XR.Management`.

## Runtime data flow (high level)

```text
UDP client (controller app)  ──UDP──▶  NetworkManager (thread)
                                        │
                                        ▼
                              pending state queues/flags
                                        │  (Update)
                                        ▼
                         NetworkManager events on main thread
                                        │
                                        ▼
                                 GameController
                 ┌───────────────┼───────────────┬──────────────────┐
                 ▼               ▼               ▼                  ▼
           MazeController   PlayerController  DynamicMazeGenerator  CardboardController
           (rotate maze)   (frame camera)     (regenerate maze)    (XR/recenter)

Goal trigger (PlayerBall enters goal) ─▶ GoalController event ─▶ GameController finish flow
```

## UDP protocol (what to send)

`NetworkManager` treats incoming UDP payload as UTF-8 text and trims whitespace.

### Orientation packets (quaternion)
If the message is not a recognized command, it tries to parse a quaternion:

- **Format (exact):** `QW:<w>,QX:<x>,QY:<y>,QZ:<z>`
- **Example:** `QW:0.7071,QX:0.0,QY:0.7071,QZ:0.0`
- **Notes:**
  - Values are parsed using invariant culture (decimal dot).
  - Quaternion is normalized; invalid/NaN/inf are rejected.
  - Only the latest sample is kept (queue is cleared to avoid lag).

### Commands
Commands are recognized case-insensitively.

- **Calibrate:** `CALIBRATE`
  - Triggers `MazeController.Calibrate()` (via `GameController`).

- **VR Recenter:** `RECENTER`
  - Triggers `CardboardController.Recenter()` (via `GameController`).

- **Maze generation:** `MAZE:<payload>`
  - Payload is `key=value,key=value,...` parsed by `Networking/MazeSettingsParser.cs`.
  - **Supported keys:**
    - `shape` (`Cube` or anything else → `Disc`)
    - `gridsize` (int, clamped 2..20)
    - `cellsize` (float, clamped 0.5..3)
    - `wallheight` (float, clamped 0.5..3)
    - `wallthickness` (float, clamped 0.05..0.5)
    - `lidthickness` (float, clamped 0.05..0.5)
    - `userandomseed` (`True`/`False`)
    - `seed` (int)
    - `goaldistance` (0..100 interpreted as %, mapped into 0.5..1.0)
    - `deadendremoval` (0..100 interpreted as %, mapped into 0..1)
  - **Example:**
    - `MAZE:shape=Cube,gridsize=10,cellsize=1,wallheight=1,userandomseed=True,goaldistance=75,deadendremoval=20`

- **Move input:** `MOVE:<payload>`
  - Payload is any string containing `W`, `A`, `S`, `D` characters.
  - The vector is the sum of directions: `W=(0,+1)`, `S=(0,-1)`, `A=(-1,0)`, `D=(+1,0)`.
  - This calls `PlayerController.ApplyMoveInput(Vector2)`.
  - **Example:** `MOVE:WAD`

- **Camera settings:** `CAM:<payload>`
  - Payload format: `angle=<value>,distance=<value>`
  - Values clamped:
    - `angle`: 30..90 degrees
    - `distance`: 0.5..5 multiplier
  - **Example:** `CAM:angle=60,distance=1.5`

## Scene wiring checklist

- **`GameController` GameObject**
  - Must have an `AudioSource` (required by attribute).
  - Assign references for: `NetworkManager`, `MazeController`, `PlayerController`, `CardboardController`, `GoalController`, `timerText`, `finishUI`, and goal VFX objects.

- **Goal object**
  - Has `GoalController` + a `Collider` set as trigger.
  - The player ball collider must have the tag **`PlayerBall`**.

- **Maze object**
  - Has `DynamicMazeGenerator` (from `MazeGenerator.Scripts`) and `MazeController` on the transform you want to rotate.

- **IP UI**
  - Assign `NetworkManager.ipAddressTextTMP` to a TMP UI text to show `<ip>:<port>` lines.

## Common extension points

- **Add a new UDP command:**
  - Add a prefix/constant in `NetworkManager`, parse it in `TryHandleCommand`, and expose a new event.
  - Subscribe in `GameController.SubscribeToEvents()` and implement a handler.

- **Change axis mapping / calibration behavior:**
  - Edit `MazeController` inspector fields (invert/swap) and smoothing/timeout values.

- **Adjust camera framing behavior:**
  - Tune `PlayerController` fixed angles and `fixedDistanceMultiplier`.
  - If you want movement relative to camera facing, change how `_positionOffset` is computed in `ApplyMoveInput`.
