using System.Collections;
using Nexo.Core;
using Nexo.Player;
using UnityEngine;
using UnityEngine.AI;

namespace Nexo.AI.Elena
{
    /// <summary>
    /// Cuerpo de Elena en el Piso 12: NPC que acompaña al jugador. Espera en la sala de servidores hasta que
    /// la historia la libera (StartFollowing) y entonces le sigue a corta distancia: camina, corre si se queda
    /// atrás, se agacha cuando el jugador se agacha y se gira hacia él cuando responde por radio.
    /// Evalúa cada _tickInterval segundos por corrutina (sin Update()), igual que la IA del infectado.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [DisallowMultipleComponent]
    public sealed class ElenaCompanion : MonoBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int IsCrouchedHash = Animator.StringToHash("IsCrouched");

        [SerializeField] private Animator _animator;
        [SerializeField] private float _tickInterval = 0.2f;

        [Header("Seguimiento")]
        [SerializeField] private float _followDistance = 2.2f;
        [SerializeField] private float _runDistance = 6f;
        [SerializeField] private float _teleportDistance = 25f;
        [SerializeField] private float _walkSpeed = 2.2f;
        [SerializeField] private float _runSpeed = 4.6f;

        private NavMeshAgent _agent;
        private Transform _player;
        private PlayerMovement _playerMovement;
        private Coroutine _routine;
        private WaitForSeconds _wait;
        private bool _isFollowing;

        public bool IsFollowing => _isFollowing;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            _wait = new WaitForSeconds(_tickInterval);
        }

        private void OnEnable()
        {
            GameEvents.OnPlayerSpawned += HandlePlayerSpawned;
            GameEvents.OnElenaResponse += HandleElenaResponse;
        }

        private void OnDisable()
        {
            GameEvents.OnPlayerSpawned -= HandlePlayerSpawned;
            GameEvents.OnElenaResponse -= HandleElenaResponse;
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
        }

        private void HandlePlayerSpawned(Transform player)
        {
            _player = player;
            _playerMovement = player != null ? player.GetComponent<PlayerMovement>() : null;
        }

        /// <summary>
        /// Libera a Elena: aparece en <paramref name="exitPoint"/> (si se indica) y empieza a seguir al jugador.
        /// </summary>
        public void StartFollowing(Transform exitPoint = null)
        {
            if (_isFollowing) return;
            if (exitPoint != null) _agent.Warp(exitPoint.position);
            _isFollowing = true;
            _routine = StartCoroutine(FollowRoutine());
        }

        private IEnumerator FollowRoutine()
        {
            while (true)
            {
                yield return _wait;
                if (_player == null || !_agent.isOnNavMesh) continue;

                float distance = Vector3.Distance(transform.position, _player.position);
                if (distance > _teleportDistance)
                {
                    // Se quedó muy atrás (otra zona del piso): reaparece detrás del jugador
                    if (NavMesh.SamplePosition(_player.position - _player.forward * 2f, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                    {
                        _agent.Warp(hit.position);
                    }
                    continue;
                }

                if (distance > _followDistance)
                {
                    _agent.isStopped = false;
                    _agent.speed = distance > _runDistance ? _runSpeed : _walkSpeed;
                    _agent.SetDestination(_player.position - (_player.position - transform.position).normalized * (_followDistance * 0.8f));
                }
                else
                {
                    _agent.isStopped = true;
                    FacePlayer();
                }

                if (_animator != null)
                {
                    _animator.SetFloat(SpeedHash, _agent.velocity.magnitude);
                    _animator.SetBool(IsCrouchedHash, _playerMovement != null && _playerMovement.IsCrouching);
                }
            }
        }

        private void HandleElenaResponse(ElenaResponse response)
        {
            if (_isFollowing) FacePlayer();
        }

        private void FacePlayer()
        {
            if (_player == null) return;
            Vector3 flat = _player.position - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(flat);
        }
    }
}
