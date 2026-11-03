namespace Nexo.UI
{
    /// <summary>
    /// Nombres en español de las puertas (DoorId de DoorController) para los textos que ve el jugador
    /// (lógica pura, probada en EditMode). Los ids técnicos nunca deben aparecer en pantalla.
    /// Ids reales del repositorio: F12_Door_Stairs, F12_Door_Meeting, F06_Door_Stairs, F06_Door_Lab.
    /// </summary>
    public static class DoorDisplayNames
    {
        /// <summary>Destino genérico cuando el id está vacío o no se conoce.</summary>
        public const string UnknownDestination = "una puerta";

        /// <summary>Etiqueta genérica de la terminal cuando el id no se conoce.</summary>
        public const string UnknownLabel = "Puerta";

        /// <summary>
        /// Destino en minúsculas con artículo, para frases: «Ruta despejada hacia la puerta de las escaleras».
        /// </summary>
        public static string Destination(string doorId)
        {
            switch (Normalize(doorId))
            {
                case "f12_door_stairs":
                case "f06_door_stairs":
                    return "la puerta de las escaleras";
                case "f06_door_lab":
                    return "el laboratorio";
                case "f12_door_meeting":
                    return "la sala de reuniones";
                default:
                    return UnknownDestination;
            }
        }

        /// <summary>Nombre corto con mayúscula inicial para listas (terminal de seguridad).</summary>
        public static string Label(string doorId)
        {
            switch (Normalize(doorId))
            {
                case "f12_door_stairs":
                case "f06_door_stairs":
                    return "Puerta de las escaleras";
                case "f06_door_lab":
                    return "Puerta del laboratorio";
                case "f12_door_meeting":
                    return "Sala de reuniones";
                default:
                    return UnknownLabel;
            }
        }

        private static string Normalize(string doorId)
        {
            return string.IsNullOrWhiteSpace(doorId) ? string.Empty : doorId.Trim().ToLowerInvariant();
        }
    }
}
