using System;

namespace Nexo.Cinematics
{
    /// <summary>
    /// Ids de las cinemáticas y resolución del clip de transición entre pisos (lógica pura, probada en EditMode).
    /// Cada id corresponde a StreamingAssets/Cinematics/&lt;id&gt;.mp4.
    /// </summary>
    public static class CinematicIds
    {
        /// <summary>Al pulsar JUGAR (partida nueva), antes de que empiece el Piso 12.</summary>
        public const string Intro = "intro";

        /// <summary>Transición genérica entre pisos (se usa si no existe la específica del piso destino).</summary>
        public const string FloorTransition = "floor_transition";

        /// <summary>Prefijo de la transición específica: floor_transition_to_06, floor_transition_to_01...</summary>
        public const string FloorTransitionToPrefix = "floor_transition_to_";

        /// <summary>Final: tras la última línea del giro y antes de los créditos.</summary>
        public const string Ending = "ending";

        /// <summary>Clips obligatorios para la entrega.</summary>
        public static readonly string[] Required = { Intro, FloorTransition, Ending };

        /// <summary>Clips opcionales (sustituyen a la transición genérica en ese piso).</summary>
        public static readonly string[] Optional = { FloorTransitionToPrefix + "06", FloorTransitionToPrefix + "01" };

        /// <summary>Id específico de la transición al piso indicado (dos dígitos: 6 → floor_transition_to_06). Vacío si el piso no es válido.</summary>
        public static string ForFloor(int floorNumber)
        {
            return floorNumber > 0 ? FloorTransitionToPrefix + floorNumber.ToString("00") : string.Empty;
        }

        /// <summary>
        /// Clip de transición al piso indicado: el específico si existe (clipExists), si no el genérico.
        /// Con clipExists nulo devuelve siempre el genérico.
        /// </summary>
        public static string ResolveFloorTransition(int floorNumber, Func<string, bool> clipExists)
        {
            string specific = ForFloor(floorNumber);
            if (specific.Length > 0 && clipExists != null && clipExists(specific)) return specific;
            return FloorTransition;
        }
    }
}
