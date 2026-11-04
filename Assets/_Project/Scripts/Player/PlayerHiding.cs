using UnityEngine;

namespace Nexo.Player
{
    /// <summary>
    /// Estado compartido "el jugador está escondido" (taquilla, bajo una mesa). Lo escribe HidingSpot y lo lee
    /// SmartInfectedBrain: un infectado que no vio al jugador esconderse no puede verlo dentro del escondite.
    /// Se limpia al entrar en Play.
    /// </summary>
    public static class PlayerHiding
    {
        /// <summary>True mientras el jugador está dentro de un escondite.</summary>
        public static bool IsHidden { get; private set; }

        /// <summary>Momento (Time.time) en que se escondió.</summary>
        public static float HiddenSince { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsHidden = false;
            HiddenSince = 0f;
        }

        public static void SetHidden(bool hidden)
        {
            if (hidden && !IsHidden) HiddenSince = Time.time;
            IsHidden = hidden;
        }
    }
}
