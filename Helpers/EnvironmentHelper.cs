using System;
using System.Configuration;

namespace bufinsweb.Helpers
{
    /// <summary>
    /// Helper para leer configuración desde Web.config de forma segura
    /// </summary>
    public static class EnvironmentHelper
    {
        /// <summary>
        /// Obtiene una configuración requerida desde Web.config AppSettings
        /// </summary>
        /// <param name="key">Nombre de la clave en AppSettings</param>
        /// <returns>Valor de la configuración</returns>
        /// <exception cref="InvalidOperationException">Si la configuración no existe o está vacía</exception>
        public static string GetRequiredVariable(string key)
        {
            string value = ConfigurationManager.AppSettings[key];

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"La configuración '{key}' es requerida pero no está configurada en Web.config. " +
                    "Por favor, agréguela en la sección <appSettings> del archivo Web.config."
                );
            }

            return value;
        }

        /// <summary>
        /// Obtiene una configuración opcional con valor por defecto
        /// </summary>
        /// <param name="key">Nombre de la clave en AppSettings</param>
        /// <param name="defaultValue">Valor por defecto si no existe</param>
        /// <returns>Valor de la configuración o el valor por defecto</returns>
        public static string GetOptionalVariable(string key, string defaultValue = "")
        {
            string value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
        }

        /// <summary>
        /// Obtiene una configuración booleana
        /// </summary>
        /// <param name="key">Nombre de la clave en AppSettings</param>
        /// <param name="defaultValue">Valor por defecto si no existe</param>
        /// <returns>Valor booleano</returns>
        public static bool GetBooleanVariable(string key, bool defaultValue = false)
        {
            string value = ConfigurationManager.AppSettings[key];

            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;

            return value.ToLower() == "true" || value == "1";
        }

        /// <summary>
        /// Obtiene una configuración entera
        /// </summary>
        /// <param name="key">Nombre de la clave en AppSettings</param>
        /// <param name="defaultValue">Valor por defecto si no existe</param>
        /// <returns>Valor entero</returns>
        public static int GetIntVariable(string key, int defaultValue = 0)
        {
            string value = ConfigurationManager.AppSettings[key];

            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;

            if (int.TryParse(value, out int result))
                return result;

            return defaultValue;
        }
    }
}
