using System.Collections;
using UnityEngine;

namespace Nexo.Interaction
{
    /// <summary>
    /// Parpadeo irregular de un fluorescente averiado. Solo modula la intensidad (no Light.enabled),
    /// así LightSwitchInteractable puede seguir apagando y encendiendo la luz sin conflictos.
    /// Corrutina con pausas aleatorias: sin Update().
    /// </summary>
    [RequireComponent(typeof(Light))]
    [DisallowMultipleComponent]
    public sealed class LightFlicker : MonoBehaviour
    {
        [Header("Intensidad")]
        [SerializeField] private float _minIntensityFactor = 0.05f;

        [Header("Ritmo (segundos)")]
        [SerializeField] private Vector2 _stableInterval = new Vector2(1.5f, 6f);
        [SerializeField] private Vector2 _flickerStep = new Vector2(0.03f, 0.12f);
        [SerializeField] private Vector2Int _flickersPerBurst = new Vector2Int(2, 7);

        private Light _light;
        private float _baseIntensity;
        private Coroutine _routine;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _baseIntensity = _light.intensity;
        }

        private void OnEnable()
        {
            _routine = StartCoroutine(FlickerRoutine());
        }

        private void OnDisable()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            if (_light != null) _light.intensity = _baseIntensity;
        }

        private IEnumerator FlickerRoutine()
        {
            while (true)
            {
                _light.intensity = _baseIntensity;
                yield return new WaitForSeconds(Random.Range(_stableInterval.x, _stableInterval.y));

                int flickers = Random.Range(_flickersPerBurst.x, _flickersPerBurst.y + 1);
                for (int i = 0; i < flickers; i++)
                {
                    _light.intensity = _baseIntensity * Random.Range(_minIntensityFactor, 0.6f);
                    yield return new WaitForSeconds(Random.Range(_flickerStep.x, _flickerStep.y));
                    _light.intensity = _baseIntensity;
                    yield return new WaitForSeconds(Random.Range(_flickerStep.x, _flickerStep.y));
                }
            }
        }
    }
}
