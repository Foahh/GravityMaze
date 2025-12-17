using System;
using System.Globalization;
using MazeGenerator.Core;
using UnityEngine;

namespace Networking
{
    /// <summary>
    ///     Parses maze generation settings from UDP payload.
    ///     Expected format: "key=value,key=value,..."
    /// </summary>
    public static class MazeSettingsParser
    {
        /// <summary>
        ///     Attempts to parse maze settings from the specified text.
        /// </summary>
        /// <param name="text">The input string in key=value format.</param>
        /// <param name="settings">The parsed settings if successful.</param>
        /// <returns>True if parsing succeeded; otherwise false.</returns>
        public static bool TryParse(string text, out MazeGenerationSettings settings)
        {
            settings = new MazeGenerationSettings();

            if (string.IsNullOrWhiteSpace(text))
                return false;

            try
            {
                var pairs = text.Split(',');

                foreach (var pair in pairs)
                {
                    var kv = pair.Split('=');
                    if (kv.Length != 2) continue;

                    ApplySetting(settings, kv[0].Trim(), kv[1].Trim());
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MazeSettingsParser] Parse error: {ex.Message}");
                return false;
            }
        }

        private static void ApplySetting(MazeGenerationSettings settings, string key, string value)
        {
            switch (key.ToLowerInvariant())
            {
                case "shape":
                    settings.mazeShape = ParseMazeShape(value);
                    break;

                case "gridsize":
                    if (int.TryParse(value, out var gridSize))
                        settings.gridSize = Mathf.Clamp(gridSize, Constraints.MinGridSize, Constraints.MaxGridSize);
                    break;

                case "cellsize":
                    if (TryParseFloat(value, out var cellSize))
                        settings.cellSize = Mathf.Clamp(cellSize, Constraints.MinCellSize, Constraints.MaxCellSize);
                    break;

                case "wallheight":
                    if (TryParseFloat(value, out var wallHeight))
                        settings.wallHeight = Mathf.Clamp(wallHeight, Constraints.MinWallHeight,
                            Constraints.MaxWallHeight);
                    break;

                case "wallthickness":
                    if (TryParseFloat(value, out var wallThickness))
                        settings.wallThickness = Mathf.Clamp(wallThickness, Constraints.MinWallThickness,
                            Constraints.MaxWallThickness);
                    break;

                case "lidthickness":
                    if (TryParseFloat(value, out var lidThickness))
                        settings.lidThickness = Mathf.Clamp(lidThickness, Constraints.MinLidThickness,
                            Constraints.MaxLidThickness);
                    break;

                case "userandomseed":
                    settings.useRandomSeed = value.Equals("True", StringComparison.OrdinalIgnoreCase);
                    break;

                case "seed":
                    if (int.TryParse(value, out var seed))
                        settings.seed = seed;
                    break;

                case "goaldistance":
                    if (TryParseFloat(value, out var goalDist))
                        // Convert from 0-100% to 0.5-1.0 range
                        settings.goalDistanceRatio = Mathf.Clamp(
                            0.5f + (goalDist / 100f) * 0.5f,
                            Constraints.MinGoalDistanceRatio,
                            Constraints.MaxGoalDistanceRatio);
                    break;

                case "deadendremoval":
                    if (TryParseFloat(value, out var deadEnd))
                        // Convert from 0-100% to 0-1 range
                        settings.deadEndRemoval = Mathf.Clamp(
                            deadEnd / 100f,
                            Constraints.MinDeadEndRemoval,
                            Constraints.MaxDeadEndRemoval);
                    break;
            }
        }

        private static MazeShape ParseMazeShape(string value)
        {
            return value.Equals("Cube", StringComparison.OrdinalIgnoreCase)
                ? MazeShape.Cube
                : MazeShape.Disc;
        }

        private static bool TryParseFloat(string value, out float result)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        /// <summary>
        ///     Validation constraints for maze settings.
        /// </summary>
        public static class Constraints
        {
            public const int MinGridSize = 2;
            public const int MaxGridSize = 20;
            public const float MinCellSize = 0.5f;
            public const float MaxCellSize = 3f;
            public const float MinWallHeight = 0.5f;
            public const float MaxWallHeight = 3f;
            public const float MinWallThickness = 0.05f;
            public const float MaxWallThickness = 0.5f;
            public const float MinLidThickness = 0.05f;
            public const float MaxLidThickness = 0.5f;
            public const float MinGoalDistanceRatio = 0.5f;
            public const float MaxGoalDistanceRatio = 1f;
            public const float MinDeadEndRemoval = 0f;
            public const float MaxDeadEndRemoval = 1f;
        }
    }
}