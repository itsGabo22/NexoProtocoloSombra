using UnityEngine;

namespace Nexo.Player
{
    /// <summary>
    /// Reglas puras del cuerpo a cuerpo (empujón [G] y cuchillo [X]), testeadas en EditMode.
    /// </summary>
    public static class MeleeRules
    {
        public const float ShoveCooldown = 1.5f;
        public const float ShoveRange = 2.1f;
        public const float ShoveHalfAngle = 55f;
        public const float ShoveStagger = 1.2f;
        public const float ShovePushMeters = 1.3f;
        public const float ShoveNoise = 30f;

        public const float KnifeCooldown = 0.55f;
        public const float KnifeRange = 1.8f;
        public const float KnifeHalfAngle = 35f;
        public const float KnifeNoise = 8f;
        /// <summary>Ángulo mínimo entre la mirada del infectado y la dirección hacia el jugador para contar «por la espalda».</summary>
        public const float BehindAngle = 110f;

        public static bool Ready(float now, float lastUse, float cooldown) => now - lastUse >= cooldown;

        /// <summary>Progreso del enfriamiento 0-1 (1 = listo).</summary>
        public static float CooldownProgress(float now, float lastUse, float cooldown)
        {
            if (cooldown <= 0f) return 1f;
            return Mathf.Clamp01((now - lastUse) / cooldown);
        }

        /// <summary>True si el objetivo está delante del jugador, dentro del alcance y del cono indicado (plano XZ).</summary>
        public static bool InCone(Vector3 origin, Vector3 forward, Vector3 target, float range, float halfAngle)
        {
            Vector3 to = target - origin;
            to.y = 0f;
            forward.y = 0f;
            if (to.sqrMagnitude > range * range) return false;
            if (to.sqrMagnitude < 0.0001f) return true;
            return Vector3.Angle(forward, to) <= halfAngle;
        }

        /// <summary>
        /// Ejecución sigilosa: el infectado no ha detectado al jugador y este le ataca por la espalda
        /// (el ángulo entre su mirada y la dirección hacia el jugador supera BehindAngle).
        /// </summary>
        public static bool IsStealthTakedown(bool infectedAware, Vector3 infectedForward, Vector3 infectedPos, Vector3 playerPos)
        {
            if (infectedAware) return false;
            Vector3 toPlayer = playerPos - infectedPos;
            toPlayer.y = 0f;
            infectedForward.y = 0f;
            if (toPlayer.sqrMagnitude < 0.0001f || infectedForward.sqrMagnitude < 0.0001f) return false;
            return Vector3.Angle(infectedForward, toPlayer) >= BehindAngle;
        }
    }
}
