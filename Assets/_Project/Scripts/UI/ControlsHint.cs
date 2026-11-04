using UnityEngine;

namespace Nexo.UI
{
    /// <summary>
    /// Franja de ayuda de controles que se muestra una sola vez por sesión, al empezar a jugar.
    /// Teclas verificadas en código: R recargar y clic derecho apuntar (PlayerWeapon), F visión térmica
    /// (FlashlightSystem / NexoControls), V hablar con Elena (ElenaVoiceInput), C agacharse (PlayerMovement).
    /// </summary>
    public static class ControlsHint
    {
        /// <summary>Texto de la franja (mayúsculas solo en las teclas).</summary>
        public const string Text = "R recargar · clic der. apuntar · F visión térmica · V hablar con Elena · T escribir a Elena · C agacharse · Espacio saltar · G empujar · X cuchillo · J diario";

        /// <summary>Tiempo en pantalla antes de ocultarse sola.</summary>
        public const long VisibleMs = 9000;

        private static bool _shown;

        /// <summary>Devuelve true solo la primera vez en la sesión (los reinicios de piso no la repiten).</summary>
        public static bool TryConsume()
        {
            if (_shown) return false;
            _shown = true;
            return true;
        }

        /// <summary>Vuelve a permitir la franja (inicio de sesión y pruebas).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetSession()
        {
            _shown = false;
        }
    }
}
