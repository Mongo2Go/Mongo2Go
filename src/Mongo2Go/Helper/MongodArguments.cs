using System;
using System.Collections.Generic;

namespace Mongo2Go.Helper
{
    public static class MongodArguments
    {
        private const string ArgumentSeparator = "--";
        private const string Space = " ";

        /// <summary>
        /// Returns the <paramref name="additionalMongodArguments" /> if it is verified that it does not contain any mongod argument already defined by Mongo2Go.
        /// </summary>
        /// <param name="existingMongodArguments">mongod arguments defined by Mongo2Go</param>
        /// <param name="additionalMongodArguments">Additional mongod arguments</param>
        /// <exception cref="T:System.ArgumentException"><paramref name="additionalMongodArguments" /> contains at least one mongod argument already defined by Mongo2Go</exception>
        /// <returns>A string with the additional mongod arguments</returns>
        public static string GetValidAdditionalArguments(string existingMongodArguments, string additionalMongodArguments)
        {
            if (string.IsNullOrWhiteSpace(additionalMongodArguments))
            {
                return string.Empty;
            }

            var existingMongodArgumentArray = existingMongodArguments.Trim().Split(new[] { ArgumentSeparator }, StringSplitOptions.RemoveEmptyEntries);

            var existingMongodArgumentOptions = new List<string>();
            for (var i = 0; i < existingMongodArgumentArray.Length; i++)
            {
                var argumentOptionSplit = existingMongodArgumentArray[i].Split(' ');

                if (argumentOptionSplit.Length == 0
                    || string.IsNullOrWhiteSpace(argumentOptionSplit[0].Trim()))
                {
                    continue;
                }

                existingMongodArgumentOptions.Add(argumentOptionSplit[0].Trim());
            }

            var additionalMongodArgumentArray = additionalMongodArguments.Trim().Split(new[] { ArgumentSeparator }, StringSplitOptions.RemoveEmptyEntries);

            var validAdditionalMongodArguments = new List<string>();
            var duplicateMongodArguments = new List<string>();
            for (var i = 0; i < additionalMongodArgumentArray.Length; i++)
            {
                var additionalArgument = additionalMongodArgumentArray[i].Trim();
                var argumentOptionSplit = additionalArgument.Split(' ');

                if (argumentOptionSplit.Length == 0
                    || string.IsNullOrWhiteSpace(argumentOptionSplit[0].Trim()))
                {
                    continue;
                }

                if (existingMongodArgumentOptions.Contains(argumentOptionSplit[0].Trim()))
                {
                    duplicateMongodArguments.Add(argumentOptionSplit[0].Trim());
                }

                validAdditionalMongodArguments.Add(ArgumentSeparator + additionalArgument);
            }

            if (duplicateMongodArguments.Count != 0)
            {
                throw new ArgumentException($"mongod arguments defined by Mongo2Go ({string.Join(", ", existingMongodArgumentOptions)}) cannot be overriden. Please remove the following additional argument(s): {string.Join(", ", duplicateMongodArguments)}.");
            }

            return validAdditionalMongodArguments.Count == 0
                ? string.Empty
                : Space + string.Join(" ", validAdditionalMongodArguments);
        }
    }
}
