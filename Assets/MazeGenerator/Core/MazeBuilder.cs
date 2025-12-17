#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using MazeGenerator.Cube;
using MazeGenerator.Flat;
using UnityEngine;
using Random = System.Random;

namespace MazeGenerator.Core
{
    public static class MazeBuilder
    {
        public static void CreateDiscMaze(MazeGenerationSettings settings)
        {
            ValidateCommonSettings(settings);
            var rng = CreateRandom(settings, out var seedUsed);
            var mazeData = FlatMazeGenerator.Generate(settings.gridSize, rng, settings.deadEndRemoval);
            var root = new GameObject($"SquareMaze_{settings.gridSize}x{settings.gridSize}");
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(root, "Generate Square Maze");
#endif

            var surfaceRoot = new GameObject("Surface");
            surfaceRoot.transform.SetParent(root.transform, false);
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(surfaceRoot, "Generate Square Maze");
#endif

            var wallsRoot = new GameObject("Walls");
            wallsRoot.transform.SetParent(root.transform, false);
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(wallsRoot, "Generate Square Maze");
#endif

            var lidRoot = new GameObject("Lid");
            lidRoot.transform.SetParent(root.transform, false);
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(lidRoot, "Generate Square Maze");
#endif

            FlatMazeBuilder.BuildSquareSurface(surfaceRoot.transform, settings);
            FlatMazeBuilder.BuildSquareWalls(wallsRoot.transform, mazeData, settings);
            FlatMazeBuilder.BuildSquareLid(lidRoot.transform, settings);

            var wallMaterialSetter = wallsRoot.AddComponent<MaterialSetter>();
            wallMaterialSetter.ObjectTag = "MazeWall";
            if (settings.wallMaterial != null) wallMaterialSetter.SetMaterial(settings.wallMaterial);

            var surfaceMaterialSetter = surfaceRoot.AddComponent<MaterialSetter>();
            surfaceMaterialSetter.ObjectTag = "MazeSurface";
            if (settings.groundMaterial != null) surfaceMaterialSetter.SetMaterial(settings.groundMaterial);

            var lidMaterialSetter = lidRoot.AddComponent<MaterialSetter>();
            lidMaterialSetter.ObjectTag = "MazeLid";
            if (settings.lidMaterial != null) lidMaterialSetter.SetMaterial(settings.lidMaterial);

#if UNITY_EDITOR
            Selection.activeGameObject = root;
#endif
            Debug.Log($"Square disc maze generated with seed {seedUsed}.", root);
        }

        public static void CreateCubeMaze(MazeGenerationSettings settings)
        {
            ValidateCommonSettings(settings);
            var rng = CreateRandom(settings, out var seedUsed);
            var mazeData = CubeMazeGenerator.Generate(settings.gridSize, settings.cellSize, rng, settings.deadEndRemoval);
            var root = new GameObject($"CubeMaze_{settings.gridSize}");
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(root, "Generate Cube Maze");
#endif

            var surfacesRoot = new GameObject("Surfaces");
            surfacesRoot.transform.SetParent(root.transform, false);
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(surfacesRoot, "Generate Cube Maze");
#endif

            var wallsRoot = new GameObject("Walls");
            wallsRoot.transform.SetParent(root.transform, false);
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(wallsRoot, "Generate Cube Maze");
#endif

            var lidRoot = new GameObject("Lid");
            lidRoot.transform.SetParent(root.transform, false);
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(lidRoot, "Generate Cube Maze");
#endif

            CubeMazeBuilder.BuildCubeSurfaces(surfacesRoot.transform, settings);
            CubeMazeBuilder.BuildCubeWalls(wallsRoot.transform, mazeData, settings);
            CubeMazeBuilder.BuildCubeLid(lidRoot.transform, settings);

            var wallMaterialSetter = wallsRoot.AddComponent<MaterialSetter>();
            wallMaterialSetter.ObjectTag = "MazeWall";
            if (settings.wallMaterial != null) wallMaterialSetter.SetMaterial(settings.wallMaterial);

            var surfaceMaterialSetter = surfacesRoot.AddComponent<MaterialSetter>();
            surfaceMaterialSetter.ObjectTag = "MazeSurface";
            if (settings.groundMaterial != null) surfaceMaterialSetter.SetMaterial(settings.groundMaterial);

            var lidMaterialSetter = lidRoot.AddComponent<MaterialSetter>();
            lidMaterialSetter.ObjectTag = "MazeLid";
            if (settings.lidMaterial != null) lidMaterialSetter.SetMaterial(settings.lidMaterial);

#if UNITY_EDITOR
            Selection.activeGameObject = root;
#endif
            Debug.Log($"Cube maze generated with seed {seedUsed}.", root);
        }

        private static void ValidateCommonSettings(MazeGenerationSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            settings.gridSize = Mathf.Max(2, settings.gridSize);
            settings.cellSize = Mathf.Max(0.1f, settings.cellSize);
            settings.wallHeight = Mathf.Max(0.1f, settings.wallHeight);
            settings.wallThickness = Mathf.Clamp(settings.wallThickness, 0.02f, settings.cellSize * 0.75f);
        }

        private static Random CreateRandom(MazeGenerationSettings settings, out int seedUsed)
        {
            if (settings.useRandomSeed)
                seedUsed = Guid.NewGuid().GetHashCode();
            else
                seedUsed = settings.seed;

            return new Random(seedUsed);
        }
    }
}