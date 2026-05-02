using System;
using System.Configuration;

namespace bufinsweb.Helpers
{
    public static class EnvironmentHelper
    {
        // Prioridad: variable de entorno del sistema (servidor) > appSettings / secretsSettings.config (desarrollo local)
        private static string Read(string key)
        {
            return Environment.GetEnvironmentVariable(key)
                ?? ConfigurationManager.AppSettings[key];
        }

        public static string GetRequiredVariable(string key)
        {
            string value = Read(key);

            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException(
                    $"La configuración '{key}' es requerida pero no está configurada. " +
                    $"En producción: defina la variable de entorno '{key}' en el servidor/IIS. " +
                    $"En desarrollo local: configúrela en secretsSettings.config."
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
