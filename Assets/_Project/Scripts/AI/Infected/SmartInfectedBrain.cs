using System;
using System.Collections;
using System.Collections.Generic;
using Nexo.Core;
using Nexo.Player;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace Nexo.AI.Infected
{
    /// <summary>
    /// Cerebro del infectado inteligente: sensores de vista/oído, parámetros del Animator (AC_SmartInfected)
    /// y FSM de 5 estados evaluada por corrutina cada 0.15s (sin Update).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [DisallowMultipleComponent]
    public sealed class SmartInfectedBrain : MonoBehaviour
    {
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int IsAlertedHash = Animator.StringToHash("IsAlerted");
        private static readonly int AttackTriggerHash = Animator.StringToHash("AttackTrigger");
        private static readonly int SabotageTriggerHash = Animator.StringToHash("SabotageTrigger");
        private static readonly int ScreamTriggerHash = Animator.StringToHash("ScreamTrigger");

        [Header("Referencias")]
        [SerializeField] private Animator _animator;
        [SerializeField] private AudioSource _voiceSource;
        [Tooltip("Hijo a la altura de los ojos (Y = 1.6m).")]
        [SerializeField] private Transform _eyes;
        [SerializeField] private Transform[] _patrolPoints;

        [Header("Evaluación de la FSM")]
        [SerializeField] private float _tickInterval = 0.15f;

        [Header("Movimiento")]
        [SerializeField] private float _walkSpeed = 1.6f;
        [SerializeField] private float _runSpeed = 4.2f;
        [SerializeField] private float _attackRange = 1.6f;
        [SerializeField] private float _attackCooldown = 1.5f;

        [Header("Vista")]
        [SerializeField] private float _sightDistance = 14f;
        [Tooltip("Ángulo total del cono de visión en grados.")]
        [SerializeField] private float _sightAngle = 80f;
        [Tooltip("Default | Environment. NUNCA incluir la capa Player.")]
        [SerializeField] private LayerMask _obstacleMask = (1 << 0) | (1 << 11); // Default (0) | Environment (11)
        [SerializeField] private float _playerTargetHeight = 1.2f;

        [Header("Oído")]
        [Tooltip("Rango a intensidad 100. El rango real es intensity / 100 * este valor.")]
        [SerializeField] private float _maxHearingRange = 25f;

        [Header("Proximidad (contexto de Elena / radar)")]
        [Tooltip("Distancia al jugador a la que este infectado cuenta en infected_nearby.")]
        [SerializeField] private float _nearbyRadius = 18f;

        [Header("Sabotaje de Luces")]
        [SerializeField] private bool _sabotageEnabled = true;
        [SerializeField] private float _sabotageRadius = 12f;
        [SerializeField] private float _sabotageCooldown = 30f;

        [Header("Mimetismo (voz falsa de Elena)")]
        [SerializeField] private bool _mimicryEnabled = true;
        [SerializeField] private float _mimicryCooldown = 45f;
        [SerializeField] private AudioClip[] _mimicClips;
        [SerializeField] private string[] _mimicMessages =
        {
            "Alex, ven al pasillo norte. Aquí estoy a salvo.",
            "Apaga la linterna, te están viendo. Ven hacia mí.",
            "La salida está por los cubículos, rápido."
        };

        [Header("Daño (M1911)")]
        [SerializeField] private int _maxHealth = 3;
        [SerializeField] private float _stunDuration = 1.2f;
        [SerializeField] private float _downedDuration = 20f;
        [Tooltip("Pieza visual que se tumba al caer (por defecto el hijo 'Body').")]
        [SerializeField] private Transform _visualRoot;
        [Tooltip("Si está activo, al agotar la salud cae y se levanta tras _downedDuration; si no, muere definitivamente.")]
        [SerializeField] private bool _reviveAfterDowned = false;

        [Header("Sigilo del jugador")]
        [Tooltip("Multiplicador del alcance de vista cuando el jugador está agachado.")]
        [SerializeField, Range(0.2f, 1f)] private float _crouchSightFactor = 0.55f;

        private NavMeshAgent _agent;
        private Transform _player;
        private PlayerMovement _playerMovement;
        private int _health;
        private float _incapacitatedUntil;
        private bool _isDowned;
        private Coroutine _downedRoutine;
        private bool _isDead;

        /// <summary>Se emite cuando un infectado muere definitivamente (LevelExitGate cuenta las bajas).</summary>
        public static event Action<SmartInfectedBrain> Died;
        private IInfectedState _currentState;
        private Coroutine _tickRoutine;
        private Coroutine _lookAroundRoutine;
        private WaitForSeconds _tickWait;

        private bool _heardNoise;
        private bool _canSeePlayer;
        private float _lastAttackTime = float.NegativeInfinity;
        private float _lastSabotageTime;
        private float _lastMimicryTime;
        private bool _isNearPlayer;

        // Infectados actualmente dentro de _nearbyRadius del jugador (compartido entre instancias)
        private static readonly HashSet<SmartInfectedBrain> NearbyInfected = new();

        /// <summary>Se emite cuando el infectado conecta un ataque cuerpo a cuerpo con el jugador.</summary>
        public event Action<SmartInfectedBrain> PlayerAttacked;

        public PatrolState Patrol { get; private set; }
        public InvestigateNoiseState Investigate { get; private set; }
        public SabotageLightState Sabotage { get; private set; }
        public MimicryAmbushState Mimicry { get; private set; }
        public ChaseState Chase { get; private set; }

        public NavMeshAgent Agent => _agent;
        public Transform Player => _player;
        public Transform[] PatrolPoints => _patrolPoints;
        public float WalkSpeed => _walkSpeed;
        public float RunSpeed => _runSpeed;
        public float AttackRange => _attackRange;
        public float SabotageRadius => _sabotageRadius;
        public float TickInterval => _tickInterval;
        public LayerMask ObstacleMask => _obstacleMask;

        public Vector3 LastNoisePos { get; private set; }
        public Vector3 LastKnownPlayerPos { get; set; }

        /// <summary>Resultado del sensor de vista calculado al inicio del tick actual.</summary>
        public bool CanSeePlayer => _canSeePlayer;

        public bool SabotageReady => _sabotageEnabled && Time.time - _lastSabotageTime >= _sabotageCooldown;
        public bool MimicryReady => _mimicryEnabled && _player != null && Time.time - _lastMimicryTime >= _mimicryCooldown;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_animator != null) _animator.applyRootMotion = false;
            if (_eyes == null) _eyes = transform;
            if (_visualRoot == null) _visualRoot = transform.Find("Body");
            _health = _maxHealth;

            _tickWait = new WaitForSeconds(_tickInterval);

            // Los cooldowns arrancan corriendo para que no sabotee ni imite nada más aparecer
            _lastSabotageTime = Time.time;
            _lastMimicryTime = Time.time;

            Patrol = new PatrolState(this);
            Investigate = new InvestigateNoiseState(this);
            Sabotage = new SabotageLightState(this);
            Mimicry = new MimicryAmbushState(this);
            Chase = new ChaseState(this);
        }

        private void OnEnable()
        {
            GameEvents.OnNoiseEmitted += HandleNoiseEmitted;
            _tickRoutine = StartCoroutine(TickRoutine());
        }

        private void OnDisable()
        {
            GameEvents.OnNoiseEmitted -= HandleNoiseEmitted;

            if (_tickRoutine != null)
            {
                StopCoroutine(_tickRoutine);
                _tickRoutine = null;
            }
            StopLookAround();

            // Al descargarse el piso (o morir) deja de contar como cercano
            UpdateProximity(false);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            NearbyInfected.Clear();
            Died = null;
        }

        /// <summary>
        /// Mantiene el conjunto compartido de infectados cercanos y publica el recuento solo cuando cambia.
        /// </summary>
        private void UpdateProximity(bool isNear)
        {
            if (isNear == _isNearPlayer) return;
            _isNearPlayer = isNear;

            if (isNear) NearbyInfected.Add(this);
            else NearbyInfected.Remove(this);

            GameEvents.RaiseInfectedNearbyChanged(NearbyInfected.Count);
        }

        /// <summary>
        /// Inicialización en runtime desde InfectedSpawnPoint (el jugador vive en Systems_Persistent).
        /// </summary>
        public void Initialize(Transform player, Transform[] patrolPoints)
        {
            _player = player;
            _playerMovement = player != null ? player.GetComponent<PlayerMovement>() : null;
            if (patrolPoints != null && patrolPoints.Length > 0)
            {
                _patrolPoints = patrolPoints;
            }

            if (_agent != null && !_agent.isOnNavMesh)
            {
                _agent.Warp(transform.position);
            }
        }

        private IEnumerator TickRoutine()
        {
            // Esperar un tick para que el agente quede colocado sobre el NavMesh
            yield return _tickWait;

            if (_currentState == null) ChangeState(Patrol);

            while (true)
            {
                // Aturdido por un disparo o tumbado: no percibe ni decide
                if (Time.time < _incapacitatedUntil)
                {
                    UpdateAnimatorSpeed();
                    yield return _tickWait;
                    continue;
                }

                _canSeePlayer = EvaluateSight();
                if (_canSeePlayer) LastKnownPlayerPos = _player.position;

                _currentState?.Tick();
                UpdateAnimatorSpeed();
                UpdateProximity(DistanceToPlayer() <= _nearbyRadius);

                yield return _tickWait;
            }
        }

        // ---------- Daño ----------

        public bool IsDowned => _isDowned;
        public bool IsDead => _isDead;

        /// <summary>
        /// Impacto de bala: aturde unos instantes y luego persigue con rabia hacia el origen del disparo.
        /// Al agotar la salud cae al suelo durante _downedDuration y se levanta de nuevo (no muere).
        /// </summary>
        public void ApplyHit(Vector3 shotOrigin)
        {
            if (_isDowned || _isDead) return;

            LastKnownPlayerPos = shotOrigin;
            _health--;
            StopMoving();
            StopLookAround();

            if (_health <= 0)
            {
                if (_reviveAfterDowned) _downedRoutine = StartCoroutine(DownedRoutine());
                else Die();
                return;
            }

            _incapacitatedUntil = Time.time + _stunDuration;
            ChangeState(Chase);
        }

        /// <summary>Muerte definitiva: queda tendido, deja de percibir y de bloquear el paso.</summary>
        private void Die()
        {
            _isDead = true;
            _incapacitatedUntil = float.PositiveInfinity;
            StopMoving();
            if (_tickRoutine != null) { StopCoroutine(_tickRoutine); _tickRoutine = null; }
            StopLookAround();
            SetAlerted(false);
            UpdateProximity(false);
            if (_visualRoot != null) _visualRoot.localRotation = Quaternion.Euler(-88f, 0f, 0f);
            if (_animator != null) _animator.enabled = false;
            if (_agent != null) _agent.enabled = false;
            foreach (Collider c in GetComponents<Collider>()) c.enabled = false;
            if (_voiceSource != null) _voiceSource.Stop();
            Died?.Invoke(this);
        }

        // ---------- Forcejeo con el jugador (QTE) ----------

        /// <summary>El infectado agarra al jugador: queda inmóvil frente a él mientras dura el forcejeo.</summary>
        public void BeginGrapple()
        {
            if (_isDead) return;
            _incapacitatedUntil = float.PositiveInfinity;
            StopMoving();
            StopLookAround();
            FacePlayer();
        }

        /// <summary>
        /// Fin del forcejeo. Si el jugador lo empuja, sale despedido hacia atrás y queda tumbado unos segundos;
        /// si el jugador falla, se queda un instante aturdido para que pueda huir.
        /// </summary>
        public void EndGrapple(bool pushedBack)
        {
            if (_isDead) return;
            _lastAttackTime = Time.time;
            if (pushedBack) StartCoroutine(KnockbackRoutine());
            else _incapacitatedUntil = Time.time + 1.5f;
        }

        private IEnumerator KnockbackRoutine()
        {
            _incapacitatedUntil = Time.time + 2.8f;
            Vector3 away = _player != null ? (transform.position - _player.position) : -transform.forward;
            away.y = 0f;
            if (NavMesh.SamplePosition(transform.position + away.normalized * 1.8f, out NavMeshHit hit, 1.5f, NavMesh.AllAreas) && _agent.enabled)
            {
                _agent.Warp(hit.position);
            }
            if (_visualRoot != null) _visualRoot.localRotation = Quaternion.Euler(-80f, 0f, 0f);
            yield return new WaitForSeconds(2.2f);
            if (_visualRoot != null && !_isDead) _visualRoot.localRotation = Quaternion.identity;
            if (!_isDead) ChangeState(Chase);
        }

        private IEnumerator DownedRoutine()
        {
            _isDowned = true;
            _incapacitatedUntil = float.PositiveInfinity;
            StopMoving();
            SetAlerted(false);
            UpdateProximity(false);
            if (_visualRoot != null) _visualRoot.localRotation = Quaternion.Euler(-85f, 0f, 0f);

            yield return new WaitForSeconds(_downedDuration);

            if (_visualRoot != null) _visualRoot.localRotation = Quaternion.identity;
            _health = _maxHealth;
            _isDowned = false;
            _incapacitatedUntil = 0f;
            _downedRoutine = null;
            TriggerScream();
            ChangeState(Investigate);
        }

        public void ChangeState(IInfectedState next)
        {
            if (next == null || next == _currentState) return;

            _currentState?.Exit();
            _currentState = next;
            _currentState.Enter();
        }

        // ---------- Sensores ----------

        private bool EvaluateSight()
        {
            if (_player == null) return false;

            // Agachado: silueta más baja y más difícil de distinguir a distancia
            bool crouching = _playerMovement != null && _playerMovement.IsCrouching;
            float sightDistance = crouching ? _sightDistance * _crouchSightFactor : _sightDistance;
            float targetHeight = crouching ? _playerTargetHeight * 0.6f : _playerTargetHeight;

            Vector3 origin = _eyes.position;
            Vector3 target = _player.position + Vector3.up * targetHeight;
            Vector3 toPlayer = target - origin;
            float distance = toPlayer.magnitude;

            if (distance > sightDistance) return false;
            if (Vector3.Angle(transform.forward, toPlayer) > _sightAngle * 0.5f) return false;

            // La máscara excluye la capa Player: si no hay obstáculo en medio, hay línea de visión
            return !Physics.Raycast(origin, toPlayer / distance, distance, _obstacleMask, QueryTriggerInteraction.Ignore);
        }

        private void HandleNoiseEmitted(Vector3 position, float intensity)
        {
            float range = Mathf.Clamp(intensity, 0f, 100f) / 100f * _maxHearingRange;
            if ((position - transform.position).sqrMagnitude > range * range) return;

            LastNoisePos = position;
            _heardNoise = true;
        }

        /// <summary>
        /// Devuelve true una sola vez por cada ruido escuchado desde la última consulta.
        /// </summary>
        public bool ConsumeHeardNoise()
        {
            if (!_heardNoise) return false;
            _heardNoise = false;
            return true;
        }

        // ---------- Utilidades para los estados ----------

        public void MoveTo(Vector3 destination, float speed)
        {
            if (_agent == null || !_agent.isOnNavMesh) return;

            _agent.isStopped = false;
            _agent.speed = speed;
            _agent.SetDestination(destination);
        }

        public void StopMoving()
        {
            if (_agent == null || !_agent.isOnNavMesh) return;

            _agent.isStopped = true;
            _agent.ResetPath();
        }

        public bool HasArrived(float tolerance = 0.3f)
        {
            if (_agent == null || !_agent.isOnNavMesh || _agent.pathPending) return false;
            return _agent.remainingDistance <= _agent.stoppingDistance + tolerance;
        }

        public float DistanceToPlayer()
        {
            return _player == null ? float.MaxValue : Vector3.Distance(transform.position, _player.position);
        }

        public void SetAlerted(bool alerted)
        {
            if (_animator != null) _animator.SetBool(IsAlertedHash, alerted);
        }

        public void TriggerScream() => SetAnimatorTrigger(ScreamTriggerHash);
        public void TriggerSabotage() => SetAnimatorTrigger(SabotageTriggerHash);

        /// <summary>
        /// Lanza el ataque si el cooldown lo permite. Devuelve true si se ejecutó.
        /// </summary>
        public bool TryAttack()
        {
            if (Time.time - _lastAttackTime < _attackCooldown) return false;

            _lastAttackTime = Time.time;
            FacePlayer();
            SetAnimatorTrigger(AttackTriggerHash);
            PlayerAttacked?.Invoke(this);
            // PlayerHealth decide: forcejeo (QTE), golpe acumulado o captura si se agota la vida
            GameEvents.RaisePlayerAttacked(transform);
            return true;
        }

        public void FacePlayer()
        {
            if (_player == null) return;

            Vector3 flat = _player.position - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(flat);
        }

        public void MarkSabotageUsed() => _lastSabotageTime = Time.time;
        public void MarkMimicryUsed() => _lastMimicryTime = Time.time;

        /// <summary>
        /// Reproduce la voz distorsionada y envía la notificación falsa (IsFake) al HUD.
        /// </summary>
        public void PlayMimicry()
        {
            if (_voiceSource != null && _mimicClips != null && _mimicClips.Length > 0)
            {
                AudioClip clip = _mimicClips[Random.Range(0, _mimicClips.Length)];
                if (clip != null) _voiceSource.PlayOneShot(clip);
            }

            string message = _mimicMessages != null && _mimicMessages.Length > 0
                ? _mimicMessages[Random.Range(0, _mimicMessages.Length)]
                : "Alex... ven aquí.";

            GameEvents.RaiseNotification(new HudNotification("ELENA", message, true));
        }

        /// <summary>
        /// Gira suavemente sobre sí mismo durante la búsqueda (corrutina temporal, se detiene sola).
        /// </summary>
        public void StartLookAround(float duration, float degreesPerSecond = 120f)
        {
            StopLookAround();
            _lookAroundRoutine = StartCoroutine(LookAroundRoutine(duration, degreesPerSecond));
        }

        public void StopLookAround()
        {
            if (_lookAroundRoutine == null) return;

            StopCoroutine(_lookAroundRoutine);
            _lookAroundRoutine = null;
        }

        private IEnumerator LookAroundRoutine(float duration, float degreesPerSecond)
        {
            float elapsed = 0f;
            float direction = Random.value < 0.5f ? -1f : 1f;

            while (elapsed < duration)
            {
                // Cambia de sentido a mitad de la búsqueda para "barrer" la zona
                float sign = elapsed < duration * 0.5f ? direction : -direction;
                transform.Rotate(0f, sign * degreesPerSecond * Time.deltaTime, 0f);
                elapsed += Time.deltaTime;
                yield return null;
            }

            _lookAroundRoutine = null;
        }

        private void UpdateAnimatorSpeed()
        {
            if (_animator == null || _agent == null) return;
            _animator.SetFloat(SpeedHash, _agent.velocity.magnitude);
        }

        private void SetAnimatorTrigger(int hash)
        {
            if (_animator != null) _animator.SetTrigger(hash);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Transform eyes = _eyes != null ? _eyes : transform;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _sightDistance);

            Vector3 left = Quaternion.Euler(0f, -_sightAngle * 0.5f, 0f) * transform.forward;
            Vector3 right = Quaternion.Euler(0f, _sightAngle * 0.5f, 0f) * transform.forward;
            Gizmos.DrawLine(eyes.position, eyes.position + left * _sightDistance);
            Gizmos.DrawLine(eyes.position, eyes.position + right * _sightDistance);

            Gizmos.color = new Color(1f, 0.4f, 0f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, _sabotageRadius);
        }
#endif
    }
}
