namespace Nexo.AI.Infected
{
    /// <summary>
    /// Perfil de comportamiento de un infectado. Cambia cómo patrulla y cuánto le cuesta reaccionar,
    /// para que no todos los encuentros se resuelvan igual.
    /// </summary>
    public enum InfectedBehavior
    {
        /// <summary>Recorre sus puntos de patrulla, se detiene y mira alrededor en cada uno.</summary>
        Patroller = 0,

        /// <summary>Espera inmóvil en su sala (de espaldas o mirando la puerta) y solo gira de vez en cuando.</summary>
        Sentinel = 1,

        /// <summary>Deambula despacio por puntos aleatorios cercanos a su posición inicial.</summary>
        Wanderer = 2,

        /// <summary>Oído muy fino: investiga cualquier ruido con más alcance y antes que los demás.</summary>
        Listener = 3
    }
}
