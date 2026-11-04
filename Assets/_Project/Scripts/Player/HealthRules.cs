using UnityEngine;

namespace Nexo.Player
{
    /// <summary>Reglas puras de curación (testeables en EditMode).</summary>
    public static class HealthRules
    {
        /// <summary>
        /// Cada unidad de curación suma un punto de vida completo (sin pasar del máximo) y borra los golpes acumulados
        /// del punto actual. Ejemplo: 2/3 con 2 golpes + 1 → 3/3 sin golpes; 3/3 con 1 golpe + 1 → 3/3 sin golpes.
        /// </summary>
        public static void Heal(int points, int hits, int maxPoints, int amount, out int newPoints, out int newHits)
        {
            newPoints = Mathf.Clamp(points + Mathf.Max(0, amount), 1, Mathf.Max(1, maxPoints));
            newHits = 0;
        }
    }
}
