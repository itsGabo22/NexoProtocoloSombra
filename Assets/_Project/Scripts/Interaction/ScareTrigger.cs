using Nexo.Core;
using UnityEngine;

namespace Nexo.Interaction
{
    /// <summary>
    /// Susto de un solo uso: cuando el jugador entra en el volumen (trigger) reproduce un sonido en un punto
    /// del piso, puede cortar la luz de un cuarto y muestra un subtítulo descriptivo (accesibilidad).
    /// El sonido también se emite como ruido para que el infectado pueda acudir a investigarlo.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class ScareTrigger : MonoBehaviour
    {
        [Header("Sonido")]
        [SerializeField] private AudioSource _source;
        [SerializeField] private AudioClip _clip;
        [SerializeField, Range(0f, 1f)] private float _volume = 1f;

        [Header("Efectos opcionales")]
        [SerializeField] private LightSwitchInteractable _cutPower;
        [SerializeField] private string _subtitle = "";
        [SerializeField, Range(0f, 100f)] private float _noiseLoudness = 0f;

        private bool _fired;

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_fired || other.gameObject.layer != LayerMask.NameToLayer("Player")) return;
            _fired = true;

            if (_source != null && _clip != null) _source.PlayOneShot(_clip, _volume);
            if (_cutPower != null && _cutPower.IsOn) _cutPower.SetPower(false);
            if (!string.IsNullOrEmpty(_subtitle)) GameEvents.RaiseSubtitle(_subtitle);
            if (_noiseLoudness > 0f && _source != null) GameEvents.RaiseNoiseEmitted(_source.transform.position, _noiseLoudness);
        }
    }
}
