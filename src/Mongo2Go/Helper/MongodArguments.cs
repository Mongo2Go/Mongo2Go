using System;
using System.Collections.Generic;

namespace Mongo2Go.Helper
{
    public static class MongodArguments
    {
        private const string ArgumentSeparator = "--";
        private const string Space = " ";

        /// <summary>
        /// This method will cleanup the additional mongod arguments to make sure the Mongo2Go arguments are not overriden
        /// </summary>
        /// <param name="existingMongodArguments">Mongo2Go defined mongod arguments</param>
        /// <param name="additionalMongodArguments">Additional custom mongod arguments</param>
        /// <returns>Additional mongod arguments excluding the Mogo2Go defined mongod arguments</returns>
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
            for (var i = 0; i < additionalMongodArgumentArray.Length; i++)
            {
                var additionalArgument = additionalMongodArgumentArray[i].Trim();
                var argumentOptionSplit = additionalArgument.Split(' ');

                if (argumentOptionSplit.Length == 0
                    || string.IsNullOrWhiteSpace(argumentOptionSplit[0].Trim())
                    || existingMongodArgumentOptions.Contains(argumentOptionSplit[0].Trim()))
                {
                    continue;
                }

                validAdditionalMongodArguments.Add(ArgumentSeparator + additionalArgument);
            }

            return validAdditionalMongodArguments.Count == 0
                ? string.Empty
                : Space + string.Join(" ", validAdditionalMongodArguments);
        }
    }
}
