using System.Collections.Generic;
using MazeGenerator.Core;
using UnityEngine;
using Random = System.Random;

namespace MazeGenerator.Flat
{
    public sealed class FlatMazeData
    {
        public FlatMazeData(int size)
        {
            Size = size;
            Cells = new FlatCell[size, size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                Cells[x, y] = new FlatCell();
        }

        public int Size { get; }
        public FlatCell[,] Cells { get; }
    }

    public sealed class FlatCell
    {
        public Dictionary<Direction, bool> Walls { get; } = new()
        {
            { Direction.North, true },
            { Direction.East, true },
            { Direction.South, true },
            { Direction.West, true }
        };
        
        /// <summary>
        /// Returns true if this cell is a dead-end (exactly one open passage).
        /// </summary>
        public bool IsDeadEnd()
        {
            var wallCount = 0;
            foreach (var wall in Walls.Values)
                if (wall) wallCount++;
            return wallCount == 3;
        }
    }

    public readonly struct FlatNeighbor
    {
        public FlatNeighbor(Vector2Int position, Direction fromDirection, Direction toDirection)
        {
            Position = position;
            FromDirection = fromDirection;
            ToDirection = toDirection;
        }

        public Vector2Int Position { get; }
        public Direction FromDirection { get; }
        public Direction ToDirection { get; }
    }

    public static class FlatMazeGenerator
    {
        public static FlatMazeData Generate(int size, Random rng, float deadEndRemoval = 0f)
        {
            var data = new FlatMazeData(size);
            var visited = new bool[size, size];
            var stack = new Stack<Vector2Int>();
            var start = new Vector2Int(0, 0);
            stack.Push(start);
            visited[start.x, start.y] = true;

            while (stack.Count > 0)
            {
                var current = stack.Peek();
                var neighbors = CollectNeighbors(current.x, current.y, size, visited);
                if (neighbors.Count == 0)
                {
                    stack.Pop();
                    continue;
                }

                var next = neighbors[rng.Next(neighbors.Count)];
                data.Cells[current.x, current.y].Walls[next.FromDirection] = false;
                data.Cells[next.Position.x, next.Position.y].Walls[next.ToDirection] = false;
                visited[next.Position.x, next.Position.y] = true;
                stack.Push(next.Position);
            }

            // Apply dead-end removal if specified
            if (deadEndRemoval > 0f)
                RemoveDeadEnds(data, size, rng, deadEndRemoval);

            return data;
        }

        /// <summary>
        /// Removes dead-ends by opening walls to adjacent cells.
        /// </summary>
        private static void RemoveDeadEnds(FlatMazeData data, int size, Random rng, float removalRatio)
        {
            var deadEnds = new List<Vector2Int>();
            
            // Multiple passes to handle cascading dead-ends
            var maxPasses = Mathf.CeilToInt(removalRatio * 3);
            for (var pass = 0; pass < maxPasses; pass++)
            {
                deadEnds.Clear();
                
                // Collect all dead-ends
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                    if (data.Cells[x, y].IsDeadEnd())
                        deadEnds.Add(new Vector2Int(x, y));

                if (deadEnds.Count == 0) break;

                // Determine how many to remove this pass
                var toRemove = Mathf.CeilToInt(deadEnds.Count * removalRatio / maxPasses);
                
                // Shuffle dead-ends for random selection
                ShuffleList(deadEnds, rng);

                for (var i = 0; i < Mathf.Min(toRemove, deadEnds.Count); i++)
                {
                    var cell = deadEnds[i];
                    RemoveOneWall(data, cell.x, cell.y, size, rng);
                }
            }
        }

        /// <summary>
        /// Removes one wall from a dead-end cell, connecting it to an adjacent cell.
        /// </summary>
        private static void RemoveOneWall(FlatMazeData data, int x, int y, int size, Random rng)
        {
            var cell = data.Cells[x, y];
            var candidates = new List<Direction>();

            // Find all walls that could be removed (walls that have a valid neighbor)
            foreach (var direction in DirectionHelper.AllDirections)
            {
                if (!cell.Walls[direction]) continue; // Already open
                if (!FlatMazeBuilder.TryGetFlatNeighbor(x, y, direction, size, out _, out _)) continue;
                candidates.Add(direction);
            }

            if (candidates.Count == 0) return;

            // Pick a random wall to remove
            var chosenDirection = candidates[rng.Next(candidates.Count)];
            FlatMazeBuilder.TryGetFlatNeighbor(x, y, chosenDirection, size, out var nx, out var ny);

            // Remove wall from both sides
            cell.Walls[chosenDirection] = false;
            data.Cells[nx, ny].Walls[DirectionHelper.Opposite(chosenDirection)] = false;
        }

        private static void ShuffleList<T>(List<T> list, Random rng)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private static List<FlatNeighbor> CollectNeighbors(int x, int y, int size, bool[,] visited)
        {
            var result = new List<FlatNeighbor>(4);
            foreach (var direction in DirectionHelper.AllDirections)
            {
                if (!FlatMazeBuilder.TryGetFlatNeighbor(x, y, direction, size, out var nx, out var ny)) continue;

                if (visited[nx, ny]) continue;

                result.Add(new FlatNeighbor(new Vector2Int(nx, ny), direction, DirectionHelper.Opposite(direction)));
            }

            return result;
        }
    }
}