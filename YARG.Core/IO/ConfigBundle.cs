using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace YARG.Core.IO
{
    /// <summary>
    /// Single-file container used to export and import the settings, profiles and control bindings of the web build.
    /// </summary>
    public static class ConfigBundle
    {
        public const string FORMAT = "yarg-web-config";
        public const int VERSION = 1;

        private const string FORMAT_KEY = "format";
        private const string VERSION_KEY = "version";
        private const string SETTINGS_KEY = "settings";
        private const string PROFILES_KEY = "profiles";
        private const string BINDINGS_KEY = "bindings";

        /// <summary>
        /// Builds a bundle. A part is omitted when its argument is <see langword="null"/>.
        /// The texts must be valid JSON; they are embedded as JSON, not as escaped strings.
        /// </summary>
        public static string Create(string? settingsJson, string? profilesJson, string? bindingsJson)
        {
            var root = new JObject
            {
                [FORMAT_KEY] = FORMAT,
                [VERSION_KEY] = VERSION,
            };
            if (settingsJson != null)
            {
                root[SETTINGS_KEY] = ParseToken(settingsJson);
            }
            if (profilesJson != null)
            {
                root[PROFILES_KEY] = ParseToken(profilesJson);
                }
                if (bindingsJson != null)
                {
                    root[BINDINGS_KEY] = ParseToken(bindingsJson);
            }
            return root.ToString(Formatting.Indented);
        }

        /// <summary>
        /// Reads a bundle. Never throws; on failure all texts are <see langword="null"/> and
        /// <paramref name="error"/> describes the problem.
        /// </summary>
        public static bool TryParse(string bundleJson, out string? settingsJson, out string? profilesJson, out string? bindingsJson, out string? error)
        {
            settingsJson = null;
            profilesJson = null;
            bindingsJson = null;
            error = null;

            try
            {
                if (!(ParseToken(bundleJson) is JObject root))
                {
                    error = "The bundle is not a JSON object.";
                    return false;
                }

                if (root[FORMAT_KEY]?.Type != JTokenType.String || (string?) root[FORMAT_KEY] != FORMAT)
                {
                    error = $"Unknown bundle format (expected \"{FORMAT}\").";
                    return false;
                }

                if (root[VERSION_KEY]?.Type != JTokenType.Integer || (int) root[VERSION_KEY]! != VERSION)
                {
                    error = $"Unsupported bundle version (expected {VERSION}).";
                    return false;
                }

                var settings = root[SETTINGS_KEY];
                var profiles = root[PROFILES_KEY];
                var bindings = root[BINDINGS_KEY];
                if (settings == null && profiles == null && bindings == null)
                {
                    error = "The bundle has no settings, profiles or bindings.";
                    return false;
                }
                if (settings != null && settings.Type != JTokenType.Object)
                {
                    error = "The bundle settings must be a JSON object.";
                    return false;
                }
                if (profiles != null && profiles.Type != JTokenType.Array)
                {
                    error = "The bundle profiles must be a JSON array.";
                    return false;
                }

                if (bindings != null && bindings.Type != JTokenType.Object)
                {
                    error = "The bundle bindings must be a JSON object.";
                    return false;
                }

                settingsJson = settings?.ToString(Formatting.Indented);
                profilesJson = profiles?.ToString(Formatting.Indented);
                bindingsJson = bindings?.ToString(Formatting.Indented);
                return true;
            }
            catch (Exception e)
            {
                settingsJson = null;
                profilesJson = null;
                bindingsJson = null;
                error = $"The bundle is not valid JSON: {e.Message}";
                return false;
            }
        }

        // Dates stay as plain strings so the text round-trips unchanged.
        private static JToken ParseToken(string json)
        {
            using var reader = new JsonTextReader(new StringReader(json))
            {
                DateParseHandling = DateParseHandling.None,
            };
            return JToken.Load(reader);
        }
    }
}
