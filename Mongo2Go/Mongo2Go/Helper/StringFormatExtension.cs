using System;
using System.Globalization;

namespace Mongo2Go.Helper
{
    /// <summary>
    /// saves about 40 keystrokes
    /// </summary>
    public static class StringFormatExtension
    {
        /// <summary>
        /// Populates the template using the provided arguments and the invariant culture
        /// </summary>
        public static string Formatted(this string template, params object[] args)
        {
            return template.Formatted(CultureInfo.InvariantCulture, args);
        }

        /// <summary>
        /// Populates the template using the provided arguments using the provided formatter
        /// </summary>
        public static string Formatted(this string template, IFormatProvider formatter, params object[] args)
        {
            return string.IsNullOrEmpty(template) ? string.Empty : string.Format(formatter, template, args);
        }
    }
}
