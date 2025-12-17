using System.Collections.Generic;
using MazeGenerator.Core;
using UnityEngine;

namespace MazeGenerator.Cube
{
    public readonly struct FaceOrientation
    {
        public FaceOrientation(Vector3 normal, Vector3 up)
        {
            Normal = normal.normalized;
            Up = up.normalized;
            Right = Vector3.Cross(Up, Normal).normalized;
        }

        public Vector3 Normal { get; }
        public Vector3 Up { get; }
        public Vector3 Right { get; }
    }

    public static class CubeTopology
    {
        private static readonly Dictionary<CubeFace, FaceOrientation> CubeOrientation = new()
        {
            { CubeFace.Front, new FaceOrientation(Vector3.forward, Vector3.up) },
            { CubeFace.Right, new FaceOrientation(Vector3.right, Vector3.up) },
            { CubeFace.Back, new FaceOrientation(Vector3.back, Vector3.up) },
            { CubeFace.Left, new FaceOrientation(Vector3.left, Vector3.up) },
            { CubeFace.Top, new FaceOrientation(Vector3.up, Vector3.back) },
            { CubeFace.Bottom, new FaceOrientation(Vector3.down, Vector3.forward) }
        };

        public static FaceOrientation GetOrientation(CubeFace face)
        {
            return CubeOrientation[face];
        }

        public static bool TryGetNeighbor(CubeCellKey cell, Direction direction, int size, float cellSize,
            out CubeCellKey neighbor, out Direction neighborDirection)
        {
            var orientation = CubeOrientation[cell.Face];
            var last = size - 1;
            var x = cell.X;
            var y = cell.Y;

            switch (direction)
            {
                case Direction.North when y < last:
                    neighbor = new CubeCellKey(cell.Face, x, y + 1);
                    neighborDirection = Direction.South;
                    return true;
                case Direction.South when y > 0:
                    neighbor = new CubeCellKey(cell.Face, x, y - 1);
                    neighborDirection = Direction.North;
                    return true;
                case Direction.East when x < last:
                    neighbor = new CubeCellKey(cell.Face, x + 1, y);
                    neighborDirection = Direction.West;
                    return true;
                case Direction.West when x > 0:
                    neighbor = new CubeCellKey(cell.Face, x - 1, y);
                    neighborDirection = Direction.East;
                    return true;
            }

            return TryTraverseEdge(cell, direction, size, cellSize, orientation, out neighbor, out neighborDirection);
        }

        private static bool TryTraverseEdge(CubeCellKey cell, Direction direction, int size, float cellSize,
            FaceOrientation orientation, out CubeCellKey neighbor, out Direction neighborDirection)
        {
            var half = size * cellSize * 0.5f;
            var center = GetCellCenter(orientation, cell.X, cell.Y, size, cellSize, half);
            var dirVector = GetDirectionVector(orientation, direction);
            var edgeDir = Vector3.Cross(dirVector, orientation.Normal).normalized;
            var edgeCenter = center + dirVector * (cellSize * 0.5f);
            const float rotationAngle = -90f;

            if (edgeDir == Vector3.zero)
            {
                neighbor = cell;
                neighborDirection = Direction.South;
                return false;
            }

            var rotation = Quaternion.AngleAxis(rotationAngle, edgeDir);
            var rotatedNormal = rotation * orientation.Normal;
            var rotatedCenter = edgeCenter + rotation * (center - edgeCenter);
            var neighborFace = FaceFromNormal(rotatedNormal);
            var neighborOrientation = CubeOrientation[neighborFace];
            var nx = ProjectIndex(rotatedCenter, neighborOrientation, size, cellSize, half, neighborOrientation.Right);
            var ny = ProjectIndex(rotatedCenter, neighborOrientation, size, cellSize, half, neighborOrientation.Up);
            neighbor = new CubeCellKey(neighborFace, nx, ny);

            var inbound = rotation * -dirVector;
            neighborDirection = DirectionFromVector(inbound, neighborOrientation);
            return true;
        }

        public static Vector3 GetCellCenter(FaceOrientation orientation, int x, int y, int size, float cellSize,
            float half)
        {
            var start = -half + cellSize * 0.5f;
            var offsetX = start + x * cellSize;
            var offsetY = start + y * cellSize;
            return orientation.Normal * half + orientation.Right * offsetX + orientation.Up * offsetY;
        }

        public static Vector3 GetDirectionVector(FaceOrientation orientation, Direction direction)
        {
            return direction switch
            {
                Direction.North => orientation.Up,
                Direction.South => -orientation.Up,
                Direction.East => orientation.Right,
                Direction.West => -orientation.Right,
                _ => Vector3.zero
            };
        }

        private static CubeFace FaceFromNormal(Vector3 normal)
        {
            normal = normal.normalized;
            if (Vector3.Dot(normal, Vector3.forward) > 0.9f) return CubeFace.Front;
            if (Vector3.Dot(normal, Vector3.back) > 0.9f) return CubeFace.Back;
            if (Vector3.Dot(normal, Vector3.right) > 0.9f) return CubeFace.Right;
            if (Vector3.Dot(normal, Vector3.left) > 0.9f) return CubeFace.Left;
            if (Vector3.Dot(normal, Vector3.up) > 0.9f) return CubeFace.Top;
            return CubeFace.Bottom;
        }

        private static int ProjectIndex(Vector3 point, FaceOrientation orientation, int size, float cellSize,
            float half, Vector3 axis)
        {
            var relative = point - orientation.Normal * half;
            var offset = Vector3.Dot(relative, axis.normalized);
            var index = (offset + half) / cellSize - 0.5f;
            return Mathf.Clamp(Mathf.RoundToInt(index), 0, size - 1);
        }

        private static Direction DirectionFromVector(Vector3 vector, FaceOrientation orientation)
        {
            vector.Normalize();
            var dotUp = Vector3.Dot(vector, orientation.Up);
            var dotRight = Vector3.Dot(vector, orientation.Right);

            if (Mathf.Abs(dotUp) > Mathf.Abs(dotRight)) return dotUp > 0f ? Direction.North : Direction.South;

            return dotRight > 0f ? Direction.East : Direction.West;
        }
    }
}