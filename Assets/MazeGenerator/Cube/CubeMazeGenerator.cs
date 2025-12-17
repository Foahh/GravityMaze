using System;
using System.Collections.Generic;
using MazeGenerator.Core;
using UnityEngine;
using Random = System.Random;

namespace MazeGenerator.Cube
{
    public enum CubeFace
    {
        Front = 0,
        Right = 1,
        Back = 2,
        Left = 3,
        Top = 4,
        Bottom = 5
    }

    public readonly struct CubeCellKey : IEquatable<CubeCellKey>
    {
        public CubeCellKey(CubeFace face, int x, int y)
        {
            Face = face;
            X = x;
            Y = y;
        }

        public CubeFace Face { get; }
        public int X { get; }
        public int Y { get; }

        public bool Equals(CubeCellKey other)
        {
            return Face == other.Face && X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is CubeCellKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)Face;
                hashCode = (hashCode * 397) ^ X;
                hashCode = (hashCode * 397) ^ Y;
                return hashCode;
            }
        }
    }

    public sealed class CubeMazeData
    {
        public CubeMazeData(int size)
        {
            Size = size;
            Cells = new Dictionary<CubeCellKey, CubeCell>(6 * size * size);
            foreach (CubeFace face in Enum.GetValues(typeof(CubeFace)))
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                    Cells.Add(new CubeCellKey(face, x, y), new CubeCell());
        }

        public int Size { get; }
        public Dictionary<CubeCellKey, CubeCell> Cells { get; }
    }

    public sealed class CubeCell
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

    public readonly struct CubeNeighbor
    {
        public CubeNeighbor(CubeCellKey neighbor, Direction fromDirection, Direction toDirection)
        {
            Neighbor = neighbor;
            FromDirection = fromDirection;
            ToDirection = toDirection;
        }

        public CubeCellKey Neighbor { get; }
        public Direction FromDirection { get; }
        public Direction ToDirection { get; }
    }

    public static class CubeMazeGenerator
    {
        public static CubeMazeData Generate(int size, float cellSize, Random rng, float deadEndRemoval = 0f)
        {
            var data = new CubeMazeData(size);
            var visited = new HashSet<CubeCellKey>();
            var stack = new Stack<CubeCellKey>();
            var start = new CubeCellKey(CubeFace.Front, 0, 0);
            stack.Push(start);
            visited.Add(start);

            while (stack.Count > 0)
            {
                var current = stack.Peek();
                var neighbors = CollectNeighbors(current, size, cellSize, visited);
                if (neighbors.Count == 0)
                {
                    stack.Pop();
                    continue;
                }

                var next = neighbors[rng.Next(neighbors.Count)];
                data.Cells[current].Walls[next.FromDirection] = false;
                data.Cells[next.Neighbor].Walls[next.ToDirection] = false;
                visited.Add(next.Neighbor);
                stack.Push(next.Neighbor);
            }

            // Apply dead-end removal if specified
            if (deadEndRemoval > 0f)
                RemoveDeadEnds(data, size, cellSize, rng, deadEndRemoval);

            return data;
        }

        /// <summary>
        /// Removes dead-ends by opening walls to adjacent cells.
        /// </summary>
        private static void RemoveDeadEnds(CubeMazeData data, int size, float cellSize, Random rng, float removalRatio)
        {
            var deadEnds = new List<CubeCellKey>();
            
            // Multiple passes to handle cascading dead-ends
            var maxPasses = Mathf.CeilToInt(removalRatio * 3);
            for (var pass = 0; pass < maxPasses; pass++)
            {
                deadEnds.Clear();
                
                // Collect all dead-ends
                foreach (var kvp in data.Cells)
                    if (kvp.Value.IsDeadEnd())
                        deadEnds.Add(kvp.Key);

                if (deadEnds.Count == 0) break;

                // Determine how many to remove this pass
                var toRemove = Mathf.CeilToInt(deadEnds.Count * removalRatio / maxPasses);
                
                // Shuffle dead-ends for random selection
                ShuffleList(deadEnds, rng);

                for (var i = 0; i < Mathf.Min(toRemove, deadEnds.Count); i++)
                {
                    RemoveOneWall(data, deadEnds[i], size, cellSize, rng);
                }
            }
        }

        /// <summary>
        /// Removes one wall from a dead-end cell, connecting it to an adjacent cell.
        /// </summary>
        private static void RemoveOneWall(CubeMazeData data, CubeCellKey cellKey, int size, float cellSize, Random rng)
        {
            if (!data.Cells.TryGetValue(cellKey, out var cell)) return;
            
            var candidates = new List<(Direction dir, CubeCellKey neighbor, Direction neighborDir)>();

            // Find all walls that could be removed (walls that have a valid neighbor)
            foreach (var direction in DirectionHelper.AllDirections)
            {
                if (!cell.Walls[direction]) continue; // Already open
                if (!CubeTopology.TryGetNeighbor(cellKey, direction, size, cellSize, out var neighbor, out var neighborDir)) 
                    continue;
                candidates.Add((direction, neighbor, neighborDir));
            }

            if (candidates.Count == 0) return;

            // Pick a random wall to remove
            var chosen = candidates[rng.Next(candidates.Count)];

            // Remove wall from both sides
            cell.Walls[chosen.dir] = false;
            if (data.Cells.TryGetValue(chosen.neighbor, out var neighborCell))
                neighborCell.Walls[chosen.neighborDir] = false;
        }

        private static void ShuffleList<T>(List<T> list, Random rng)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private static List<CubeNeighbor> CollectNeighbors(CubeCellKey cell, int size, float cellSize,
            HashSet<CubeCellKey> visited)
        {
            var result = new List<CubeNeighbor>(4);
            foreach (var direction in DirectionHelper.AllDirections)
            {
                if (!CubeTopology.TryGetNeighbor(cell, direction, size, cellSize, out var neighbor,
                        out var neighborDirection)) continue;

                if (visited.Contains(neighbor)) continue;

                result.Add(new CubeNeighbor(neighbor, direction, neighborDirection));
            }

            return result;
        }
    }
}