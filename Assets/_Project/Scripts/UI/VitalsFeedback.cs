namespace Nexo.UI
{
    /// <summary>
    /// Interpreta los vitales que publica PlayerHealth («67% HERIDO») para el feedback de daño del HUD
    /// (lógica pura, probada en EditMode). PlayerHealth no expone eventos de daño propios: un descenso del
    /// porcentaje entre dos OnVitalsChanged es un golpe recibido.
    /// </summary>
    public static class VitalsFeedback
    {
        /// <summary>Por debajo o igual a este porcentaje se considera salud baja (último punto de vida de 3).</summary>
        public const int LowHealthPercent = 34;

        /// <summary>Valor que indica que todavía no se conoce el porcentaje anterior.</summary>
        public const int Unknown = -1;

        /// <summary>Lee el porcentaje inicial de «67% HERIDO». Devuelve false si el texto no empieza por un número.</summary>
        public static bool TryParsePercent(string valueText, out int percent)
        {
            percent = 0;
            if (string.IsNullOrEmpty(valueText)) return false;

            int i = 0;
            while (i < valueText.Length && valueText[i] == ' ') i++;

            int start = i;
            int value = 0;
            while (i < valueText.Length && valueText[i] >= '0' && valueText[i] <= '9' && i - start < 4)
            {
                value = value * 10 + (valueText[i] - '0');
                i++;
            }

            if (i == start || i >= valueText.Length || valueText[i] != '%') return false;

            percent = value > 100 ? 100 : value;
            return true;
        }

        /// <summary>Hay daño si se conocía un porcentaje anterior y el nuevo es menor.</summary>
        public static bool IsDamage(int previousPercent, int currentPercent)
        {
            return previousPercent != Unknown && currentPercent < previousPercent;
        }

        /// <summary>Salud baja: activa el pulso lento de la viñeta roja.</summary>
        public static bool IsLowHealth(int percent)
        {
            return percent >= 0 && percent <= LowHealthPercent;
        }
    }
}
