using System.Collections;
using UnityEngine;

namespace Nexo.AI.Elena
{
    /// <summary>
    /// Figura visible de Elena (PF_ElenaVisual) en momentos concretos: tras el cristal de la sala de servidores del Piso 12.
    /// Idle procedural (respiración y la cabeza girando despacio hacia el jugador si lo tiene delante), sin Animator propio,
    /// para que funcione con cualquier modelo que se ponga en su lugar (Nexo › Elena › Rebuild Visual).
    /// Cuando Elena sale de la sala (ElenaCompanion.FollowStarted) la figura desaparece: nunca camina pegada al jugador.
    /// Sin Update: una corrutina a 30 Hz mientras está visible.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ElenaVisualIdle : MonoBehaviour
    {
        [SerializeField] private Transform _model;
        [SerializeField] private Transform _head;
        [SerializeField] private ElenaCompanion _companion;
        [SerializeField] private float _breathAmplitude = 0.012f;
        [SerializeField] private float _breathSeconds = 3.2f;
        [SerializeField] private float _maxHeadYaw = 35f;

        private Vector3 _baseScale;
        private Quaternion _headRest;
        private Coroutine _routine;
        private Transform _player;

        public Transform Model => _model;

        private void Awake()
        {
            if (_model == null && transform.childCount > 0) _model = transform.GetChild(0);
            if (_model != null) _baseScale = _model.localScale;
            if (_head == null && _model != null)
            {
                foreach (Transform t in _model.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.EndsWith("Head")) { _head = t; break; }
                }
            }
            if (_head != null) _headRest = _head.localRotation;
            if (_companion == null) _companion = GetComponentInParent<ElenaCompanion>();
        }

        private void OnEnable()
        {
            if (_companion != null) _companion.FollowStarted += HandleFollowStarted;
            Nexo.Core.GameEvents.OnPlayerSpawned += HandlePlayerSpawned;
            _routine = StartCoroutine(IdleRoutine());
        }

        private void OnDisable()
        {
            if (_companion != null) _companion.FollowStarted -= HandleFollowStarted;
            Nexo.Core.GameEvents.OnPlayerSpawned -= HandlePlayerSpawned;
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            if (_model != null) _model.localScale = _baseScale;
        }

        private void HandlePlayerSpawned(Transform player) => _player = player;

        private void HandleFollowStarted() => gameObject.SetActive(false);

        private IEnumerator IdleRoutine()
        {
            var wait = new WaitForSeconds(1f / 30f);
            while (true)
            {
                float t = Time.time;
                if (_model != null)
                {
                    float breath = 1f + Mathf.Sin(t * Mathf.PI * 2f / _breathSeconds) * _breathAmplitude;
                    _model.localScale = new Vector3(_baseScale.x, _baseScale.y * breath, _baseScale.z);
                }
                if (_head != null)
                {
                    float yaw = Mathf.Sin(t * 0.35f) * 12f;
                    if (_player != null)
                    {
                        Vector3 local = transform.InverseTransformPoint(_player.position);
                        float toPlayer = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
                        if (Mathf.Abs(toPlayer) < 80f && local.magnitude < 14f) yaw = Mathf.Clamp(toPlayer, -_maxHeadYaw, _maxHeadYaw);
                    }
                    _head.localRotation = Quaternion.Slerp(_head.localRotation, _headRest * Quaternion.Euler(0f, yaw, 0f), 0.08f);
                }
                yield return wait;
            }
        }
    }
}
