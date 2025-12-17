using System.Collections.Generic;
using MazeGenerator.Core;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MazeGenerator.Flat
{
    public static class FlatMazeBuilder
    {
        public static void BuildSquareSurface(Transform parent, MazeGenerationSettings settings)
        {
            var extent = settings.gridSize * settings.cellSize;

            // Create Floor Collider (Inner Cube equivalent)
            var floorCollider = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floorCollider.name = "Floor_Collider";
            floorCollider.tag = "MazeSurface";
            floorCollider.transform.SetParent(parent, false);
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(floorCollider, "Generate Square Maze");
#endif
            // Position: Top face at Y=0. Center at -extent/2.
            floorCollider.transform.position = new Vector3(0f, -extent * 0.5f, 0f);
            floorCollider.transform.localScale = new Vector3(extent, extent, extent);

            var cRenderer = floorCollider.GetComponent<Renderer>();
            if (cRenderer) Object.DestroyImmediate(cRenderer);

            // Visual Surface
            var surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surface.name = "Surface";
            surface.tag = "MazeSurface";
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(surface, "Generate Square Maze");
#endif
            surface.transform.SetParent(parent, false);

            // Position centered at Y=0
            const float surfaceThickness = 0.001f;
            surface.transform.position = Vector3.zero;

            // Scale: extent x extent, with minimal height
            surface.transform.localScale = new Vector3(extent, surfaceThickness, extent);

            var sCollider = surface.GetComponent<Collider>();
            if (sCollider) Object.DestroyImmediate(sCollider);

            if (settings.groundMaterial != null)
            {
                var renderer = surface.GetComponent<Renderer>();
                if (renderer != null) renderer.material = settings.groundMaterial;
            }
        }

        public static void BuildSquareLid(Transform parent, MazeGenerationSettings settings)
        {
            var extent = settings.gridSize * settings.cellSize;
            var lidSize = extent + settings.wallThickness;

            // Visual Lid (No thickness, Quad)
            var lidVisual = GameObject.CreatePrimitive(PrimitiveType.Quad);
            lidVisual.name = "Lid_Visual";
            lidVisual.tag = "MazeLid";
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(lidVisual, "Generate Square Maze");
#endif
            lidVisual.transform.SetParent(parent, false);

            // Position at wall height, rotate to face down (so visible from inside)
            lidVisual.transform.localPosition = new Vector3(0f, settings.wallHeight, 0f);
            // Rotate 90 on X creates a horizontal plane. -90 makes it face down (if Quad faces -Z).
            // We choose -90 to be visible from inside the maze.
            lidVisual.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            lidVisual.transform.localScale = new Vector3(lidSize, lidSize, 1f);

            var meshCollider = lidVisual.GetComponent<Collider>();
            if (meshCollider) Object.DestroyImmediate(meshCollider);

            if (settings.lidMaterial != null)
            {
                var renderer = lidVisual.GetComponent<Renderer>();
                if (renderer != null) renderer.material = settings.lidMaterial;
            }

            // Collider Lid (Very large, invisible)
            var lidCollider = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lidCollider.name = "Lid_Collider";
            lidCollider.tag = "MazeLid";
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(lidCollider, "Generate Square Maze");
#endif
            lidCollider.transform.SetParent(parent, false);

            var colliderThickness = 10f;
            var colliderSize = lidSize * 50f;

            lidCollider.transform.localPosition = new Vector3(0f, settings.wallHeight + colliderThickness * 0.5f, 0f);
            lidCollider.transform.localScale = new Vector3(colliderSize, colliderThickness, colliderSize);

            var cRenderer = lidCollider.GetComponent<Renderer>();
            if (cRenderer) Object.DestroyImmediate(cRenderer);
        }

        public static void BuildSquareWalls(Transform parent, FlatMazeData data, MazeGenerationSettings settings)
        {
            var size = settings.gridSize;
            var cellSize = settings.cellSize;
            var wallHeight = settings.wallHeight;
            var wallThickness = settings.wallThickness;
            var half = cellSize * size * 0.5f;
            var start = -half + cellSize * 0.5f;
            var gridOrigin = start - cellSize * 0.5f;
            var joints = new HashSet<Vector2Int>();

            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var cell = data.Cells[x, y];
                var center = new Vector3(start + x * cellSize, 0f, start + y * cellSize);

                foreach (var direction in DirectionHelper.AllDirections)
                {
                    if (!cell.Walls[direction]) continue;

                    if (TryGetFlatNeighbor(x, y, direction, size, out var nx, out var ny))
                        if (CompareFlatCells(x, y, nx, ny) >= 0)
                            continue;

                    var nodes = GetEdgeNodes(x, y, direction);
                    CreateSquareWall(parent, center, direction, cellSize, wallHeight, wallThickness);
                    EnsureSquareJoint(parent, nodes.Item1, gridOrigin, cellSize, wallHeight, wallThickness, joints);
                    EnsureSquareJoint(parent, nodes.Item2, gridOrigin, cellSize, wallHeight, wallThickness, joints);
                }
            }
        }

        public static bool TryGetFlatNeighbor(int x, int y, Direction direction, int size, out int nx, out int ny)
        {
            nx = x;
            ny = y;
            switch (direction)
            {
                case Direction.North when y < size - 1:
                    ny = y + 1;
                    return true;
                case Direction.East when x < size - 1:
                    nx = x + 1;
                    return true;
                case Direction.South when y > 0:
                    ny = y - 1;
                    return true;
                case Direction.West when x > 0:
                    nx = x - 1;
                    return true;
                default:
                    return false;
            }
        }

        private static void CreateSquareWall(Transform parent, Vector3 center, Direction direction, float cellSize,
            float wallHeight, float wallThickness)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = $"Wall_{direction}";
            wall.tag = "MazeWall";
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(wall, "Generate Square Maze");
#endif
            wall.transform.SetParent(parent, false);

            var trimmedLength = Mathf.Max(0.01f, cellSize - wallThickness);

            Vector3 offset;
            Vector3 scale;

            switch (direction)
            {
                case Direction.North:
                    // Position at north edge, extending half thickness in both X directions
                    offset = new Vector3(0f, wallHeight * 0.5f, cellSize * 0.5f);
                    scale = new Vector3(trimmedLength, wallHeight, wallThickness);
                    break;
                case Direction.South:
                    // Position at south edge, extending half thickness in both X directions
                    offset = new Vector3(0f, wallHeight * 0.5f, -cellSize * 0.5f);
                    scale = new Vector3(trimmedLength, wallHeight, wallThickness);
                    break;
                case Direction.East:
                    // Position at east edge, extending half thickness in both Z directions
                    offset = new Vector3(cellSize * 0.5f, wallHeight * 0.5f, 0f);
                    scale = new Vector3(wallThickness, wallHeight, trimmedLength);
                    break;
                default: // West
                    // Position at west edge, extending half thickness in both Z directions
                    offset = new Vector3(-cellSize * 0.5f, wallHeight * 0.5f, 0f);
                    scale = new Vector3(wallThickness, wallHeight, trimmedLength);
                    break;
            }

            wall.transform.position = center + offset;
            wall.transform.localScale = scale;
        }

        private static void EnsureSquareJoint(Transform parent, Vector2Int node, float gridOrigin, float cellSize,
            float wallHeight, float wallThickness, HashSet<Vector2Int> joints)
        {
            if (!joints.Add(node)) return;

            var joint = GameObject.CreatePrimitive(PrimitiveType.Cube);
            joint.name = $"Joint_{node.x}_{node.y}";
            joint.tag = "MazeWall";
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(joint, "Generate Square Maze");
#endif
            joint.transform.SetParent(parent, false);
            var x = gridOrigin + node.x * cellSize;
            var z = gridOrigin + node.y * cellSize;
            joint.transform.position = new Vector3(x, wallHeight * 0.5f, z);
            joint.transform.localScale = new Vector3(wallThickness, wallHeight, wallThickness);
        }

        private static (Vector2Int, Vector2Int) GetEdgeNodes(int x, int y, Direction direction)
        {
            return direction switch
            {
                Direction.North => (new Vector2Int(x, y + 1), new Vector2Int(x + 1, y + 1)),
                Direction.South => (new Vector2Int(x, y), new Vector2Int(x + 1, y)),
                Direction.East => (new Vector2Int(x + 1, y), new Vector2Int(x + 1, y + 1)),
                _ => (new Vector2Int(x, y), new Vector2Int(x, y + 1))
            };
        }

        private static int CompareFlatCells(int ax, int ay, int bx, int by)
        {
            var aIndex = ay * 1000 + ax;
            var bIndex = by * 1000 + bx;
            return aIndex.CompareTo(bIndex);
        }
    }
}