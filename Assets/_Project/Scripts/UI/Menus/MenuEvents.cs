using System;
using UnityEngine;

namespace Nexo.UI.Menus
{
    /// <summary>
    /// Bus de eventos de los menús (separado de Nexo.Core.GameEvents para no modificarlo).
    /// MenuGameFlowBridge traduce estas peticiones a SceneLoader; PauseMenuController atiende ResumeRequested.
    /// </summary>
    public static class MenuEvents
    {
        public static event Action PlayRequested;
        public static event Action ResumeRequested;
        public static event Action RestartFloorRequested;
        public static event Action ReturnToMainMenuRequested;
        public static event Action QuitRequested;

        /// <summary>
        /// Pide mostrar la pantalla "Piso reiniciado". Uso desde otro script cuando el jugador es capturado:
        /// <code>
        /// private void OnEnable()  { GameEvents.OnPlayerCaught += HandlePlayerCaught; }
        /// private void OnDisable() { GameEvents.OnPlayerCaught -= HandlePlayerCaught; }
        /// private void HandlePlayerCaught() { MenuEvents.RaiseShowFloorRestartedRequested(); }
        /// </code>
        /// Hoy la abre MenuGameFlowBridge cuando PlayerCaptureHandler ya recargó el piso tras la captura:
        /// REINTENTAR solo reanuda (el checkpoint ya está restaurado) y MENÚ PRINCIPAL vuelve al hub.
        /// </summary>
        public static event Action ShowFloorRestartedRequested;

        /// <summary>
        /// Pide la pantalla de créditos (tema hive). La emite Nexo.Narrative.PlotTwistManager al final del giro.
        /// </summary>
        public static event Action ShowCreditsRequested;

        public static void RaisePlayRequested() => PlayRequested?.Invoke();
        public static void RaiseResumeRequested() => ResumeRequested?.Invoke();
        public static void RaiseRestartFloorRequested() => RestartFloorRequested?.Invoke();
        public static void RaiseReturnToMainMenuRequested() => ReturnToMainMenuRequested?.Invoke();
        public static void RaiseQuitRequested() => QuitRequested?.Invoke();
        public static void RaiseShowFloorRestartedRequested() => ShowFloorRestartedRequested?.Invoke();
        public static void RaiseShowCreditsRequested() => ShowCreditsRequested?.Invoke();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAll()
        {
            PlayRequested = null;
            ResumeRequested = null;
            RestartFloorRequested = null;
            ReturnToMainMenuRequested = null;
            QuitRequested = null;
            ShowFloorRestartedRequested = null;
            ShowCreditsRequested = null;
        }
    }
}
