using System.Collections.Generic;
using MazeGenerator.Cube;
using MazeGenerator.Flat;
using UnityEngine;
using Random = System.Random;

namespace MazeGenerator.Core
{
    /// <summary>
    ///     Provides pathfinding utilities for maze distance calculations.
    /// </summary>
    public static class MazePathfinder
    {
        #region Flat Maze Pathfinding

        /// <summary>
        ///     Computes distances from a starting cell to all reachable cells in a flat maze.
        /// </summary>
        public static int[,] ComputeFlatDistances(Vector2Int start, FlatMazeData data, int gridSize)
        {
            var distances = InitializeDistanceArray(gridSize);
            var queue = new Queue<Vector2Int>();

            var clampedStart = ClampToGrid(start, gridSize);
            distances[clampedStart.x, clampedStart.y] = 0;
            queue.Enqueue(clampedStart);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var currentDistance = distances[current.x, current.y];

                foreach (var neighbor in GetFlatNeighbors(current, data, gridSize))
                {
                    if (distances[neighbor.x, neighbor.y] >= 0) continue;

                    distances[neighbor.x, neighbor.y] = currentDistance + 1;
                    queue.Enqueue(neighbor);
                }
            }

            return distances;
        }

        /// <summary>
        ///     Finds the diameter endpoints of a flat maze (two cells with maximum path distance).
        /// </summary>
        public static (Vector2Int start, Vector2Int end, int distance) FindFlatDiameterEndpoints(
            FlatMazeData data, int gridSize, Random rng)
        {
            var buffer = new List<Vector2Int>();

            // First pass: find farthest cell from origin
            var distancesFromOrigin = ComputeFlatDistances(Vector2Int.zero, data, gridSize);
            var originMax = CollectMaxDistanceCells(distancesFromOrigin, gridSize, buffer);

            if (buffer.Count == 0)
                return (Vector2Int.zero, Vector2Int.zero, originMax);

            var firstEndpoint = buffer[rng.Next(buffer.Count)];

            // Second pass: find farthest cell from first endpoint
            var distancesFromFirst = ComputeFlatDistances(firstEndpoint, data, gridSize);
            var diameterMax = CollectMaxDistanceCells(distancesFromFirst, gridSize, buffer);

            if (buffer.Count == 0)
                return (firstEndpoint, firstEndpoint, diameterMax);

            var secondEndpoint = buffer[rng.Next(buffer.Count)];

            // Randomly swap to vary start/end positions
            if (rng.Next(2) == 0)
                return (firstEndpoint, secondEndpoint, diameterMax);

            return (secondEndpoint, firstEndpoint, diameterMax);
        }

        /// <summary>
        ///     Collects all cells at maximum distance and returns the maximum distance value.
        /// </summary>
        public static int CollectMaxDistanceCells(int[,] distances, int gridSize, List<Vector2Int> buffer)
        {
            buffer.Clear();
            var maxDistance = -1;

            for (var y = 0; y < gridSize; y++)
            for (var x = 0; x < gridSize; x++)
            {
                var value = distances[x, y];
                if (value < 0) continue;

                if (value > maxDistance)
                {
                    maxDistance = value;
                    buffer.Clear();
                    buffer.Add(new Vector2Int(x, y));
                }
                else if (value == maxDistance)
                {
                    buffer.Add(new Vector2Int(x, y));
                }
            }

            return maxDistance;
        }

        private static int[,] InitializeDistanceArray(int gridSize)
        {
            var distances = new int[gridSize, gridSize];

            for (var y = 0; y < gridSize; y++)
            for (var x = 0; x < gridSize; x++)
                distances[x, y] = -1;

            return distances;
        }

        private static Vector2Int ClampToGrid(Vector2Int cell, int gridSize)
        {
            return new Vector2Int(
                Mathf.Clamp(cell.x, 0, gridSize - 1),
                Mathf.Clamp(cell.y, 0, gridSize - 1));
        }

        private static IEnumerable<Vector2Int> GetFlatNeighbors(Vector2Int cell, FlatMazeData data, int gridSize)
        {
            foreach (var direction in DirectionHelper.AllDirections)
            {
                if (data.Cells[cell.x, cell.y].Walls[direction]) continue;

                if (!FlatMazeBuilder.TryGetFlatNeighbor(cell.x, cell.y, direction, gridSize, out var nx, out var ny))
                    continue;

                yield return new Vector2Int(nx, ny);
            }
        }

        #endregion

        #region Cube Maze Pathfinding

        /// <summary>
        ///     Computes distances from a starting cell to all reachable cells in a cube maze.
        /// </summary>
        public static Dictionary<CubeCellKey, int> ComputeCubeDistances(
            CubeCellKey start, CubeMazeData data, MazeGenerationSettings settings)
        {
            var distances = new Dictionary<CubeCellKey, int>(data.Cells.Count);
            var queue = new Queue<CubeCellKey>();

            distances[start] = 0;
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var currentDistance = distances[current];

                foreach (var neighbor in GetCubeNeighbors(current, data, settings))
                {
                    if (distances.ContainsKey(neighbor)) continue;

                    distances[neighbor] = currentDistance + 1;
                    queue.Enqueue(neighbor);
                }
            }

            return distances;
        }

        /// <summary>
        ///     Collects all cells at maximum distance and returns the maximum distance value.
        /// </summary>
        public static int CollectMaxDistanceCells(Dictionary<CubeCellKey, int> distances, List<CubeCellKey> buffer)
        {
            buffer.Clear();
            var maxDistance = -1;

            foreach (var pair in distances)
                if (pair.Value > maxDistance)
                {
                    maxDistance = pair.Value;
                    buffer.Clear();
                    buffer.Add(pair.Key);
                }
                else if (pair.Value == maxDistance)
                {
                    buffer.Add(pair.Key);
                }

            return maxDistance;
        }

        /// <summary>
        ///     Gets the center cell of the top face of a cube maze.
        /// </summary>
        public static CubeCellKey GetTopCenterCell(int gridSize)
        {
            var centerIndex = Mathf.Clamp(gridSize / 2, 0, gridSize - 1);
            return new CubeCellKey(CubeFace.Top, centerIndex, centerIndex);
        }

        private static IEnumerable<CubeCellKey> GetCubeNeighbors(
            CubeCellKey cell, CubeMazeData data, MazeGenerationSettings settings)
        {
            if (!data.Cells.TryGetValue(cell, out var cellData))
                yield break;

            foreach (var direction in DirectionHelper.AllDirections)
            {
                if (cellData.Walls[direction]) continue;

                if (!CubeTopology.TryGetNeighbor(cell, direction, settings.gridSize, settings.cellSize,
                        out var neighbor, out _))
                    continue;

                yield return neighbor;
            }
        }

        #endregion
    }
}