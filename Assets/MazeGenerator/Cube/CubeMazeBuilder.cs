using System;
using System.Collections.Generic;
using MazeGenerator.Core;
using UnityEngine;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MazeGenerator.Cube
{
    public static class CubeMazeBuilder
    {
        public static void BuildCubeSurfaces(Transform parent, MazeGenerationSettings settings)
        {
            var faceSize = settings.gridSize * settings.cellSize;
            var half = faceSize * 0.5f;

            var innerCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            innerCube.name = "Inner_Cube";
            innerCube.transform.SetParent(parent, false);
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(innerCube, "Generate Cube Maze");
#endif
            innerCube.transform.localScale = new Vector3(faceSize, faceSize, faceSize);

            var innerRenderer = innerCube.GetComponent<Renderer>();
            if (innerRenderer) Object.DestroyImmediate(innerRenderer);

            foreach (CubeFace face in Enum.GetValues(typeof(CubeFace)))
            {
                // Visual Surface
                var surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
                surface.name = face + "_Surface";
                surface.tag = "MazeSurface";
                surface.transform.SetParent(parent, false);

#if UNITY_EDITOR
                Undo.RegisterCreatedObjectUndo(surface, "Generate Cube Maze");
#endif

                var orientation = CubeTopology.GetOrientation(face);
                const float surfaceThickness = 0.001f;

                surface.transform.position = orientation.Normal * half;
                surface.transform.rotation = Quaternion.LookRotation(-orientation.Normal, orientation.Up);
                surface.transform.localScale = new Vector3(faceSize, faceSize, surfaceThickness);

                var sCollider = surface.GetComponent<Collider>();
                if (sCollider) Object.DestroyImmediate(sCollider);

                if (settings.groundMaterial != null)
                {
                    var renderer = surface.GetComponent<Renderer>();
                    if (renderer != null) renderer.material = settings.groundMaterial;
                }
            }
        }

        public static void BuildCubeLid(Transform parent, MazeGenerationSettings settings)
        {
            var faceSize = settings.gridSize * settings.cellSize;
            var half = faceSize * 0.5f;
            var lidInnerDist = half + settings.wallHeight;
            // Visual size to cover corners roughly
            var visualSize = lidInnerDist * 2.01f;

            foreach (CubeFace face in Enum.GetValues(typeof(CubeFace)))
            {
                var orientation = CubeTopology.GetOrientation(face);

                // Visual Lid (Quad)
                var lidVisual = GameObject.CreatePrimitive(PrimitiveType.Quad);
                lidVisual.name = face + "_Lid_Visual";
                lidVisual.tag = "MazeLid";
                lidVisual.transform.SetParent(parent, false);
#if UNITY_EDITOR
                Undo.RegisterCreatedObjectUndo(lidVisual, "Generate Cube Maze");
#endif
                lidVisual.transform.position = orientation.Normal * lidInnerDist;
                // Z points Out (Normal). Quad faces -Z (In).
                lidVisual.transform.rotation = Quaternion.LookRotation(orientation.Normal, orientation.Up);
                lidVisual.transform.localScale = new Vector3(visualSize, visualSize, 1f);

                var vCollider = lidVisual.GetComponent<Collider>();
                if (vCollider) Object.DestroyImmediate(vCollider);

                if (settings.lidMaterial != null)
                {
                    var renderer = lidVisual.GetComponent<Renderer>();
                    if (renderer != null) renderer.material = settings.lidMaterial;
                }

                // Collider Lid (Thick Cube, invisible)
                var lidCollider = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lidCollider.name = face + "_Lid_Collider";
                lidCollider.tag = "MazeLid";
                lidCollider.transform.SetParent(parent, false);
#if UNITY_EDITOR
                Undo.RegisterCreatedObjectUndo(lidCollider, "Generate Cube Maze");
#endif
                var colliderThickness = 10f;
                var colliderDist = lidInnerDist + colliderThickness * 0.5f;

                lidCollider.transform.position = orientation.Normal * colliderDist;
                lidCollider.transform.rotation = Quaternion.LookRotation(orientation.Normal, orientation.Up);

                var cSize = (lidInnerDist + colliderThickness) * 2f;
                lidCollider.transform.localScale = new Vector3(cSize, cSize, colliderThickness);

                var cRenderer = lidCollider.GetComponent<Renderer>();
                if (cRenderer) Object.DestroyImmediate(cRenderer);
            }
        }

        public static void BuildCubeWalls(Transform parent, CubeMazeData data, MazeGenerationSettings settings)
        {
            var wallHeight = settings.wallHeight;
            var wallThickness = settings.wallThickness;
            var cellSize = settings.cellSize;
            var half = settings.gridSize * cellSize * 0.5f;

            var faceParents = new Dictionary<CubeFace, Transform>();
            var faceJoints = new Dictionary<CubeFace, HashSet<Vector2Int>>();
            foreach (CubeFace face in Enum.GetValues(typeof(CubeFace)))
            {
                var faceRoot = new GameObject(face + "_Walls");
                faceRoot.transform.SetParent(parent, false);
#if UNITY_EDITOR
                Undo.RegisterCreatedObjectUndo(faceRoot, "Generate Cube Maze");
#endif
                faceParents[face] = faceRoot.transform;
                faceJoints[face] = new HashSet<Vector2Int>();
            }

            var seamRoot = new GameObject("Face_Junctions");
            seamRoot.transform.SetParent(parent, false);
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(seamRoot, "Generate Cube Maze");
#endif
            var seamBlocks = new HashSet<CubeSeamKey>();

            foreach (var pair in data.Cells)
            {
                var cellKey = pair.Key;
                var cell = pair.Value;
                var orientation = CubeTopology.GetOrientation(cellKey.Face);
                var center = CubeTopology.GetCellCenter(orientation, cellKey.X, cellKey.Y, settings.gridSize, cellSize,
                    half);

                foreach (var direction in DirectionHelper.AllDirections)
                {
                    if (!cell.Walls[direction]) continue;

                    if (!CubeTopology.TryGetNeighbor(cellKey, direction, settings.gridSize, cellSize, out var neighbor,
                            out _))
                    {
                        var nodes = GetEdgeNodes(cellKey.X, cellKey.Y, direction);
                        CreateCubeWall(faceParents[cellKey.Face], orientation, center, direction, cellSize, wallHeight,
                            wallThickness);
                        EnsureCubeJoint(faceParents[cellKey.Face], faceJoints[cellKey.Face], orientation, nodes.Item1,
                            wallHeight, wallThickness, cellSize, half);
                        EnsureCubeJoint(faceParents[cellKey.Face], faceJoints[cellKey.Face], orientation, nodes.Item2,
                            wallHeight, wallThickness, cellSize, half);
                        continue;
                    }

                    if (neighbor.Face == cellKey.Face)
                        if (CompareCubeCells(cellKey, neighbor) >= 0)
                            continue;

                    var edgeNodes = GetEdgeNodes(cellKey.X, cellKey.Y, direction);
                    CreateCubeWall(faceParents[cellKey.Face], orientation, center, direction, cellSize, wallHeight,
                        wallThickness);
                    EnsureCubeJoint(faceParents[cellKey.Face], faceJoints[cellKey.Face], orientation, edgeNodes.Item1,
                        wallHeight, wallThickness, cellSize, half);
                    EnsureCubeJoint(faceParents[cellKey.Face], faceJoints[cellKey.Face], orientation, edgeNodes.Item2,
                        wallHeight, wallThickness, cellSize, half);

                    if (neighbor.Face != cellKey.Face)
                        EnsureCubeSeamBlock(seamRoot.transform, seamBlocks, cellKey, neighbor, orientation, center,
                            direction, wallHeight, wallThickness, cellSize);
                }
            }

            CreateCornerSeamBlocks(seamRoot.transform, half, wallHeight, wallThickness);
        }

        private static void CreateCubeWall(Transform parent, FaceOrientation orientation, Vector3 cellCenter,
            Direction direction, float cellSize, float wallHeight, float wallThickness)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = $"Wall_{direction}";
            wall.tag = "MazeWall";
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(wall, "Generate Cube Maze");
#endif
            wall.transform.SetParent(parent, false);

            var dirVector = CubeTopology.GetDirectionVector(orientation, direction);
            var lengthAxis = direction is Direction.North or Direction.South ? orientation.Right : orientation.Up;

            var trimmedLength = Mathf.Max(0.01f, cellSize - wallThickness);

            // Position wall at the edge, centered on the cell boundary
            var edgeCenter = cellCenter + dirVector * (cellSize * 0.5f);
            var heightOffset = orientation.Normal * (wallHeight * 0.5f);
            wall.transform.position = edgeCenter + heightOffset;

            // Set wall orientation: forward along length, right perpendicular to face, up along normal
            wall.transform.rotation = Quaternion.LookRotation(lengthAxis, orientation.Normal);

            wall.transform.localScale = new Vector3(wallThickness, wallHeight, trimmedLength);
        }

        private static void EnsureCubeJoint(Transform parent, HashSet<Vector2Int> joints, FaceOrientation orientation,
            Vector2Int node, float wallHeight, float wallThickness, float cellSize, float half)
        {
            if (!joints.Add(node)) return;

            var joint = GameObject.CreatePrimitive(PrimitiveType.Cube);
            joint.name = $"Joint_{node.x}_{node.y}";
            joint.tag = "MazeWall";
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(joint, "Generate Cube Maze");
#endif
            joint.transform.SetParent(parent, false);
            joint.transform.position = GetNodePosition(orientation, node.x, node.y, cellSize, half) +
                                       orientation.Normal * (wallHeight * 0.5f);
            joint.transform.rotation = Quaternion.LookRotation(orientation.Right, orientation.Normal);
            joint.transform.localScale = new Vector3(wallThickness, wallHeight, wallThickness);
        }

        private static Vector3 GetNodePosition(FaceOrientation orientation, int nodeX, int nodeY, float cellSize,
            float half)
        {
            var origin = -half;
            var offsetX = origin + nodeX * cellSize;
            var offsetY = origin + nodeY * cellSize;
            return orientation.Normal * half + orientation.Right * offsetX + orientation.Up * offsetY;
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

        private static int CompareCubeCells(CubeCellKey a, CubeCellKey b)
        {
            var faceIndex = (int)a.Face;
            var otherFaceIndex = (int)b.Face;
            if (faceIndex != otherFaceIndex) return faceIndex.CompareTo(otherFaceIndex);

            var aIndex = a.Y * 1000 + a.X;
            var bIndex = b.Y * 1000 + b.X;
            return aIndex.CompareTo(bIndex);
        }

        private static void EnsureCubeSeamBlock(Transform parent, HashSet<CubeSeamKey> seamBlocks,
            CubeCellKey cell, CubeCellKey neighbor, FaceOrientation orientation, Vector3 cellCenter,
            Direction direction,
            float wallHeight, float wallThickness, float cellSize)
        {
            var seamKey = new CubeSeamKey(cell, neighbor);
            if (!seamBlocks.Add(seamKey)) return;

            var neighborOrientation = CubeTopology.GetOrientation(neighbor.Face);
            var dirVector = CubeTopology.GetDirectionVector(orientation, direction);
            var edgeCenter = cellCenter + dirVector * (cellSize * 0.5f);

            var seamAxis = Vector3.Cross(neighborOrientation.Normal, orientation.Normal);
            if (seamAxis == Vector3.zero) return;
            seamAxis.Normalize();

            var seamBlock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seamBlock.name = $"SeamBlock_{cell.Face}_{neighbor.Face}_{direction}";
            seamBlock.tag = "MazeWall";
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(seamBlock, "Generate Cube Maze");
#endif
            seamBlock.transform.SetParent(parent, false);
            seamBlock.transform.rotation = Quaternion.LookRotation(seamAxis, orientation.Normal);

            var seamLength = Mathf.Max(0.01f, cellSize + wallThickness);
            var depthCurrent = wallHeight;
            var depthNeighbor = wallHeight;
            seamBlock.transform.localScale = new Vector3(depthNeighbor, depthCurrent, seamLength);

            var offset = orientation.Normal * (depthCurrent * 0.5f) +
                         neighborOrientation.Normal * (depthNeighbor * 0.5f);
            seamBlock.transform.position = edgeCenter + offset;
        }

        private static void CreateCornerSeamBlocks(Transform parent, float half, float wallHeight, float wallThickness)
        {
            var zFaces = new[] { CubeFace.Front, CubeFace.Back };
            var xFaces = new[] { CubeFace.Right, CubeFace.Left };
            var yFaces = new[] { CubeFace.Top, CubeFace.Bottom };
            var hasHeight = wallHeight > Mathf.Epsilon;
            var thicknessRatio = hasHeight ? Mathf.Clamp01(wallThickness / wallHeight) : 0f;
            var scaleMultiplier = 1f + thicknessRatio * 0.5f;
            var axisOffset = (scaleMultiplier - 1f) * 0.5f * wallHeight;
            var seamScale = Vector3.one * (wallHeight * scaleMultiplier);

            foreach (var zFace in zFaces)
            foreach (var xFace in xFaces)
            foreach (var yFace in yFaces)
            {
                var direction = CubeTopology.GetOrientation(zFace).Normal +
                                CubeTopology.GetOrientation(xFace).Normal +
                                CubeTopology.GetOrientation(yFace).Normal;

                var seamBlock = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seamBlock.name = $"CornerSeam_{zFace}_{xFace}_{yFace}";
                seamBlock.tag = "MazeWall";
#if UNITY_EDITOR
                Undo.RegisterCreatedObjectUndo(seamBlock, "Generate Cube Maze");
#endif
                seamBlock.transform.SetParent(parent, false);
                var position = direction * (half + wallHeight * 0.5f);
                position.x -= Mathf.Sign(position.x) * axisOffset;
                position.y -= Mathf.Sign(position.y) * axisOffset;
                position.z -= Mathf.Sign(position.z) * axisOffset;
                seamBlock.transform.position = position;
                seamBlock.transform.localScale = seamScale;
            }
        }

        private readonly struct CubeSeamKey : IEquatable<CubeSeamKey>
        {
            public CubeSeamKey(CubeCellKey a, CubeCellKey b)
            {
                if (CompareCubeCells(a, b) <= 0)
                {
                    A = a;
                    B = b;
                }
                else
                {
                    A = b;
                    B = a;
                }
            }

            public CubeCellKey A { get; }
            public CubeCellKey B { get; }

            public bool Equals(CubeSeamKey other)
            {
                return A.Equals(other.A) && B.Equals(other.B);
            }

            public override bool Equals(object obj)
            {
                return obj is CubeSeamKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (A.GetHashCode() * 397) ^ B.GetHashCode();
                }
            }
        }
    }
}