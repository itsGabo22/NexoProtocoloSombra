using UnityEngine;

namespace Nexo.Core
{
    /// <summary>
    /// Estado persistente de la partida (munición, vida y equipo) entre pisos y reinicios.
    /// Vive en memoria estática: sobrevive a cargas/descargas de escena e incluso a recargar Systems_Persistent.
    /// - Al empezar un piso se guarda un checkpoint con lo que el jugador trae del piso anterior.
    /// - Si el jugador es capturado, al recargar el piso se restaura ese checkpoint (no se acumula munición).
    /// </summary>
    public static class GameSession
    {
        public static bool HasSnapshot { get; private set; }
        public static bool RestorePending { get; set; }
        /// <summary>El próximo piso que se cargue empieza una partida nueva (vida y munición iniciales).</summary>
        public static bool NewGamePending { get; set; }

        public static int Magazine { get; private set; }
        public static int Reserve { get; private set; }
        public static bool HasWeapon { get; private set; } = true;
        public static int HealthPoints { get; private set; }
        public static int HitCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            HasSnapshot = false;
            RestorePending = false;
            NewGamePending = false;
            HasWeapon = true;
        }

        public static void Save(int magazine, int reserve, bool hasWeapon, int healthPoints, int hitCount)
        {
            Magazine = magazine;
            Reserve = reserve;
            HasWeapon = hasWeapon;
            HealthPoints = healthPoints;
            HitCount = hitCount;
            HasSnapshot = true;
        }
    }
}
