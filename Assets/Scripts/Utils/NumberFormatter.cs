using UnityEngine;

namespace Utils
{
    public static class NumberFormatter
    {
        public static string FormatRoundedAbbreviation(float value, int decimalPlaces = 0)
        {
            // Handle negative values
            if (value < 0)
            {
                return "-" + FormatRoundedAbbreviation(Mathf.Abs(value), decimalPlaces);
            }

            // Handle zero
            if (value == 0)
            {
                return "0 ";// + (decimalPlaces > 0 ? "." + new string('0', decimalPlaces) : "");
            }

            // Define a format string like "0.##" or "0.000"
            string format = "0" + (decimalPlaces > 0 ? "." + new string('#', decimalPlaces) : "");

            // Handle tiny values
            if (value < 0.000000001f) // pico
                return $"{(value * 1_000_000_000_000).ToString(format)} p";
            else if (value < 0.000001f) // nano
                return $"{(value * 1_000_000_000).ToString(format)} n";
            else if (value < 0.001f) // micro
                return $"{(value * 1_000_000).ToString(format)} μ";
            else if (value < 1f) // milli
                return $"{(value * 1_000).ToString(format)} m";
            // Handle large values
            else if (value >= 1_000_000_000_000f)
                return $"{(value / 1_000_000_000_000).ToString(format)} T";
            else if (value >= 1_000_000_000f)
                return $"{(value / 1_000_000_000).ToString(format)} B";
            else if (value >= 1_000_000f)
                return $"{(value / 1_000_000).ToString(format)} M";
            else if (value >= 1_000f)
                return $"{(value / 1_000).ToString(format)} K";
            else
                return value.ToString(format) + " ";
        }

        /// <summary>
        /// Formats a value with a specific unit
        /// </summary>
        /// <param name="value">The value to format</param>
        /// <param name="unit">The unit to append (e.g., "V" for volts)</param>
        /// <param name="decimalPlaces">Number of decimal places to show</param>
        /// <returns>Formatted string with unit</returns>
        public static string FormatWithUnit(float value, string unit, int decimalPlaces = 0)
        {
            string formattedValue = FormatRoundedAbbreviation(value, decimalPlaces);
            return $"{formattedValue}{unit}";
        }
    }
}