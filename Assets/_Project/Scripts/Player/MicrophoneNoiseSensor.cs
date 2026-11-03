using System.Collections;
using Nexo.Core;
using UnityEngine;

namespace Nexo.Player
{
    /// <summary>
    /// Escucha el micrófono real del jugador: si habla (o grita) cerca del micro, su voz se convierte en ruido
    /// dentro del juego (GameEvents.OnNoiseEmitted) y los infectados acuden a investigar.
    /// Muestrea el nivel RMS cada _sampleInterval segundos mediante corrutina (sin Update()).
    /// Si no hay micrófono o está desactivado en el Inspector, el componente no hace nada.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MicrophoneNoiseSensor : MonoBehaviour
    {
        private const int SampleRate = 16000;

        [SerializeField] private bool _enabledByDefault = true;
        [SerializeField] private float _sampleInterval = 0.15f;

        [Header("Calibración (dBFS)")]
        [Tooltip("Por debajo de este nivel se considera silencio (ruido de fondo del micro).")]
        [SerializeField] private float _silenceDb = -42f;
        [Tooltip("Nivel que equivale a un grito (ruido 100).")]
        [SerializeField] private float _shoutDb = -8f;
        [Tooltip("Ruido mínimo (0-100) para avisar a los infectados.")]
        [SerializeField, Range(0f, 100f)] private float _emitThreshold = 20f;

        private string _device;
        private AudioClip _clip;
        private float[] _buffer;
        private Coroutine _routine;
        private WaitForSeconds _wait;
        private bool _warnedOnce;

        /// <summary>Último nivel de voz medido (0-100).</summary>
        public float VoiceLevel { get; private set; }

        private void Awake()
        {
            _wait = new WaitForSeconds(_sampleInterval);
            _buffer = new float[Mathf.CeilToInt(SampleRate * _sampleInterval)];
        }

        private void OnEnable()
        {
            if (!_enabledByDefault) return;

            if (Microphone.devices.Length == 0)
            {
                Debug.Log("[MicrophoneNoiseSensor] No hay micrófono: la detección por voz queda desactivada.", this);
                return;
            }

            _device = Microphone.devices[0];
            _clip = Microphone.Start(_device, true, 1, SampleRate);
            _routine = StartCoroutine(ListenRoutine());
        }

        private void OnDisable()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
            if (_clip != null && Microphone.IsRecording(_device)) Microphone.End(_device);
            _clip = null;
            VoiceLevel = 0f;
        }

        private IEnumerator ListenRoutine()
        {
            while (true)
            {
                yield return _wait;

                int position = Microphone.GetPosition(_device);
                if (_clip == null || position < _buffer.Length) continue;

                _clip.GetData(_buffer, position - _buffer.Length);

                float sum = 0f;
                for (int i = 0; i < _buffer.Length; i++) sum += _buffer[i] * _buffer[i];
                float rms = Mathf.Sqrt(sum / _buffer.Length);
                float db = 20f * Mathf.Log10(Mathf.Max(rms, 1e-6f));

                VoiceLevel = Mathf.Clamp01(Mathf.InverseLerp(_silenceDb, _shoutDb, db)) * 100f;
                if (VoiceLevel < _emitThreshold) continue;

                GameEvents.RaiseNoiseEmitted(transform.position, VoiceLevel);
                if (!_warnedOnce && VoiceLevel > 60f)
                {
                    _warnedOnce = true;
                    GameEvents.RaiseSubtitle("(Tu voz resuena por la oficina... algo la ha oído)");
                }
            }
        }
    }
}
