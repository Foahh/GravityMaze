using System.Collections.Generic;
using MazeGenerator.Core;
using MazeGenerator.Cube;
using UnityEngine;
using UnityEngine.Serialization;
using Random = System.Random;

namespace MazeGenerator.Scripts
{
    /// <summary>
    ///     Handles placement of gameplay objects (ball, goal) when a maze is generated.
    ///     Attach this to the same GameObject as MazeGenerator or reference it.
    /// </summary>
    public class MazeGameplayPlacer : MonoBehaviour
    {
        #region Serialized Fields

        [FormerlySerializedAs("mazeGenerator")]
        [Header("Maze Generator Reference")]
        [SerializeField] private DynamicMazeGenerator dynamicMazeGenerator;

        [Header("Gameplay References")]
        [SerializeField] private Transform playerBall;
        [SerializeField] private Transform playerGoal;

        [Header("Placement Offsets")]
        [SerializeField][Min(0f)] private float ballHeightOffset = 0.5f;
        [SerializeField][Min(0f)] private float goalHeightOffset = 0.5f;

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            if (dynamicMazeGenerator == null)
                dynamicMazeGenerator = GetComponent<DynamicMazeGenerator>();

            if (dynamicMazeGenerator != null)
                dynamicMazeGenerator.OnMazeGenerated += HandleDynamicMazeGenerated;
        }

        private void OnDisable()
        {
            if (dynamicMazeGenerator != null)
                dynamicMazeGenerator.OnMazeGenerated -= HandleDynamicMazeGenerated;
        }

        #endregion

        #region Public Methods

        /// <summary>
        ///     Manually trigger placement using current maze data.
        /// </summary>
        public void PlaceObjects()
        {
            if (dynamicMazeGenerator?.CurrentMaze == null)
            {
                Debug.LogWarning("[MazeGameplayPlacer] No maze available for placement.");
                return;
            }

            HandleDynamicMazeGenerated(dynamicMazeGenerator.CurrentMaze);
        }

        #endregion

        #region Event Handlers

        private void HandleDynamicMazeGenerated(GameObject mazeRoot)
        {
            if (dynamicMazeGenerator == null) return;

            var settings = dynamicMazeGenerator.Settings;
            var rng = dynamicMazeGenerator.CurrentRandom ?? new Random();

            switch (settings.mazeShape)
            {
                case MazeShape.Disc:
                    PlaceFlatGameplayObjects(mazeRoot.transform, settings, rng);
                    break;
                case MazeShape.Cube:
                    PlaceCubeGameplayObjects(mazeRoot.transform, settings, rng);
                    break;
                default:
                    Debug.LogWarning("[MazeGameplayPlacer] Unsupported maze shape for placement.");
                    break;
            }
        }

        #endregion

        #region Flat Maze Placement

        private void PlaceFlatGameplayObjects(Transform mazeRoot, MazeGenerationSettings settings, Random rng)
        {
            var mazeData = dynamicMazeGenerator.CurrentFlatData;
            if (mazeData == null)
            {
                Debug.LogWarning("[MazeGameplayPlacer] Flat maze data missing; cannot place objects.");
                return;
            }

            var ballTransform = ResolvePlayerBall();
            var goalTransform = ResolvePlayerGoal();

            var (startCell, goalCell, distance) = FindFlatEndpointsWithDistanceRatio(
                mazeData, settings.gridSize, settings.goalDistanceRatio, rng);

            if (ballTransform != null)
            {
                var ballPosition = MazePlacementHelper.GetFlatCellWorldPosition(
                    settings, startCell, mazeRoot, ballHeightOffset);
                MoveBall(ballTransform, ballPosition);
            }
            else
            {
                Debug.LogWarning("[MazeGameplayPlacer] PlayerBall reference not set; skipping player placement.");
            }

            if (goalTransform != null)
            {
                var goalPosition = MazePlacementHelper.GetFlatCellWorldPosition(
                    settings, goalCell, mazeRoot, goalHeightOffset);
                MoveGoal(goalTransform, goalPosition, mazeRoot.rotation);
            }
            else
            {
                Debug.LogWarning("[MazeGameplayPlacer] PlayerGoal reference not set; skipping goal placement.");
            }

            Debug.Log(
                $"[MazeGameplayPlacer] Flat maze path length {distance} (ratio: {settings.goalDistanceRatio:P0}) between cells {startCell} and {goalCell}.");
        }

        /// <summary>
        /// Finds start and goal positions based on goalDistanceRatio.
        /// </summary>
        private static (Vector2Int start, Vector2Int goal, int distance) FindFlatEndpointsWithDistanceRatio(
            Flat.FlatMazeData data, int gridSize, float goalDistanceRatio, Random rng)
        {
            // First, find the diameter endpoints (maximum possible distance)
            var (diameterStart, diameterEnd, maxDistance) = MazePathfinder.FindFlatDiameterEndpoints(data, gridSize, rng);
            
            // If ratio is 1.0 or very close, use full diameter
            if (goalDistanceRatio >= 0.99f)
                return (diameterStart, diameterEnd, maxDistance);

            // Calculate target distance based on ratio
            var targetDistance = Mathf.Max(1, Mathf.RoundToInt(maxDistance * goalDistanceRatio));
            
            // Compute distances from start cell
            var distances = MazePathfinder.ComputeFlatDistances(diameterStart, data, gridSize);
            
            // Collect all cells within target distance range (with small tolerance)
            var candidates = new List<Vector2Int>();
            var tolerance = Mathf.Max(1, targetDistance / 5); // 20% tolerance
            
            for (var y = 0; y < gridSize; y++)
            for (var x = 0; x < gridSize; x++)
            {
                var dist = distances[x, y];
                if (dist >= targetDistance - tolerance && dist <= targetDistance + tolerance)
                    candidates.Add(new Vector2Int(x, y));
            }

            // If no candidates in range, fall back to closest available
            if (candidates.Count == 0)
            {
                var bestDiff = int.MaxValue;
                for (var y = 0; y < gridSize; y++)
                for (var x = 0; x < gridSize; x++)
                {
                    var dist = distances[x, y];
                    if (dist < 0) continue;
                    var diff = Mathf.Abs(dist - targetDistance);
                    if (diff < bestDiff)
                    {
                        bestDiff = diff;
                        candidates.Clear();
                        candidates.Add(new Vector2Int(x, y));
                    }
                    else if (diff == bestDiff)
                    {
                        candidates.Add(new Vector2Int(x, y));
                    }
                }
            }

            if (candidates.Count == 0)
                return (diameterStart, diameterEnd, maxDistance);

            var goalCell = candidates[rng.Next(candidates.Count)];
            var actualDistance = distances[goalCell.x, goalCell.y];
            
            return (diameterStart, goalCell, actualDistance);
        }

        #endregion

        #region Cube Maze Placement

        private void PlaceCubeGameplayObjects(Transform mazeRoot, MazeGenerationSettings settings, Random rng)
        {
            var mazeData = dynamicMazeGenerator.CurrentCubeData;
            if (mazeData == null)
            {
                Debug.LogWarning("[MazeGameplayPlacer] Cube maze data missing; cannot place objects.");
                return;
            }

            var ballTransform = ResolvePlayerBall();
            var goalTransform = ResolvePlayerGoal();
            var startCell = MazePathfinder.GetTopCenterCell(settings.gridSize);

            var distances = MazePathfinder.ComputeCubeDistances(startCell, mazeData, settings);
            var (goalCell, actualDistance) = FindCubeGoalWithDistanceRatio(distances, settings.goalDistanceRatio, rng);

            if (ballTransform != null)
            {
                var ballPosition = MazePlacementHelper.GetCubeCellWorldPosition(
                    settings, startCell, mazeRoot, ballHeightOffset);
                MoveBall(ballTransform, ballPosition);
            }
            else
            {
                Debug.LogWarning("[MazeGameplayPlacer] PlayerBall reference not set; skipping player placement.");
            }

            if (goalTransform != null)
            {
                var goalPosition = MazePlacementHelper.GetCubeCellWorldPosition(
                    settings, goalCell, mazeRoot, goalHeightOffset);
                var goalRotation = MazePlacementHelper.GetCubeCellWorldRotation(goalCell, mazeRoot);
                MoveGoal(goalTransform, goalPosition, goalRotation);
            }
            else
            {
                Debug.LogWarning("[MazeGameplayPlacer] PlayerGoal reference not set; skipping goal placement.");
            }

            Debug.Log(
                $"[MazeGameplayPlacer] Cube maze path length {actualDistance} (ratio: {settings.goalDistanceRatio:P0}) from top center to {goalCell.Face} ({goalCell.X}, {goalCell.Y}).");
        }

        /// <summary>
        /// Finds a goal cell based on goalDistanceRatio.
        /// </summary>
        private static (CubeCellKey goal, int distance) FindCubeGoalWithDistanceRatio(
            Dictionary<CubeCellKey, int> distances, float goalDistanceRatio, Random rng)
        {
            // Find max distance first
            var farthestCells = new List<CubeCellKey>();
            var maxDistance = MazePathfinder.CollectMaxDistanceCells(distances, farthestCells);

            // If ratio is 1.0 or very close, use max distance cells
            if (goalDistanceRatio >= 0.99f)
            {
                if (farthestCells.Count == 0)
                    return (new CubeCellKey(CubeFace.Bottom, 0, 0), 0);
                return (farthestCells[rng.Next(farthestCells.Count)], maxDistance);
            }

            // Calculate target distance based on ratio
            var targetDistance = Mathf.Max(1, Mathf.RoundToInt(maxDistance * goalDistanceRatio));
            var tolerance = Mathf.Max(1, targetDistance / 5); // 20% tolerance

            // Collect all cells within target distance range
            var candidates = new List<CubeCellKey>();
            foreach (var kvp in distances)
            {
                if (kvp.Value >= targetDistance - tolerance && kvp.Value <= targetDistance + tolerance)
                    candidates.Add(kvp.Key);
            }

            // If no candidates in range, find closest available
            if (candidates.Count == 0)
            {
                var bestDiff = int.MaxValue;
                foreach (var kvp in distances)
                {
                    var diff = Mathf.Abs(kvp.Value - targetDistance);
                    if (diff < bestDiff)
                    {
                        bestDiff = diff;
                        candidates.Clear();
                        candidates.Add(kvp.Key);
                    }
                    else if (diff == bestDiff)
                    {
                        candidates.Add(kvp.Key);
                    }
                }
            }

            if (candidates.Count == 0)
            {
                if (farthestCells.Count == 0)
                    return (new CubeCellKey(CubeFace.Bottom, 0, 0), 0);
                return (farthestCells[rng.Next(farthestCells.Count)], maxDistance);
            }

            var goalCell = candidates[rng.Next(candidates.Count)];
            var actualDistance = distances[goalCell];
            
            return (goalCell, actualDistance);
        }

        #endregion

        #region Object Resolution

        private Transform ResolvePlayerBall()
        {
            if (playerBall != null) return playerBall;

            var found = GameObject.FindGameObjectWithTag("PlayerBall");
            if (found != null)
                playerBall = found.transform;

            return playerBall;
        }

        private Transform ResolvePlayerGoal()
        {
            if (playerGoal != null) return playerGoal;

            var found = GameObject.FindGameObjectWithTag("PlayerGoal");
            if (found != null)
                playerGoal = found.transform;

            return playerGoal;
        }

        #endregion

        #region Object Movement

        private void MoveBall(Transform target, Vector3 worldPosition)
        {
            if (target == null) return;

            if (!target.TryGetComponent<Rigidbody>(out var rigidbody))
            {
                target.position = worldPosition;
                return;
            }

            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
            rigidbody.position = worldPosition;
            rigidbody.WakeUp();
        }

        private static void MoveGoal(Transform target, Vector3 worldPosition, Quaternion worldRotation)
        {
            if (target == null) return;
            target.SetPositionAndRotation(worldPosition, worldRotation);
        }

        #endregion
    }
}
