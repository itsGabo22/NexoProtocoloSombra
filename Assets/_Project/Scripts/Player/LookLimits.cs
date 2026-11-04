using UnityEngine;

namespace Nexo.Player
{
    /// <summary>Límites de la mirada (escondite): recorta un ángulo a ±límite. Lógica pura.</summary>
    public static class LookLimits
    {
        public const float HidingYaw = 70f;
        public const float HidingPitch = 35f;

        public static float Clamp(float angle, float limit)
        {
            float l = Mathf.Abs(limit);
            return Mathf.Clamp(angle, -l, l);
        }
    }
}
