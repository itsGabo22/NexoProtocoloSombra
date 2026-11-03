using System.Collections;
using Nexo.Core;
using UnityEngine;

namespace Nexo.Interaction
{
    /// <summary>
    /// Diferencial de una sala (puzle de energía del Piso 6). Cuando la sala se queda a oscuras por primera vez
    /// (apagón del guion o sabotaje) su interruptor pasa a "sin corriente". El jugador puede restablecerla aquí:
    /// tarda unos segundos, hace mucho ruido (el motor arranca) y atrae a los infectados cercanos.
    /// Elegir entre luz y sigilo es la decisión del puzle. Sin Update: evento de luces y una corrutina.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PowerBreaker : MonoBehaviour, IInteractable
    {
        [SerializeField] private LightSwitchInteractable _target;
        [SerializeField] private string _roomName = "el laboratorio B";
        [SerializeField] private float _restoreTime = 3f;
        [SerializeField, Range(0f, 100f)] private float _noise = 70f;
        [SerializeField] private AudioSource _source;
        [SerializeField] private AudioClip _crankClip;
        [SerializeField] private AudioClip _restoreClip;
        [SerializeField] private Renderer _statusLamp;
        [SerializeField] private Color _offColor = new Color(1f, 0.15f, 0.1f);
        [SerializeField] private Color _onColor = new Color(0.2f, 1f, 0.45f);

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private bool _armed;
        private bool _restoring;
        private Coroutine _routine;

        public string Prompt
        {
            get
            {
                if (_restoring) return "Restableciendo energía...";
                return _armed ? $"[E] Restablecer la energía de {_roomName} (ruidoso)" : "Diferencial · en servicio";
            }
        }

        private void OnEnable() => GameEvents.OnLightsChanged += HandleLightsChanged;

        private void OnDisable()
        {
            GameEvents.OnLightsChanged -= HandleLightsChanged;
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            _restoring = false;
        }

        private void Start() => SetLamp(true);

        private void HandleLightsChanged(string roomId, bool isOn)
        {
            if (_target == null || isOn || _armed || _restoring || roomId != _target.RoomId) return;

            // Primer corte de la sala: el interruptor queda sin corriente hasta usar el diferencial
            _armed = true;
            _target.PowerCut = true;
            SetLamp(false);
        }

        public void Interact()
        {
            if (!_armed || _restoring || _target == null) return;
            _routine = StartCoroutine(RestoreRoutine());
        }

        private IEnumerator RestoreRoutine()
        {
            _restoring = true;
            if (_source != null && _crankClip != null) _source.PlayOneShot(_crankClip, 0.9f);
            GameEvents.RaiseSubtitle("(Fuerzas la palanca del diferencial: el motor arranca con un estruendo)");
            // El ruido se repite mientras arranca: cualquiera cerca lo oye
            for (float t = 0f; t < _restoreTime; t += 1f)
            {
                GameEvents.RaiseNoiseEmitted(transform.position, _noise);
                yield return new WaitForSeconds(1f);
            }

            _target.PowerCut = false;
            _target.SetPower(true);
            if (_source != null && _restoreClip != null) _source.PlayOneShot(_restoreClip, 0.8f);
            SetLamp(true);
            StorySequence.SetObjective($"Energía restablecida en {_roomName}. Sal antes de que acudan al ruido.");
            _armed = false;
            _restoring = false;
            _routine = null;
        }

        private void SetLamp(bool on)
        {
            if (_statusLamp == null) return;
            Color color = on ? _onColor : _offColor;
            _statusLamp.material.SetColor(EmissionColorId, color * 2f);
            _statusLamp.material.color = color * 0.5f;
        }
    }
}
