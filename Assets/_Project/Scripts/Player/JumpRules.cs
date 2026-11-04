using UnityEngine;

namespace Nexo.Player
{
    /// <summary>
    /// Reglas puras del salto de Alex (testeables en EditMode): cuándo se permite y con qué velocidad despega.
    /// </summary>
    public static class JumpRules
    {
        /// <summary>
        /// Solo se salta con los pies en el suelo, con el control libre (sin UI, forcejeo ni cinemática), de pie,
        /// fuera de un escondite y pasado el margen tras recuperar el control (evita reutilizar el Espacio del forcejeo
        /// o del salto de cinemática).
        /// </summary>
        public static bool CanJump(bool grounded, bool inputLocked, bool crouching, bool hidden, float secondsSinceUnlock, float unlockGrace)
        {
            return grounded && !inputLocked && !crouching && !hidden && secondsSinceUnlock >= unlockGrace;
        }

        /// <summary>Velocidad vertical inicial para alcanzar <paramref name="height"/> metros con la gravedad dada (negativa).</summary>
        public static float TakeOffSpeed(float height, float gravity)
        {
            return Mathf.Sqrt(Mathf.Max(0f, height) * 2f * Mathf.Abs(gravity));
        }
    }
}
