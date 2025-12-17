using System;
using MazeGenerator.Core;
using MazeGenerator.Cube;
using MazeGenerator.Flat;
using UnityEngine;
using Random = System.Random;

namespace MazeGenerator.Scripts
{
    /// <summary>
    ///     Runtime maze generator component that can dynamically create and regenerate mazes during gameplay.
    ///     Attach this to a GameObject in your scene to generate mazes at runtime.
    /// </summary>
    public class DynamicMazeGenerator : MonoBehaviour
    {
        #region Unity Lifecycle

        private void Start()
        {
            GenerateMaze();
        }

        #endregion

        #region Serialized Fields

        [SerializeField] private MazeGenerationSettings settings = new();

        #endregion

        #region Public Properties

        /// <summary>
        ///     Reference to the currently generated maze root GameObject.
        /// </summary>
        public GameObject CurrentMaze { get; private set; }

        /// <summary>
        ///     The current maze generation settings.
        /// </summary>
        public MazeGenerationSettings Settings
        {
            get => settings;
            set => settings = value;
        }

        /// <summary>
        ///     The flat maze data from the last generation (null if cube maze or not yet generated).
        /// </summary>
        public FlatMazeData CurrentFlatData { get; private set; }

        /// <summary>
        ///     The cube maze data from the last generation (null if flat maze or not yet generated).
        /// </summary>
        public CubeMazeData CurrentCubeData { get; private set; }

        /// <summary>
        ///     The random number generator used for the current maze.
        /// </summary>
        public Random CurrentRandom { get; private set; }

        #endregion

        #region Events

        /// <summary>
        ///     Event fired when a maze is generated. Passes the maze root GameObject.
        /// </summary>
        public event Action<GameObject> OnMazeGenerated;

        /// <summary>
        ///     Event fired when a maze is destroyed.
        /// </summary>
        public event Action OnMazeDestroyed;

        #endregion

        #region Public Methods

        /// <summary>
        ///     Generates a new maze using the current settings.
        /// </summary>
        public void GenerateMaze()
        {
            DestroyMaze();
            ValidateSettings();

            CurrentRandom = CreateRandom(out var seedUsed);
            var root = CreateMaze(CurrentRandom);

            root.transform.SetParent(transform, false);
            CurrentMaze = root;

            Debug.Log($"[MazeGenerator] Maze generated with seed {seedUsed}.", root);

            OnMazeGenerated?.Invoke(root);
        }

        /// <summary>
        ///     Destroys the currently generated maze.
        /// </summary>
        public void DestroyMaze()
        {
            if (CurrentMaze == null) return;

            if (Application.isPlaying)
                Destroy(CurrentMaze);
            else
                DestroyImmediate(CurrentMaze);

            CurrentMaze = null;
            CurrentFlatData = null;
            CurrentCubeData = null;
            CurrentRandom = null;
            OnMazeDestroyed?.Invoke();
        }

        #endregion

        #region Maze Creation

        private void ValidateSettings()
        {
            settings.gridSize = Mathf.Max(2, settings.gridSize);
            settings.cellSize = Mathf.Max(0.1f, settings.cellSize);
            settings.wallHeight = Mathf.Max(0.1f, settings.wallHeight);
            settings.wallThickness = Mathf.Clamp(settings.wallThickness, 0.02f, settings.cellSize * 0.75f);
        }

        private Random CreateRandom(out int seedUsed)
        {
            seedUsed = settings.useRandomSeed
                ? Guid.NewGuid().GetHashCode()
                : settings.seed;

            return new Random(seedUsed);
        }

        private GameObject CreateMaze(Random rng)
        {
            return settings.mazeShape switch
            {
                MazeShape.Disc => CreateDiscMaze(rng),
                MazeShape.Cube => CreateCubeMaze(rng),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private GameObject CreateDiscMaze(Random rng)
        {
            CurrentFlatData = FlatMazeGenerator.Generate(settings.gridSize, rng, settings.deadEndRemoval);
            CurrentCubeData = null;

            var root = new GameObject($"SquareMaze_{settings.gridSize}x{settings.gridSize}");

            var surfaceRoot = CreateChildObject(root.transform, "Surface");
            var wallsRoot = CreateChildObject(root.transform, "Walls");
            var lidRoot = CreateChildObject(root.transform, "Lid");

            FlatMazeBuilder.BuildSquareSurface(surfaceRoot.transform, settings);
            FlatMazeBuilder.BuildSquareWalls(wallsRoot.transform, CurrentFlatData, settings);
            FlatMazeBuilder.BuildSquareLid(lidRoot.transform, settings);

            ApplyMaterials(wallsRoot, "MazeWall", settings.wallMaterial);
            ApplyMaterials(surfaceRoot, "MazeSurface", settings.groundMaterial);
            ApplyMaterials(lidRoot, "MazeLid", settings.lidMaterial);

            return root;
        }

        private GameObject CreateCubeMaze(Random rng)
        {
            CurrentCubeData = CubeMazeGenerator.Generate(settings.gridSize, settings.cellSize, rng, settings.deadEndRemoval);
            CurrentFlatData = null;

            var root = new GameObject($"CubeMaze_{settings.gridSize}");

            var surfacesRoot = CreateChildObject(root.transform, "Surfaces");
            var wallsRoot = CreateChildObject(root.transform, "Walls");
            var lidRoot = CreateChildObject(root.transform, "Lid");

            CubeMazeBuilder.BuildCubeSurfaces(surfacesRoot.transform, settings);
            CubeMazeBuilder.BuildCubeWalls(wallsRoot.transform, CurrentCubeData, settings);
            CubeMazeBuilder.BuildCubeLid(lidRoot.transform, settings);

            ApplyMaterials(wallsRoot, "MazeWall", settings.wallMaterial);
            ApplyMaterials(surfacesRoot, "MazeSurface", settings.groundMaterial);
            ApplyMaterials(lidRoot, "MazeLid", settings.lidMaterial);

            return root;
        }

        private static GameObject CreateChildObject(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static void ApplyMaterials(GameObject parent, string tag, Material material)
        {
            var setter = parent.AddComponent<MaterialSetter>();
            setter.ObjectTag = tag;

            if (material != null)
                setter.SetMaterial(material);
        }

        #endregion
    }
}
