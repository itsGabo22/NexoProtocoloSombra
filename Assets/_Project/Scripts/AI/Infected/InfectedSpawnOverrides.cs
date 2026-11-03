using System;
using UnityEngine;

namespace Nexo.AI.Infected
{
    /// <summary>
    /// Ajustes de un infectado concreto definidos en su punto de aparición. Permiten reutilizar el mismo prefab
    /// (PF_Infected_Variant) con roles distintos en cada piso sin crear variantes. Desactivado por defecto:
    /// los puntos existentes del Piso 12 conservan el comportamiento del prefab.
    /// </summary>
    [Serializable]
    public sealed class InfectedSpawnOverrides
    {
        [Tooltip("Aplica estos ajustes al instanciar el infectado.")]
        public bool Enabled;

        public InfectedBehavior Behavior = InfectedBehavior.Patroller;
        [Tooltip("Detección gradual por vista (sigilo).")]
        public bool GradedDetection = true;
        public bool MimicryEnabled;
        public bool SabotageEnabled;
        [Tooltip("Añade InfectedLearningMemory: aprende de lo que oye y ve.")]
        public bool EnableLearning;

        [Tooltip("0 = valor del prefab.")]
        public int MaxHealth;
        [Tooltip("0 = valor del prefab.")]
        public float SightDistance;
        [Tooltip("0 = valor del prefab.")]
        public float HearingRange;
        [Tooltip("0 = valor del prefab.")]
        public float MimicryCooldown;

        [TextArea(1, 3)] public string[] MimicMessages = Array.Empty<string>();
    }
}
