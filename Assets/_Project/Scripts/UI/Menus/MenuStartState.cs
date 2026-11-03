using UnityEngine;

namespace Nexo.UI.Menus
{
    /// <summary>
    /// Estado de arranque que sobrevive a la recarga de Systems_Persistent (estático).
    /// SkipBootOnce lo activa MenuGameFlowBridge tras el giro final; MenuFlowController lo consume en Start
    /// para ir directo al menú principal sin repetir la secuencia de arranque.
    /// </summary>
    public static class MenuStartState
    {
        public static bool SkipBootOnce { get; set; }

        /// <summary>Devuelve true una sola vez si había que saltar el arranque y limpia el indicador.</summary>
        public static bool ConsumeSkipBoot()
        {
            bool skip = SkipBootOnce;
            SkipBootOnce = false;
            return skip;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAll()
        {
            SkipBootOnce = false;
        }
    }
}
