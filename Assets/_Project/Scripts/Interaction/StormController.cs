using System.Collections;
using UnityEngine;

namespace Nexo.Interaction
{
    /// <summary>
    /// Tormenta exterior percibida desde dentro del edificio. El Piso 6 no tiene ventanas, así que el relámpago se
    /// traduce en una sobretensión de la red eléctrica: las luces indicadas destellan y caen un instante, y el trueno
    /// llega con el retardo de la distancia. Los clips ya traen chasquido, cuerpo grave y cola reverberada;
    /// el volumen no se fuerza (sin distorsión). Corrutina con esperas aleatorias: sin Update.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StormController : MonoBehaviour
    {
        [SerializeField] private AudioSource _thunderSource;
        [SerializeField] private AudioClip[] _thunderClips;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.85f;
        [Tooltip("Luces que acusan el relámpago (sobretensión y caída).")]
        [SerializeField] private Light[] _surgeLights;
        [Tooltip("Luz fría opcional que destella con el relámpago.")]
        [SerializeField] private Light _flashLight;
        [SerializeField] private Vector2 _interval = new Vector2(35f, 70f);
        [SerializeField] private float _firstDelay = 18f;
        [Tooltip("Subtítulo de los truenos (accesibilidad).")]
        [SerializeField] private string _closeSubtitle = "(Un trueno retumba sobre el edificio y las luces vacilan)";
        [SerializeField] private string _farSubtitle = "(Un trueno lejano)";

        private float[] _baseIntensities;
        private Coroutine _routine;

        private void Awake()
        {
            if (_flashLight != null) _flashLight.enabled = false;
            if (_surgeLights == null) return;
            _baseIntensities = new float[_surgeLights.Length];
            for (int i = 0; i < _surgeLights.Length; i++) if (_surgeLights[i] != null) _baseIntensities[i] = _surgeLights[i].intensity;
        }

        private void OnEnable()
        {
            if (_thunderSource != null && _thunderClips != null && _thunderClips.Length > 0) _routine = StartCoroutine(StormRoutine());
        }

        private void OnDisable()
        {
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            RestoreLights();
            if (_flashLight != null) _flashLight.enabled = false;
        }

        /// <summary>Lanza un relámpago y su trueno ahora (lo usan las secuencias del guion).</summary>
        public void StrikeNow()
        {
            if (isActiveAndEnabled) StartCoroutine(StrikeRoutine(Random.Range(0.3f, 0.8f)));
        }

        private IEnumerator StormRoutine()
        {
            yield return new WaitForSeconds(_firstDelay);
            while (true)
            {
                // Relámpago lejano (trueno tarde) o cercano (casi inmediato)
                yield return StrikeRoutine(Random.Range(0.4f, 2.6f));
                yield return new WaitForSeconds(Random.Range(_interval.x, _interval.y));
            }
        }

        private IEnumerator StrikeRoutine(float thunderDelay)
        {
            bool close = thunderDelay < 1f;

            // Destello doble y caída de tensión
            SetSurge(close ? 2.4f : 1.6f, true);
            yield return new WaitForSeconds(0.06f);
            SetSurge(0.25f, false);
            yield return new WaitForSeconds(0.08f);
            SetSurge(close ? 2f : 1.3f, true);
            yield return new WaitForSeconds(0.05f);
            SetSurge(0.35f, false);
            yield return new WaitForSeconds(0.35f);
            RestoreLights();

            yield return new WaitForSeconds(Mathf.Max(0f, thunderDelay - 0.54f));
            AudioClip clip = _thunderClips[Random.Range(0, _thunderClips.Length)];
            _thunderSource.pitch = Random.Range(0.92f, 1.04f);
            _thunderSource.PlayOneShot(clip, close ? _volume : _volume * 0.7f);
            string subtitle = close ? _closeSubtitle : _farSubtitle;
            if (!string.IsNullOrEmpty(subtitle)) Nexo.Core.GameEvents.RaiseSubtitle(subtitle);
        }

        private void SetSurge(float factor, bool flash)
        {
            if (_flashLight != null) _flashLight.enabled = flash;
            if (_surgeLights == null) return;
            for (int i = 0; i < _surgeLights.Length; i++)
            {
                if (_surgeLights[i] != null && _surgeLights[i].enabled) _surgeLights[i].intensity = _baseIntensities[i] * factor;
            }
        }

        private void RestoreLights()
        {
            if (_flashLight != null) _flashLight.enabled = false;
            if (_surgeLights == null || _baseIntensities == null) return;
            for (int i = 0; i < _surgeLights.Length; i++) if (_surgeLights[i] != null) _surgeLights[i].intensity = _baseIntensities[i];
        }
    }
}
