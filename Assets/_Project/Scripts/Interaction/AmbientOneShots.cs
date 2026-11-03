using System.Collections;
using UnityEngine;

namespace Nexo.Interaction
{
    /// <summary>
    /// Sonidos ambientales esporádicos (crujidos, golpes lejanos) en posiciones aleatorias del piso.
    /// Corrutina con esperas aleatorias: sin Update().
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AmbientOneShots : MonoBehaviour
    {
        [SerializeField] private AudioSource[] _emitters;
        [SerializeField] private AudioClip[] _clips;
        [SerializeField] private Vector2 _interval = new Vector2(18f, 45f);
        [SerializeField] private Vector2 _volume = new Vector2(0.35f, 0.8f);

        private Coroutine _routine;

        private void OnEnable()
        {
            if (_emitters != null && _emitters.Length > 0 && _clips != null && _clips.Length > 0)
            {
                _routine = StartCoroutine(PlayRoutine());
            }
        }

        private void OnDisable()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
        }

        private IEnumerator PlayRoutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(_interval.x, _interval.y));

                AudioSource emitter = _emitters[Random.Range(0, _emitters.Length)];
                AudioClip clip = _clips[Random.Range(0, _clips.Length)];
                if (emitter != null && clip != null)
                {
                    emitter.pitch = Random.Range(0.85f, 1.1f);
                    emitter.PlayOneShot(clip, Random.Range(_volume.x, _volume.y));
                }
            }
        }
    }
}
