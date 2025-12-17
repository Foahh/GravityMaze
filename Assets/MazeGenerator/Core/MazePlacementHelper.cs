using MazeGenerator.Cube;
using UnityEngine;

namespace MazeGenerator.Core
{
    /// <summary>
    ///     Helper class for calculating object placement positions within mazes.
    /// </summary>
    public static class MazePlacementHelper
    {
        #region Flat Maze Positions

        /// <summary>
        ///     Gets the local position of a cell center in a flat maze.
        /// </summary>
        /// <param name="settings">The maze generation settings.</param>
        /// <param name="cell">The cell coordinates.</param>
        /// <returns>The local position of the cell center.</returns>
        public static Vector3 GetFlatCellCenterLocal(MazeGenerationSettings settings, Vector2Int cell)
        {
            var halfGridSize = settings.gridSize * settings.cellSize * 0.5f;
            var cellOffset = settings.cellSize * 0.5f;
            var startOffset = -halfGridSize + cellOffset;

            return new Vector3(
                startOffset + cell.x * settings.cellSize,
                0f,
                startOffset + cell.y * settings.cellSize);
        }

        /// <summary>
        ///     Gets the world position for placing an object in a flat maze cell.
        /// </summary>
        public static Vector3 GetFlatCellWorldPosition(
            MazeGenerationSettings settings, Vector2Int cell, Transform mazeRoot, float heightOffset)
        {
            var localPosition = GetFlatCellCenterLocal(settings, cell) + Vector3.up * heightOffset;
            return mazeRoot.TransformPoint(localPosition);
        }

        #endregion

        #region Cube Maze Positions

        /// <summary>
        ///     Gets the local position of a cell center in a cube maze.
        /// </summary>
        /// <param name="settings">The maze generation settings.</param>
        /// <param name="cellKey">The cell key identifying the face and coordinates.</param>
        /// <param name="normalOffset">Additional offset along the face normal.</param>
        /// <returns>The local position of the cell center.</returns>
        public static Vector3 GetCubeCellCenterLocal(
            MazeGenerationSettings settings, CubeCellKey cellKey, float normalOffset = 0f)
        {
            var halfSize = settings.gridSize * settings.cellSize * 0.5f;
            var orientation = CubeTopology.GetOrientation(cellKey.Face);
            var center = CubeTopology.GetCellCenter(
                orientation, cellKey.X, cellKey.Y,
                settings.gridSize, settings.cellSize, halfSize);

            return center + orientation.Normal * Mathf.Max(0f, normalOffset);
        }

        /// <summary>
        ///     Gets the local rotation for an object placed on a cube face.
        /// </summary>
        /// <param name="cellKey">The cell key identifying the face.</param>
        /// <returns>The local rotation aligning up with the face normal.</returns>
        public static Quaternion GetCubeCellLocalRotation(CubeCellKey cellKey)
        {
            var orientation = CubeTopology.GetOrientation(cellKey.Face);
            return Quaternion.FromToRotation(Vector3.up, orientation.Normal);
        }

        /// <summary>
        ///     Gets the world position for placing an object in a cube maze cell.
        /// </summary>
        public static Vector3 GetCubeCellWorldPosition(
            MazeGenerationSettings settings, CubeCellKey cellKey, Transform mazeRoot, float heightOffset)
        {
            var localPosition = GetCubeCellCenterLocal(settings, cellKey, heightOffset);
            return mazeRoot.TransformPoint(localPosition);
        }

        /// <summary>
        ///     Gets the world rotation for an object placed on a cube face.
        /// </summary>
        public static Quaternion GetCubeCellWorldRotation(CubeCellKey cellKey, Transform mazeRoot)
        {
            var localRotation = GetCubeCellLocalRotation(cellKey);
            return mazeRoot.rotation * localRotation;
        }

        #endregion
    }
}