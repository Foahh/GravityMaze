# MazeGenerator (Technical Notes)

This document describes the design and internals of the maze generation system located in `Assets/MazeGenerator/`.

## High-level architecture

There are two supported shapes:

- **Disc (flat grid)**: a classic 2D grid maze.
- **Cube**: 6 connected 2D grids (one per cube face) with topology that wraps across edges.

The subsystem is split into three concerns:

- **Data generation**: produce a graph/adjacency representation (walls open/closed).
- **Geometry building**: instantiate Unity primitives (walls/surfaces/lids) from maze data.
- **Gameplay placement**: choose start/goal cells and convert them into world positions/rotations.

## Entry points

### Runtime

- **`MazeGenerator.Scripts.DynamicMazeGenerator`**
  - Auto-generates in `Start()`.
  - Call `GenerateMaze()` to regenerate.
  - Exposes:
    - `CurrentMaze` (root `GameObject`)
    - `CurrentFlatData` / `CurrentCubeData`
    - `CurrentRandom`
  - Events:
    - `OnMazeGenerated(GameObject root)`
    - `OnMazeDestroyed()`

### Editor

- **`MazeGenerator.Editor.MazeGeneratorWindow`** (`Tools → Maze Generator`)
  - Uses `MazeGenerator.Core.MazeBuilder` to generate a maze in the editor with Undo support.

## Settings (parameters)

All generation/build options live in **`MazeGenerator.Core.MazeGenerationSettings`**:

- **Shape & size**
  - `mazeShape`: `Disc` or `Cube`
  - `gridSize`: cells per side (clamped to ≥ 2 at runtime)

- **Dimensions**
  - `cellSize`: world-space size of a cell (clamped to ≥ 0.1)
  - `wallHeight`: wall extrusion along the face normal (clamped to ≥ 0.1)
  - `wallThickness`: wall thickness (clamped to `[0.02, cellSize * 0.75]`)
  - `lidThickness`: (currently only used as a setting; builders implement lids via primitives)

- **Difficulty**
  - `deadEndRemoval` (`0..1`): post-process that opens additional passages to reduce dead-ends.
  - `goalDistanceRatio` (`0.5..1`): used by gameplay placement to choose the goal distance.

- **Seed**
  - `useRandomSeed`: when `true`, a new seed is created each generation
  - `seed`: deterministic seed when `useRandomSeed == false`

- **Materials**
  - `wallMaterial`, `groundMaterial`, `lidMaterial`

## Maze generation algorithm

Both disc and cube generators implement the same overall logic:

1. **Initialize all walls = closed**.
2. **Depth-first search (recursive backtracker)**:
   - Start at a chosen cell.
   - Repeatedly pick a random *unvisited* neighbor.
   - Carve a passage by opening the shared wall on both cells.
   - Backtrack when there are no unvisited neighbors.
3. **Optional dead-end removal**:
   - Identify dead-ends (cells with exactly one open passage).
   - Randomly open additional walls (in multiple passes) to reduce dead-ends.

This produces a *perfect maze* (single unique path between any two cells) when `deadEndRemoval = 0`, and a more “loopy/open” maze as the ratio increases.

### Disc data model

- **`FlatMazeData`** contains:
  - `Cells[x, y]` (2D array)
- Each `FlatCell` contains:
  - `Walls[Direction] : bool` where `true = wall exists`.

Generation: `MazeGenerator.Flat.FlatMazeGenerator.Generate(int size, Random rng, float deadEndRemoval)`

### Cube data model

- **`CubeMazeData`** contains:
  - `Cells[CubeCellKey]` (dictionary)
- `CubeCellKey` = `(CubeFace face, int x, int y)`.
- Each `CubeCell` contains:
  - `Walls[Direction] : bool` where `true = wall exists`.

Generation: `MazeGenerator.Cube.CubeMazeGenerator.Generate(int size, float cellSize, Random rng, float deadEndRemoval)`

## Cube topology (how faces connect)

The cube maze relies on **`MazeGenerator.Cube.CubeTopology`**:

- Each face has a **`FaceOrientation`** with:
  - `Normal` (points out of the cube)
  - `Up`
  - `Right = cross(Up, Normal)`

When moving off an edge of a face, `CubeTopology.TryGetNeighbor(...)` falls back to **edge traversal**:

- Compute the current cell’s edge center.
- Rotate around the edge axis by **-90°** to “fold” onto the adjacent face.
- Determine the neighbor face from the rotated normal.
- Project the rotated point onto the neighbor face’s `(Right, Up)` axes to get `(x, y)` indices.
- Determine the neighbor’s inbound direction so walls can be opened symmetrically.

## Geometry building (Unity objects)

There are two builders:

- **`MazeGenerator.Flat.FlatMazeBuilder`**
- **`MazeGenerator.Cube.CubeMazeBuilder`**

Both instantiate Unity **primitives** and use **tags** to categorize them:

- `MazeSurface`: walkable surfaces
- `MazeWall`: wall segments, joints, seam blocks
- `MazeLid`: lid visuals and lid colliders

Important implementation details:

- Visual surfaces are very thin cubes (thickness `0.001`) and have their colliders removed.
- “Lids” are:
  - A visible `Quad` (collider removed)
  - An oversized invisible `Cube` collider that acts as a containment ceiling
- Walls are cubes placed along cell edges; “joint” cubes are added at grid nodes to close cracks.
- Cube mazes also add seam blocks and corner blocks to bridge face junctions.

### Materials

Two mechanisms exist:

- Builders directly set materials on primitives when the relevant material is non-null.
- `MazeGenerator.Core.MaterialSetter` can be added to a parent and will apply `sharedMaterial` to all tagged renderers under it.

## Gameplay placement (ball/goal)

**`MazeGenerator.Scripts.MazeGameplayPlacer`** listens to `DynamicMazeGenerator.OnMazeGenerated` and places:

- **Disc**:
  - Finds diameter endpoints (approx. “hardest” start/goal) via BFS distances.
  - Uses `goalDistanceRatio` to select a goal cell around a target distance.

- **Cube**:
  - Uses the **top-face center** as a fixed start (`MazePathfinder.GetTopCenterCell`).
  - BFS over cube topology for distances.
  - Uses `goalDistanceRatio` to select a goal cell around a target distance.

World placement utilities are in **`MazeGenerator.Core.MazePlacementHelper`**:

- `GetFlatCellWorldPosition(...)`
- `GetCubeCellWorldPosition(...)`
- `GetCubeCellWorldRotation(...)`
