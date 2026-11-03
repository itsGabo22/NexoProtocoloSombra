namespace Nexo.UI.Menus
{
    /// <summary>
    /// Textos de piso que muestran los menús y el HUD (lógica pura, probada en EditMode).
    /// El piso inicial es el 12, que no siempre publica FloorMetadata; por eso existe un valor por defecto.
    /// </summary>
    public static class FloorLabels
    {
        /// <summary>Piso con el que arranca JUGAR.</summary>
        public const int DefaultFloor = 12;

        /// <summary>
        /// Elige el piso a mostrar: el de FloorMetadata.Active si existe (&gt; 0), si no el último registrado por eventos,
        /// y si tampoco hay uno válido, el piso inicial.
        /// </summary>
        public static int Resolve(int metadataFloor, int trackedFloor)
        {
            if (metadataFloor > 0) return metadataFloor;
            return trackedFloor > 0 ? trackedFloor : DefaultFloor;
        }

        /// <summary>Nombre de la zona del piso, o cadena vacía si no se conoce.</summary>
        public static string ZoneName(int floor)
        {
            switch (floor)
            {
                case 12: return "OFICINAS";
                case 6: return "LABORATORIOS";
                case 1: return "VESTÍBULO";
                default: return string.Empty;
            }
        }

        /// <summary>Ubicación para la pausa: "PISO 6 · LABORATORIOS" (o "PISO n" si la zona no se conoce).</summary>
        public static string Location(int floor)
        {
            string zone = ZoneName(floor);
            return zone.Length == 0 ? $"PISO {floor}" : $"PISO {floor} · {zone}";
        }

        /// <summary>Subtítulo de la pantalla "Piso reiniciado": "PISO 6 REINICIADO".</summary>
        public static string Restarted(int floor)
        {
            return $"PISO {floor} REINICIADO";
        }

        /// <summary>Aplica el número de piso a una plantilla con {0}; sin plantilla devuelve la ubicación.</summary>
        public static string Format(string template, int floor)
        {
            if (string.IsNullOrEmpty(template)) return Location(floor);
            return string.Format(template, floor, ZoneName(floor));
        }
    }
}
