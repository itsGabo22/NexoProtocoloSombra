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

        public static void RaiseBioScannerHandPlaced() => BioScannerHandPlaced?.Invoke();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAll()
        {
            BioScannerHandPlaced = null;
            TwistHasPlayed = false;
        }
    }
}
