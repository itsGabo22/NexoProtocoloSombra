using Nexo.Core;
using Nexo.UI;
using UnityEngine;

namespace Nexo.Interaction
{
    /// <summary>
    /// Susto ligado a la red de cámaras: una silueta aparece en la sala solo cuando el jugador la mira por la cámara
    /// indicada y desaparece en cuanto el jugador entra en la sala (este trigger). De un solo uso.
    /// La silueta es un objeto de la escena desactivado por defecto; no tiene colisión ni IA.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class CameraFeedApparition : MonoBehaviour
    {
        [SerializeField] private string _feedId = "F06-03";
        [SerializeField] private GameObject _silhouette;
        [SerializeField] private string _vanishSubtitle = "(La sala está vacía. En el suelo, huellas húmedas que se cortan en seco)";

        private bool _shown;
        private bool _done;

        private void Reset() => GetComponent<Collider>().isTrigger = true;

        private void Awake()
        {
            if (_silhouette != null) _silhouette.SetActive(false);
        }

        private void OnEnable() => CameraNetworkController.FeedViewed += HandleFeedViewed;

        private void OnDisable() => CameraNetworkController.FeedViewed -= HandleFeedViewed;

        private void HandleFeedViewed(string feedId)
        {
            if (_done || _shown || feedId != _feedId) return;
            _shown = true;
            if (_silhouette != null) _silhouette.SetActive(true);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_done || other.gameObject.layer != LayerMask.NameToLayer("Player")) return;
            _done = true;
            if (_silhouette != null) _silhouette.SetActive(false);
            if (_shown && !string.IsNullOrEmpty(_vanishSubtitle)) GameEvents.RaiseSubtitle(_vanishSubtitle);
        }
    }
}
