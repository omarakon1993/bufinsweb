using System;
using Microsoft.Extensions.Configuration;

namespace bufinsweb.Helpers
{
    /// <summary>
    /// Lee la configuración del sitio. Prioridad: variable de entorno del servidor > appsettings.json / secretsSettings.json
    /// (desarrollo local). Se inicializa una vez en Program.cs con IConfiguration.
    /// </summary>
    public static class EnvironmentHelper
    {
        private static IConfiguration _config;

        public static void Iniciar(IConfiguration config) { _config = config; }

        private static string Read(string key)
        {
            return Environment.GetEnvironmentVariable(key)
                ?? (_config == null ? null : _config[key]);
        }

        public static string GetRequiredVariable(string key)
        {
            string value = Read(key);
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException(
                    $"La configuración '{key}' es requerida pero no está configurada. " +
                    $"En producción: defina la variable de entorno '{key}' en el servidor/IIS. " +
                    $"En desarrollo local: configúrela en secretsSettings.json."
                );
            return value;
        }

        public static string GetOptionalVariable(string key, string defaultValue = "")
        {
            string value = Read(key);
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
        }

        public static bool GetBooleanVariable(string key, bool defaultValue = false)
        {
            string value = Read(key);
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;
            return value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
        }

        public static int GetIntVariable(string key, int defaultValue = 0)
        {
            string value = Read(key);
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;
            return int.TryParse(value, out int result) ? result : defaultValue;
        }
    }
}
