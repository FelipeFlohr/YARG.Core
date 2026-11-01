using System;
using System.Collections.Generic;

namespace YARG.Core.Utility
{
    public static class QueryStringArgs
    {
        /// <summary>
        /// Converts a URL query string (<c>location.search</c>, with or without the leading <c>?</c>)
        /// into command line style arguments, without the executable name.
        /// </summary>
        /// <remarks>
        /// Each <c>key=value</c> pair becomes <c>-key</c> followed by the tokens of the decoded value
        /// (<c>+</c> is a space). Values are split on spaces and empty tokens are dropped, so
        /// <c>?mp-song=My%20Song</c> yields <c>-mp-song My Song</c>, exactly like passing
        /// <c>-mp-song My Song</c> unquoted on the desktop. A key without a value yields only <c>-key</c>.
        /// Order and repeated keys are preserved.
        /// </remarks>
        public static string[] Parse(string query)
        {
            var args = new List<string>();
            if (string.IsNullOrEmpty(query))
            {
                return args.ToArray();
            }

            if (query[0] == '?')
            {
                query = query.Substring(1);
            }

            foreach (var pair in query.Split('&'))
            {
                if (pair.Length == 0)
                {
                    continue;
                }

                int eq = pair.IndexOf('=');
                string key = eq < 0 ? pair : pair.Substring(0, eq);
                key = Decode(key).Trim();
                if (key.Length == 0)
                {
                    continue;
                }

                args.Add("-" + key);
                if (eq < 0)
                {
                    continue;
                }

                var tokens = Decode(pair.Substring(eq + 1)).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                args.AddRange(tokens);
            }

            return args.ToArray();
        }

        private static string Decode(string value)
        {
            return Uri.UnescapeDataString(value.Replace('+', ' '));
        }
    }
}
