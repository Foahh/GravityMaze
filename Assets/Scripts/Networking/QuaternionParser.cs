using System;
using System.Globalization;
using UnityEngine;

namespace Networking
{
    /// <summary>
    ///     Parses quaternion data from UDP payload.
    ///     Expected format: "QW:0.0,QX:0.0,QY:0.0,QZ:0.0"
    /// </summary>
    public static class QuaternionParser
    {
        private const string PrefixQW = "QW:";
        private const string PrefixQX = "QX:";
        private const string PrefixQY = "QY:";
        private const string PrefixQZ = "QZ:";

        /// <summary>
        ///     Attempts to parse a quaternion from the specified text.
        /// </summary>
        /// <param name="text">The input string in format "QW:0.0,QX:0.0,QY:0.0,QZ:0.0"</param>
        /// <param name="quaternion">The parsed quaternion if successful.</param>
        /// <returns>True if parsing succeeded; otherwise false.</returns>
        public static bool TryParse(string text, out Quaternion quaternion)
        {
            quaternion = Quaternion.identity;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            var parts = text.Split(',');
            if (parts.Length != 4)
                return false;

            if (!TryParseComponent(parts[0], PrefixQW, out var qw) ||
                !TryParseComponent(parts[1], PrefixQX, out var qx) ||
                !TryParseComponent(parts[2], PrefixQY, out var qy) ||
                !TryParseComponent(parts[3], PrefixQZ, out var qz))
                return false;

            if (!IsValidFloat(qw) || !IsValidFloat(qx) || !IsValidFloat(qy) || !IsValidFloat(qz))
                return false;

            quaternion = new Quaternion(qx, qy, qz, qw);
            return TryNormalize(ref quaternion);
        }

        private static bool TryParseComponent(string part, string prefix, out float value)
        {
            value = 0f;
            var trimmed = part.Trim();

            if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;

            var number = trimmed[prefix.Length..];
            return float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool IsValidFloat(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool TryNormalize(ref Quaternion q)
        {
            var magnitudeSquared = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;

            if (magnitudeSquared < 1e-6f)
                return false;

            q.Normalize();
            return true;
        }
    }
}