using UnityEngine;

namespace Nexo.Core
{
    /// <summary>
    /// Estado global «el jugador está escribiendo» (cuadro de texto para Elena, [T]). Mientras está activo, y durante el
    /// frame en que se cierra, las teclas rápidas que no dependen del bloqueo del jugador (linterna F, preguntas 1-3,
    /// objetivo Tab, interactuar E, pausa Esc/P) se ignoran para que escribir no dispare acciones.
    /// </summary>
    public static class UiTextEntry
    {
        public static bool IsActive { get; private set; }
        private static int s_closedFrame = -10;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsActive = false;
            s_closedFrame = -10;
        }

        /// <summary>True si las teclas rápidas deben ignorarse ahora.</summary>
        public static bool BlocksHotkeys => IsActive || Time.frameCount - s_closedFrame <= 1;

        public static void SetActive(bool active)
        {
            if (IsActive && !active) s_closedFrame = Time.frameCount;
            IsActive = active;
        }
    }
}
