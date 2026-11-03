namespace Nexo.UI
{
    /// <summary>
    /// Decisiones de presentación del panel de arma del HUD (lógica pura, sin UnityEngine, probada en EditMode).
    /// HUDController solo traduce el resultado a textos y clases USS.
    /// </summary>
    public static class AmmoDisplayState
    {
        /// <summary>Nombre del arma que se muestra en el panel.</summary>
        public const string WeaponName = "M1911";

        /// <summary>Con este número de balas o menos en el cargador el panel pasa a aviso.</summary>
        public const int LowThreshold = 2;

        /// <summary>Máximo de celdas dibujadas para el cargador (protege el layout ante tamaños absurdos).</summary>
        public const int MaxCells = 12;

        /// <summary>
        /// Estado del panel. La recarga tiene prioridad; después cargador vacío (con o sin reserva) y munición baja.
        /// </summary>
        public static AmmoStatus Resolve(int magazine, int reserve, int magazineSize, bool reloading)
        {
            if (reloading) return AmmoStatus.Reloading;

            int mag = ClampMagazine(magazine, magazineSize);
            int res = reserve < 0 ? 0 : reserve;

            if (mag == 0) return res > 0 ? AmmoStatus.Empty : AmmoStatus.Depleted;
            return mag <= LowThreshold ? AmmoStatus.Low : AmmoStatus.Normal;
        }

        /// <summary>Contador grande «cargador / reserva», p. ej. «7 / 21».</summary>
        public static string Counter(int magazine, int reserve, int magazineSize)
        {
            int res = reserve < 0 ? 0 : reserve;
            return $"{ClampMagazine(magazine, magazineSize)} / {res}";
        }

        /// <summary>Texto de estado bajo el contador (vacío en estado normal).</summary>
        public static string StatusText(AmmoStatus status)
        {
            switch (status)
            {
                case AmmoStatus.Low: return "MUNICIÓN BAJA";
                case AmmoStatus.Empty: return "RECARGAR [R]";
                case AmmoStatus.Depleted: return "SIN MUNICIÓN";
                case AmmoStatus.Reloading: return "RECARGANDO…";
                default: return string.Empty;
            }
        }

        /// <summary>Número de celdas del cargador a dibujar (entre 1 y MaxCells).</summary>
        public static int CellCount(int magazineSize)
        {
            if (magazineSize < 1) return 1;
            return magazineSize > MaxCells ? MaxCells : magazineSize;
        }

        /// <summary>Cuántas celdas se pintan llenas para el cargador actual.</summary>
        public static int FilledCells(int magazine, int magazineSize)
        {
            int cells = CellCount(magazineSize);
            int mag = ClampMagazine(magazine, magazineSize);
            return mag > cells ? cells : mag;
        }

        /// <summary>Texto del destello al recoger munición: «+7». Cadena vacía si no se recogió nada.</summary>
        public static string PickupText(int rounds)
        {
            return rounds > 0 ? $"+{rounds}" : string.Empty;
        }

        /// <summary>Clase USS de modificador para cada estado (BEM sobre hud-weapon).</summary>
        public static string StatusClass(AmmoStatus status)
        {
            switch (status)
            {
                case AmmoStatus.Low: return "hud-weapon--low";
                case AmmoStatus.Empty: return "hud-weapon--empty";
                case AmmoStatus.Depleted: return "hud-weapon--depleted";
                case AmmoStatus.Reloading: return "hud-weapon--reloading";
                default: return string.Empty;
            }
        }

        private static int ClampMagazine(int magazine, int magazineSize)
        {
            if (magazine < 0) return 0;
            return magazineSize > 0 && magazine > magazineSize ? magazineSize : magazine;
        }
    }
}
