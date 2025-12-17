using System;
using System.Globalization;
using UnityEngine;

namespace Networking
{
    /// <summary>
    ///     Camera settings received from the controller.
    /// </summary>
    public struct CameraSettings
    {
        public float Angle;
        public float DistanceMultiplier;

        public static readonly CameraSettings Default = new()
        {
            Angle = 45f,
            DistanceMultiplier = 2.0f
        };
    }

    /// <summary>
    ///     Parses camera settings from UDP payload.
    ///     Expected format: "angle={value},distance={value}"
    /// </summary>
    public static class CameraSettingsParser
    {
        public const float MinAngle = 30f;
        public const float MaxAngle = 90f;
        public const float MinDistance = 0.5f;
        public const float MaxDistance = 5f;

        /// <summary>
        ///     Attempts to parse camera settings from the specified text.
        /// </summary>
        /// <param name="text">The input string in key=value format.</param>
        /// <param name="settings">The parsed settings if successful.</param>
        /// <returns>True if parsing succeeded; otherwise false.</returns>
        public static bool TryParse(string text, out CameraSettings settings)
        {
            settings = CameraSettings.Default;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            try
            {
                var pairs = text.Split(',');

                foreach (var pair in pairs)
                {
                    var kv = pair.Split('=');
                    if (kv.Length != 2) continue;

                    ApplySetting(ref settings, kv[0].Trim(), kv[1].Trim());
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CameraSettingsParser] Parse error: {ex.Message}");
                return false;
            }
        }

        private static void ApplySetting(ref CameraSettings settings, string key, string value)
        {
            switch (key.ToLowerInvariant())
            {
                case "angle":
                    if (TryParseFloat(value, out var angle))
                        settings.Angle = Mathf.Clamp(angle, MinAngle, MaxAngle);
                    break;

                case "distance":
                    if (TryParseFloat(value, out var distance))
                        settings.DistanceMultiplier = Mathf.Clamp(distance, MinDistance, MaxDistance);
                    break;
            }
        }

        private static bool TryParseFloat(string value, out float result)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }
    }
}

