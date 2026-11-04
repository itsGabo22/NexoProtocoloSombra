using UnityEngine;
using UnityEngine.UIElements;

namespace Nexo.Cinematics
{
    /// <summary>
    /// Ajustes de la capa de cinemáticas. Vive en Resources/Cinematics/CinematicSettings.asset porque
    /// CinematicService se crea por código (RuntimeInitializeOnLoadMethod) y no tiene referencias de escena.
    /// Si falta, el servicio crea un PanelSettings propio (el vídeo se ve igual; el aviso de salto puede no tener fuente).
    /// </summary>
    [CreateAssetMenu(fileName = "CinematicSettings", menuName = "Nexo/Cinematics/Settings")]
    public sealed class CinematicSettings : ScriptableObject
    {
        public const string ResourcePath = "Cinematics/CinematicSettings";

        [Tooltip("PanelSettings del proyecto (PS_Nexo): mismo escalado y tema que el HUD y los menús.")]
        [SerializeField] private PanelSettings _panelSettings;

        [Tooltip("Orden del UIDocument dentro del panel: por encima del HUD y de los menús (100).")]
        [SerializeField] private float _sortingOrder = 1000f;

        [Tooltip("Volumen del audio del clip (0-1); el volumen maestro de Opciones se aplica encima.")]
        [Range(0f, 1f)]
        [SerializeField] private float _volume = 1f;

        public PanelSettings PanelSettings => _panelSettings;
        public float SortingOrder => _sortingOrder;
        public float Volume => _volume;
    }
}
