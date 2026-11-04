using System;
using UnityEngine;

namespace Nexo.Narrative
{
    /// <summary>
    /// Bus de eventos narrativos (separado de Nexo.Core.GameEvents para no modificarlo).
    /// </summary>
    public static class NarrativeEvents
    {
        /// <summary>El jugador puso la mano en el BioScanner maestro (Piso 1): arranca el giro final.</summary>
        public static event Action BioScannerHandPlaced;

        /// <summary>
        /// True desde que arranca el giro final. MenuGameFlowBridge lo usa para recargar Systems_Persistent al volver
        /// al menú (los bloqueos del giro en jugador, linterna, HUD y rueda no se reinician de otro modo).
        /// </summary>
        public static bool TwistHasPlayed { get; set; }

        /// <summary>
        /// FloorProgression terminó de bajar a otro piso (número de piso: 6, 1...; -1 si la escena no sigue el patrón FloorNN_).
        /// </summary>
        public static event Action<int> FloorChanged;

        /// <summary>El jugador lee una nota (autor, texto): NoteReader la muestra como papel a pantalla completa.</summary>
        public static event Action<string, string> NoteRead;

        public static void RaiseBioScannerHandPlaced() => BioScannerHandPlaced?.Invoke();
        public static void RaiseFloorChanged(int floorNumber) => FloorChanged?.Invoke(floorNumber);
        public static void RaiseNoteRead(string author, string body) => NoteRead?.Invoke(author, body);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAll()
        {
            BioScannerHandPlaced = null;
            FloorChanged = null;
            NoteRead = null;
            TwistHasPlayed = false;
        }
    }
}
