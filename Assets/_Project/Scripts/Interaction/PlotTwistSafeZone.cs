using Nexo.Core;
using UnityEngine;

namespace Nexo.Interaction
{
    /// <summary>
    /// Protege el giro final: al dispararse OnPlotTwistTriggered desactiva todo lo que cuelga de este objeto
    /// (infectados de ambientación, sustos, audio propio del vestíbulo) para que nada interfiera con la secuencia
    /// de PlotTwistManager. No toca ningún objeto fuera de su jerarquía.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlotTwistSafeZone : MonoBehaviour
    {
        [Tooltip("Hijos que se desactivan al empezar el giro (si está vacío, todos los hijos).")]
        [SerializeField] private GameObject[] _disableOnTwist;

        private void OnEnable() => GameEvents.OnPlotTwistTriggered += HandlePlotTwist;

        private void OnDisable() => GameEvents.OnPlotTwistTriggered -= HandlePlotTwist;

        private void HandlePlotTwist()
        {
            if (_disableOnTwist != null && _disableOnTwist.Length > 0)
            {
                foreach (GameObject go in _disableOnTwist) if (go != null) go.SetActive(false);
                return;
            }
            foreach (Transform child in transform) child.gameObject.SetActive(false);
        }
    }
}
