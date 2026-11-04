namespace Nexo.UI
{
    /// <summary>
    /// Nombres en español de los ids internos (puertas, salas y objetos) para los textos que ve el jugador
    /// (lógica pura, probada en EditMode). Los ids técnicos nunca deben aparecer en pantalla.
    /// Ids reales del repositorio: puertas F12_Door_Stairs, F12_Door_Meeting, F06_Door_Stairs, F06_Door_Lab (en el Piso 6
    /// reconstruido es la puerta de Contención);
    /// salas (RoomId) F12_Cubicles, F12_Cubicles_East, F12_ServerRoom; F01_BioScanner y F12_Terminal_Security.
    /// </summary>
    public static class DoorDisplayNames
    {
        /// <summary>Destino genérico cuando el id está vacío o es una puerta desconocida.</summary>
        public const string UnknownDestination = "una puerta";

        /// <summary>Destino genérico cuando el id desconocido no es una puerta.</summary>
        public const string UnknownPlace = "ese lugar";

        /// <summary>Etiqueta genérica de la terminal cuando el id no se conoce.</summary>
        public const string UnknownLabel = "Puerta";

        /// <summary>
        /// Destino en minúsculas con artículo, para frases: «Ruta despejada hacia la puerta de las escaleras».
        /// Id vacío o puerta desconocida → «una puerta»; cualquier otro id desconocido → «ese lugar».
        /// </summary>
        public static string Destination(string id)
        {
            if (TryGetName(id, out string name)) return name;
            string key = Normalize(id);
            return key.Length == 0 || IsDoorId(key) ? UnknownDestination : UnknownPlace;
        }

        /// <summary>Nombre conocido de un id (puerta, sala u objeto). Devuelve false si el id no está en el mapa.</summary>
        public static bool TryGetName(string id, out string name)
        {
            switch (Normalize(id))
            {
                case "f12_door_stairs":
                case "f06_door_stairs":
                    name = "la puerta de las escaleras";
                    return true;
                case "f06_door_lab":
                    name = "la sala de contención";
                    return true;
                case "f12_door_meeting":
                    name = "la sala de reuniones";
                    return true;
                case "f12_cubicles":
                    name = "los cubículos";
                    return true;
                case "f12_cubicles_east":
                    name = "los cubículos del ala este";
                    return true;
                case "f12_serverroom":
                    name = "la sala de servidores";
                    return true;
                case "f01_bioscanner":
                    name = "el escáner maestro";
                    return true;
                case "f12_terminal_security":
                    name = "la terminal de seguridad";
                    return true;
                default:
                    name = string.Empty;
                    return false;
            }
        }

        /// <summary>
        /// Complemento que sigue a «Puerta» en una frase: «Puerta de las escaleras desbloqueada».
        /// Cadena vacía si la puerta no se conoce.
        /// </summary>
        public static string DoorQualifier(string doorId)
        {
            switch (Normalize(doorId))
            {
                case "f12_door_stairs":
                case "f06_door_stairs":
                    return "de las escaleras";
                case "f06_door_lab":
                    return "de Contención";
                case "f12_door_meeting":
                    return "de la sala de reuniones";
                default:
                    return string.Empty;
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
                    return "Puerta de Contención";
                case "f12_door_meeting":
                    return "Sala de reuniones";
                default:
                    return UnknownLabel;
            }
        }

        /// <summary>True si el id es de una puerta (contiene «_Door_»).</summary>
        public static bool IsDoorId(string id)
        {
            return Normalize(id).Contains("_door_");
        }

        private static string Normalize(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim().ToLowerInvariant();
        }
    }
}
