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

        public static void RaiseBioScannerHandPlaced() => BioScannerHandPlaced?.Invoke();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAll()
        {
            BioScannerHandPlaced = null;
        }
    }
}
