#if NETSTANDARD2_0
using System;

namespace Mongo2Go.Helper
{
    public static class NetStandard21Compatibility
    {
        /// <summary>
        /// Returns a value indicating whether a specified string occurs within this <paramref name="string"/>, using the specified comparison rules.
        /// </summary>
        /// <param name="string">The string to operate on.</param>
        /// <param name="value">The string to seek.</param>
        /// <param name="comparisonType">One of the enumeration values that specifies the rules to use in the comparison.</param>
        /// <returns><see langword="true"/> if the <paramref name="value"/> parameter occurs within this string, or if <paramref name="value"/> is the empty string (""); otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/></exception>
        public static bool Contains(this string @string, string value, StringComparison comparisonType)
        {
            if (@string == null) throw new ArgumentNullException(nameof(@string));

            return @string.IndexOf(value, comparisonType) >= 0;
        }
    }
}
#endif
