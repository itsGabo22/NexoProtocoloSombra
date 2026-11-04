using System;
using System.Collections.Generic;
using Nexo.Core;
using Nexo.UI.Menus;
using UnityEngine;

namespace Nexo.AI.Elena
{
    /// <summary>
    /// Voz de Elena por la radio de las gafas. Cada línea de Elena (respuesta, secuencia de historia, aviso o giro)
    /// busca su clip pregrabado en ElenaVoiceLibrary y lo reproduce en 2D con tratamiento de radio (filtros paso alto y
    /// paso bajo y una distorsión leve), bajando un poco el resto del audio mientras habla. Si una respuesta dinámica de
    /// Gemini no tiene clip, puede pedir la voz a Gemini TTS con timeout corto y una caché pequeña; si no llega, solo
    /// subtítulo. Se pausa con el menú de pausa, calla en cinemáticas y al cambiar de piso, y respeta el volumen general
    /// (AudioListener). La voz falsa («¿ELENA?:») no pasa por aquí.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class ElenaVoicePlayer : MonoBehaviour
    {
        private const int CacheSize = 8;
        private const float DuplicateWindow = 2.5f;

        [SerializeField] private ElenaConfig _config;
        [SerializeField, Range(0f, 1f)] private float _volume = 1f;
        [Tooltip("Factor del volumen general mientras Elena habla (1 = sin atenuar).")]
        [SerializeField, Range(0.5f, 1f)] private float _duckFactor = 0.85f;
        [SerializeField] private float _highPassHz = 320f;
        [SerializeField] private float _lowPassHz = 4200f;

        private AudioSource _source;
        private bool _paused;
        private bool _inCinematic;
        private bool _ducked;
        private float _listenerBeforeDuck = 1f;
        private string _lastKey = string.Empty;
        private float _lastKeyTime = -10f;
        private int _requestSerial;
        private Coroutine _duckWatch;
        private readonly Dictionary<string, AudioClip> _dynamicCache = new Dictionary<string, AudioClip>();
        private readonly Queue<string> _cacheOrder = new Queue<string>();

        public bool IsSpeaking => _source != null && _source.isPlaying;
        public string LastSpokenKey => _lastKey;
        /// <summary>Líneas reproducidas con clip (comprobaciones del Editor).</summary>
        public int ClipsPlayed { get; private set; }

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0f;
            _source.priority = 8;
            _source.ignoreListenerPause = false; // las cinemáticas pausan AudioListener y con él esta voz
            AddRadioFilters();
        }

        private void AddRadioFilters()
        {
            // TryGetComponent: en el Editor un componente que falta no es null de C# (el operador ?? no sirve)
            if (!TryGetComponent(out AudioHighPassFilter high)) high = gameObject.AddComponent<AudioHighPassFilter>();
            high.cutoffFrequency = _highPassHz;
            if (!TryGetComponent(out AudioLowPassFilter low)) low = gameObject.AddComponent<AudioLowPassFilter>();
            low.cutoffFrequency = _lowPassHz;
            if (!TryGetComponent(out AudioDistortionFilter dist)) dist = gameObject.AddComponent<AudioDistortionFilter>();
            dist.distortionLevel = 0.12f;
        }

        private void OnEnable()
        {
            GameEvents.OnElenaResponse += HandleElenaResponse;
            GameEvents.OnSubtitle += HandleSubtitle;
            GameEvents.OnCinematicStarted += HandleCinematicStarted;
            GameEvents.OnCinematicFinished += HandleCinematicFinished;
            GameEvents.OnFloorChanged += HandleFloorChanged;
            GameEvents.OnPlayerCaught += HandlePlayerCaught;
            MenuEvents.PauseChanged += HandlePauseChanged;
            MenuEvents.ReturnToMainMenuRequested += HandleReturnToMenu;
        }

        private void OnDisable()
        {
            GameEvents.OnElenaResponse -= HandleElenaResponse;
            GameEvents.OnSubtitle -= HandleSubtitle;
            GameEvents.OnCinematicStarted -= HandleCinematicStarted;
            GameEvents.OnCinematicFinished -= HandleCinematicFinished;
            GameEvents.OnFloorChanged -= HandleFloorChanged;
            GameEvents.OnPlayerCaught -= HandlePlayerCaught;
            MenuEvents.PauseChanged -= HandlePauseChanged;
            MenuEvents.ReturnToMainMenuRequested -= HandleReturnToMenu;
            StopVoice();
        }

        private void HandleElenaResponse(ElenaResponse response)
        {
            if (response == null) return;
            Speak(response.dialogue, response.emotion, true);
        }

        private void HandleSubtitle(string text)
        {
            // Líneas que solo llegan como subtítulo (giro final). Las de historia llegan también como respuesta: se ignoran duplicadas
            if (ElenaVoiceText.IsElenaSubtitle(text)) Speak(ElenaVoiceText.StripSpeaker(text), "CALM", false);
        }

        /// <summary>Reproduce la voz de una línea (comprobaciones del Editor y sistemas propios).</summary>
        public void Speak(string line, string emotion, bool allowDynamic)
        {
            if (_inCinematic || string.IsNullOrWhiteSpace(line)) return;
            string key = ElenaVoiceText.Normalize(line);
            if (key.Length == 0) return;
            if (key == _lastKey && Time.unscaledTime - _lastKeyTime < DuplicateWindow) return;
            _lastKey = key;
            _lastKeyTime = Time.unscaledTime;
            _requestSerial++;

            ElenaVoiceLibrary library = ElenaVoiceLibrary.Instance;
            if (library != null && library.TryGetClip(line, out AudioClip clip))
            {
                ClipsPlayed++;
                Play(clip);
                return;
            }
            if (_dynamicCache.TryGetValue(key, out AudioClip cached) && cached != null)
            {
                Play(cached);
                return;
            }
            if (allowDynamic && CanSynthesize()) RequestDynamic(line, emotion, key, _requestSerial);
        }

        private bool CanSynthesize()
        {
            return _config != null && _config.RuntimeTts && !_config.ForceFallback && _config.Secrets != null &&
                   _config.Secrets.HasApiKey && Application.internetReachability != NetworkReachability.NotReachable;
        }

        private async void RequestDynamic(string line, string emotion, string key, int serial)
        {
            try
            {
                AudioClip clip = await GeminiTtsClient.SynthesizeAsync(_config, line, emotion);
                if (clip == null) return;
                Remember(key, clip);
                // Solo si sigue siendo la última línea de Elena y nada la ha silenciado
                if (this != null && isActiveAndEnabled && serial == _requestSerial && !_inCinematic) Play(clip);
            }
            catch (Exception ex)
            {
                Debug.Log($"[ElenaVoicePlayer] Voz dinámica descartada: {ex.GetType().Name}");
            }
        }

        private void Remember(string key, AudioClip clip)
        {
            if (_dynamicCache.ContainsKey(key)) return;
            _dynamicCache[key] = clip;
            _cacheOrder.Enqueue(key);
            while (_cacheOrder.Count > CacheSize)
            {
                string old = _cacheOrder.Dequeue();
                if (_dynamicCache.TryGetValue(old, out AudioClip oldClip) && oldClip != null) Destroy(oldClip);
                _dynamicCache.Remove(old);
            }
        }

        private void Play(AudioClip clip)
        {
            if (_source == null || clip == null) return;
            _source.Stop();
            _source.clip = clip;
            _source.volume = _volume;
            _source.Play();
            if (_paused) _source.Pause();
            BeginDuck();
        }

        private void BeginDuck()
        {
            if (!_ducked)
            {
                _ducked = true;
                _listenerBeforeDuck = AudioListener.volume;
                AudioListener.volume = _listenerBeforeDuck * _duckFactor;
                // La voz compensa la atenuación global: suena igual, el ambiente un poco más bajo
                _source.volume = Mathf.Clamp01(_volume / _duckFactor);
            }
            if (_duckWatch != null) StopCoroutine(_duckWatch);
            _duckWatch = StartCoroutine(EndDuckWhenSilent());
        }

        private System.Collections.IEnumerator EndDuckWhenSilent()
        {
            var wait = new WaitForSecondsRealtime(0.2f);
            while (_source != null && (_source.isPlaying || _paused)) yield return wait;
            EndDuck();
            _duckWatch = null;
        }

        private void EndDuck()
        {
            if (!_ducked) return;
            _ducked = false;
            // Si el jugador cambió el volumen general mientras hablaba, se respeta el nuevo valor
            float expected = _listenerBeforeDuck * _duckFactor;
            AudioListener.volume = Mathf.Abs(AudioListener.volume - expected) < 0.001f ? _listenerBeforeDuck : AudioListener.volume;
        }

        private void StopVoice()
        {
            _requestSerial++;
            if (_source != null) _source.Stop();
            if (_duckWatch != null) { StopCoroutine(_duckWatch); _duckWatch = null; }
            EndDuck();
        }

        private void HandlePauseChanged(bool paused)
        {
            _paused = paused;
            if (_source == null) return;
            if (paused) _source.Pause();
            else _source.UnPause();
        }

        private void HandleCinematicStarted(string id)
        {
            _inCinematic = true;
            StopVoice();
        }

        private void HandleCinematicFinished(string id) => _inCinematic = false;

        private void HandleFloorChanged(int floor)
        {
            _paused = false;
            StopVoice();
        }

        private void HandlePlayerCaught() => StopVoice();

        private void HandleReturnToMenu()
        {
            _paused = false;
            StopVoice();
        }
    }
}
