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
    /// y FSM evaluada por corrutina cada 0.15 s (sin Update).
    /// Estados de sigilo: Patrol → Suspicious → Investigate → Search → Chase → Attack → Return,
    /// más Sabotage (luces) y Mimicry (voz falsa de Elena).
    /// La vista puede ser instantánea (Piso 12, comportamiento original) o gradual (_gradedDetection: el infectado
    /// necesita ver al jugador un tiempo que depende de la distancia, la postura y la linterna).
    /// Nunca consulta la posición real del jugador salvo cuando lo ve con línea de visión.
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

        private const float FarTickInterval = 0.45f;
        private const float FarTickDistance = 40f;
        private const float InstantSightDistance = 3f;

        [Header("Referencias")]
        [SerializeField] private Animator _animator;
        [SerializeField] private AudioSource _voiceSource;
        [Tooltip("Hijo a la altura de los ojos (Y = 1.6m).")]
        [SerializeField] private Transform _eyes;
        [SerializeField] private Transform[] _patrolPoints;
        [Tooltip("Reacciones procedurales a impactos y caídas (opcional).")]
        [SerializeField] private InfectedHitReactions _reactions;
        [Tooltip("Memoria del infectado que aprende (opcional: solo en los infectados especiales).")]
        [SerializeField] private InfectedLearningMemory _memory;

        [Header("Comportamiento")]
        [SerializeField] private InfectedBehavior _behavior = InfectedBehavior.Patroller;
        [Tooltip("Pausa al llegar a cada punto de patrulla (segundos, mínimo y máximo).")]
        [SerializeField] private Vector2 _patrolPause = new Vector2(1.2f, 3.5f);
        [Tooltip("Radio en el que deambula el perfil Wanderer alrededor de su posición inicial.")]
        [SerializeField] private float _wanderRadius = 6f;

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
        [Tooltip("Detección gradual: debe ver al jugador un tiempo antes de reconocerlo (sigilo del Piso 6).")]
        [SerializeField] private bool _gradedDetection = false;
        [Tooltip("Segundos de visión continua a media distancia para reconocer al jugador (detección gradual).")]
        [SerializeField] private float _detectionTime = 1.4f;

        [Header("Oído")]
        [Tooltip("Rango a intensidad 100. El rango real es intensity / 100 * este valor.")]
        [SerializeField] private float _maxHearingRange = 25f;
        [Tooltip("Factor del alcance cuando hay una pared entre el ruido y el infectado.")]
        [SerializeField, Range(0.2f, 1f)] private float _occludedHearingFactor = 0.55f;

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
        [Tooltip("Tono de la voz imitada (< 1 = más grave). El eco lo aporta el AudioEchoFilter de la fuente.")]
        [SerializeField, Range(0.6f, 1.2f)] private float _mimicPitch = 0.88f;
        [SerializeField] private string[] _mimicMessages =
        {
            "Alex, ven al pasillo norte. Aquí estoy a salvo.",
            "Apaga la linterna, te están viendo. Ven hacia mí.",
            "La salida está por los cubículos, rápido."
        };

        [Header("Daño (M1911)")]
        [SerializeField] private int _maxHealth = 3;
        [Tooltip("Daño por disparo según la zona: torso / cabeza / piernas.")]
        [SerializeField] private int _torsoDamage = 1;
        [SerializeField] private int _headDamage = 3;
        [SerializeField] private int _legDamage = 1;
        [Tooltip("Aturdimiento por zona: torso / cabeza / piernas (segundos).")]
        [SerializeField] private float _stunDuration = 0.6f;
        [SerializeField] private float _headStunDuration = 1.3f;
        [SerializeField] private float _legStunDuration = 0.9f;
        [Tooltip("Un disparo en la pierna lo hace cojear: multiplica su velocidad durante unos segundos.")]
        [SerializeField, Range(0.3f, 1f)] private float _limpSpeedFactor = 0.6f;
        [SerializeField] private float _limpDuration = 5f;
        [SerializeField] private float _downedDuration = 20f;
        [Tooltip("Pieza visual que se tumba al caer (por defecto el hijo 'Body').")]
        [SerializeField] private Transform _visualRoot;
        [Tooltip("Si está activo, al agotar la salud cae y se levanta tras _downedDuration; si no, muere definitivamente.")]
        [SerializeField] private bool _reviveAfterDowned = false;

        [Header("Sigilo del jugador")]
        [Tooltip("Multiplicador del alcance de vista cuando el jugador está agachado.")]
        [SerializeField, Range(0.2f, 1f)] private float _crouchSightFactor = 0.55f;

        private NavMeshAgent _agent;
        private InfectedNavigationGuard _navigation;
        private Transform _player;
        private Transform _aimCamera;
        private PlayerMovement _playerMovement;
        private FlashlightSystem _playerFlashlight;
        private int _health;
        private float _incapacitatedUntil;
        private float _limpUntil;
        private bool _isDowned;
        private Coroutine _downedRoutine;
        private bool _isDead;
        private Vector3 _homePosition;
        private Quaternion _homeRotation;

        /// <summary>Se emite cuando un infectado muere definitivamente (LevelExitGate cuenta las bajas).</summary>
        public static event Action<SmartInfectedBrain> Died;
        private IInfectedState _currentState;
        private Coroutine _tickRoutine;
        private Coroutine _lookAroundRoutine;
        private WaitForSeconds _tickWait;
        private WaitForSeconds _farTickWait;
        private float _lastTickTime;

        private bool _heardNoise;
        private bool _canSeePlayer;
        private bool _hadSight;
        private float _awareness;
        private float _lastAttackTime = float.NegativeInfinity;
        private float _lastSabotageTime;
        private float _lastMimicryTime;
        private bool _isNearPlayer;

        // Infectados actualmente dentro de _nearbyRadius del jugador (compartido entre instancias)
        private static readonly HashSet<SmartInfectedBrain> NearbyInfected = new();

        /// <summary>Se emite cuando el infectado conecta un ataque cuerpo a cuerpo con el jugador.</summary>
        public event Action<SmartInfectedBrain> PlayerAttacked;

        public PatrolState Patrol { get; private set; }
        public SuspiciousState Suspicious { get; private set; }
        public InvestigateNoiseState Investigate { get; private set; }
        public SearchState Search { get; private set; }
        public ChaseState Chase { get; private set; }
        public AttackState Attack { get; private set; }
        public ReturnState Return { get; private set; }
        public SabotageLightState Sabotage { get; private set; }
        public MimicryAmbushState Mimicry { get; private set; }

        public NavMeshAgent Agent => _agent;
        public Transform Player => _player;
        public Transform[] PatrolPoints => _patrolPoints;
        public InfectedBehavior Behavior => _behavior;
        public InfectedLearningMemory Memory => _memory;
        public Vector2 PatrolPause => _patrolPause;
        public float WanderRadius => _wanderRadius;
        public Vector3 HomePosition => _homePosition;
        public Quaternion HomeRotation => _homeRotation;
        public float WalkSpeed => _walkSpeed;
        public float RunSpeed => _runSpeed;
        public float AttackRange => _attackRange;
        public float SabotageRadius => _sabotageRadius;
        public float TickInterval => _tickInterval;
        public LayerMask ObstacleMask => _obstacleMask;
        public IInfectedState CurrentState => _currentState;

        public Vector3 LastNoisePos { get; private set; }
        /// <summary>El último ruido fue fuerte o cercano: merece investigarse directamente (no solo mirar).</summary>
        public bool LastNoiseAlarming { get; private set; }
        public Vector3 LastKnownPlayerPos { get; set; }
        /// <summary>Dónde vio "algo" sin llegar a reconocer al jugador (detección gradual).</summary>
        public Vector3 SuspicionPoint { get; private set; }

        /// <summary>Resultado del sensor de vista calculado al inicio del tick actual (jugador reconocido).</summary>
        public bool CanSeePlayer => _canSeePlayer;
        /// <summary>Ve una silueta pero aún no la reconoce (nivel de alerta entre 0.25 y 1).</summary>
        public bool SeesSomething => !_canSeePlayer && _hadSight && _awareness >= 0.25f;
        /// <summary>Nivel de alerta visual acumulado (0-1).</summary>
        public float Awareness => _awareness;
        /// <summary>El guardia de navegación agotó sus intentos hacia el destino actual.</summary>
        public bool NavigationBlocked => _navigation != null && _navigation.GaveUp;
        public bool CanAttackNow => Time.time - _lastAttackTime >= _attackCooldown;

        public bool SabotageReady => _sabotageEnabled && Time.time - _lastSabotageTime >= _sabotageCooldown;
        public bool MimicryReady => _mimicryEnabled && _player != null && Time.time - _lastMimicryTime >= _mimicryCooldown;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _navigation = new InfectedNavigationGuard(_agent);
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_animator != null) _animator.applyRootMotion = false;
            if (_eyes == null) _eyes = transform;
            if (_visualRoot == null) _visualRoot = transform.Find("Body");
            if (_reactions == null) _reactions = GetComponent<InfectedHitReactions>();
            if (_memory == null) _memory = GetComponent<InfectedLearningMemory>();
            _health = _maxHealth;
            _homePosition = transform.position;
            _homeRotation = transform.rotation;

            _tickWait = new WaitForSeconds(_tickInterval);
            _farTickWait = new WaitForSeconds(FarTickInterval);

            // Los cooldowns arrancan corriendo para que no sabotee ni imite nada más aparecer
            _lastSabotageTime = Time.time;
            _lastMimicryTime = Time.time;

            Patrol = new PatrolState(this);
            Suspicious = new SuspiciousState(this);
            Investigate = new InvestigateNoiseState(this);
            Search = new SearchState(this);
            Chase = new ChaseState(this);
            Attack = new AttackState(this);
            Return = new ReturnState(this);
            Sabotage = new SabotageLightState(this);
            Mimicry = new MimicryAmbushState(this);
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
            _playerFlashlight = player != null ? player.GetComponent<FlashlightSystem>() : null;
            Camera aim = player != null ? player.GetComponentInChildren<Camera>() : null;
            _aimCamera = aim != null ? aim.transform : null;
            if (patrolPoints != null && patrolPoints.Length > 0)
            {
                _patrolPoints = patrolPoints;
            }

            if (_agent != null && !_agent.isOnNavMesh)
            {
                _agent.Warp(transform.position);
            }
            _homePosition = transform.position;
            _homeRotation = transform.rotation;
        }

        /// <summary>
        /// Ajustes por punto de aparición (perfil, sigilo, mimetismo...). Se aplica antes de que arranque la FSM.
        /// </summary>
        public void ApplyOverrides(InfectedSpawnOverrides overrides)
        {
            if (overrides == null || !overrides.Enabled) return;

            _behavior = overrides.Behavior;
            _gradedDetection = overrides.GradedDetection;
            _mimicryEnabled = overrides.MimicryEnabled;
            _sabotageEnabled = overrides.SabotageEnabled;
            if (overrides.MimicMessages != null && overrides.MimicMessages.Length > 0) _mimicMessages = overrides.MimicMessages;
            if (overrides.MaxHealth > 0) { _maxHealth = overrides.MaxHealth; _health = _maxHealth; }
            if (overrides.SightDistance > 0f) _sightDistance = overrides.SightDistance;
            if (overrides.HearingRange > 0f) _maxHearingRange = overrides.HearingRange;
            if (overrides.MimicryCooldown > 0f) _mimicryCooldown = overrides.MimicryCooldown;
            if (overrides.EnableLearning && _memory == null) _memory = gameObject.AddComponent<InfectedLearningMemory>();
            if (_behavior == InfectedBehavior.Crawler) ConfigureCrawler();
        }

        /// <summary>
        /// Perfil reptante: tumba el modelo boca abajo sobre el agente (sin animación de arrastre propia: el ciclo de
        /// andar con los brazos del Blend Tree hace de reptado), baja ojos y colisión, y lo vuelve lento pero
        /// traicionero a corta distancia.
        /// </summary>
        private void ConfigureCrawler()
        {
            _walkSpeed *= 0.35f;
            _runSpeed = 1.5f;
            _attackRange = 1.3f;
            _sightDistance = Mathf.Min(_sightDistance, 8f);
            _sightAngle = 120f;
            _playerTargetHeight = 0.6f;
            _sabotageEnabled = false;
            _mimicryEnabled = false;

            if (_eyes != null) _eyes.localPosition = new Vector3(0f, 0.35f, 0.9f);

            // El cuerpo gira sobre los pies: se adelanta media altura para que quede centrado sobre el agente
            if (_visualRoot != null)
            {
                Quaternion prone = Quaternion.Euler(82f, 0f, 0f);
                Vector3 offset = new Vector3(0f, 0.12f, -0.85f);
                _visualRoot.localRotation = prone;
                _visualRoot.localPosition = offset;
                if (_reactions != null) _reactions.SetRestPose(prone, offset);
            }

            // Colisión baja y alargada: los disparos deben apuntar al suelo
            var capsule = GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                capsule.direction = 2;
                capsule.radius = 0.32f;
                capsule.height = 1.8f;
                capsule.center = new Vector3(0f, 0.32f, 0f);
            }
            if (_agent != null)
            {
                _agent.height = 0.7f;
                _agent.angularSpeed = 160f;
            }
        }

        /// <summary>Aviso diegético del agarre del reptante (una vez por ataque).</summary>
        private void AnnounceCrawlerGrab()
        {
            if (_behavior == InfectedBehavior.Crawler) GameEvents.RaiseSubtitle("(¡Algo te agarra del tobillo desde el suelo!)");
        }

        private IEnumerator TickRoutine()
        {
            // Esperar un tick para que el agente quede colocado sobre el NavMesh
            yield return _tickWait;
            _lastTickTime = Time.time;

            if (_currentState == null) ChangeState(Patrol);

            while (true)
            {
                float now = Time.time;
                float dt = now - _lastTickTime;
                _lastTickTime = now;

                // Aturdido por un disparo, en el suelo o forcejeando: no percibe ni decide
                if (now < _incapacitatedUntil)
                {
                    UpdateAnimatorSpeed();
                    yield return _tickWait;
                    continue;
                }

                _canSeePlayer = EvaluateSight(dt);
                if (_canSeePlayer)
                {
                    LastKnownPlayerPos = _player.position;
                    if (_memory != null) _memory.RecordSighting(LastKnownPlayerPos);
                }

                _navigation.Tick(now);
                if (_memory != null) _memory.Tick(now);
                _currentState?.Tick();
                UpdateAnimatorSpeed();

                float distance = DistanceToPlayer();
                UpdateProximity(distance <= _nearbyRadius);

                // Lejos del jugador y sin nada que hacer: piensa menos a menudo
                bool idleFar = distance > FarTickDistance && (_currentState == Patrol || _currentState == Return);
                yield return idleFar ? _farTickWait : _tickWait;
            }
        }

        // ---------- Daño ----------

        public bool IsDowned => _isDowned;
        public bool IsDead => _isDead;
        public bool IsLimping => Time.time < _limpUntil;

        /// <summary>
        /// Impacto de bala. La zona (cabeza, torso, piernas) se obtiene repitiendo el rayo de la cámara del jugador:
        /// cabeza = más daño y aturdimiento, piernas = cojera. Reacciona con retroceso y persigue hacia el disparo.
        /// </summary>
        public void ApplyHit(Vector3 shotOrigin)
        {
            if (_isDowned || _isDead) return;

            HitZone zone = _reactions != null ? _reactions.ResolveZone(_aimCamera) : HitZone.Torso;
            int damage = zone == HitZone.Head ? _headDamage : zone == HitZone.Legs ? _legDamage : _torsoDamage;

            LastKnownPlayerPos = shotOrigin;
            _awareness = 1f;
            _health -= Mathf.Max(1, damage);
            StopMoving();
            StopLookAround();

            if (_health <= 0)
            {
                if (_reviveAfterDowned) _downedRoutine = StartCoroutine(DownedRoutine());
                else Die(shotOrigin);
                return;
            }

            float stun = zone == HitZone.Head ? _headStunDuration : zone == HitZone.Legs ? _legStunDuration : _stunDuration;
            if (zone == HitZone.Legs) _limpUntil = Time.time + _limpDuration;
            _incapacitatedUntil = Time.time + stun;
            if (_reactions != null) _reactions.PlayHit(zone, shotOrigin, stun);
            ChangeState(Chase);
        }

        /// <summary>Muerte definitiva: se desploma, deja de percibir y de bloquear el paso.</summary>
        private void Die(Vector3 shotOrigin)
        {
            _isDead = true;
            _incapacitatedUntil = float.PositiveInfinity;
            StopMoving();
            if (_tickRoutine != null) { StopCoroutine(_tickRoutine); _tickRoutine = null; }
            StopLookAround();
            SetAlerted(false);
            UpdateProximity(false);
            if (_animator != null) _animator.enabled = false;
            foreach (Collider c in GetComponents<Collider>()) c.enabled = false;
            if (_voiceSource != null) _voiceSource.Stop();

            if (_reactions != null)
            {
                // El agente se apaga al terminar la caída (la caída lo desliza sobre el NavMesh)
                _reactions.PlayDeath(shotOrigin);
                StartCoroutine(DisableAgentAfter(1.1f));
            }
            else
            {
                if (_visualRoot != null) _visualRoot.localRotation = Quaternion.Euler(-88f, 0f, 0f);
                if (_agent != null) _agent.enabled = false;
            }
            Died?.Invoke(this);
        }

        private IEnumerator DisableAgentAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (_agent != null) _agent.enabled = false;
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
        /// Fin del forcejeo. Si el jugador lo empuja, cae de espaldas y se levanta unos segundos después
        /// (sin teletransportarse); si el jugador falla, se queda un instante aturdido para que pueda huir.
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
            const float downTime = 2.8f;
            _incapacitatedUntil = Time.time + downTime + 0.9f;
            Vector3 pusher = _player != null ? _player.position : transform.position + transform.forward;

            if (_reactions != null)
            {
                _reactions.PlayKnockdown(pusher, downTime);
            }
            else if (_visualRoot != null)
            {
                _visualRoot.localRotation = Quaternion.Euler(-80f, 0f, 0f);
            }

            yield return new WaitForSeconds(downTime + 0.9f);
            if (_reactions == null && _visualRoot != null && !_isDead) _visualRoot.localRotation = Quaternion.identity;
            if (!_isDead) ChangeState(Chase);
        }

        private IEnumerator DownedRoutine()
        {
            _isDowned = true;
            _incapacitatedUntil = float.PositiveInfinity;
            StopMoving();
            SetAlerted(false);
            UpdateProximity(false);
            if (_reactions != null) _reactions.PlayKnockdown(LastKnownPlayerPos, _downedDuration);
            else if (_visualRoot != null) _visualRoot.localRotation = Quaternion.Euler(-85f, 0f, 0f);

            yield return new WaitForSeconds(_downedDuration);

            if (_reactions == null && _visualRoot != null) _visualRoot.localRotation = Quaternion.identity;
            _health = _maxHealth;
            _isDowned = false;
            _incapacitatedUntil = 0f;
            _downedRoutine = null;
            TriggerScream();
            Search.SetCenter(LastKnownPlayerPos);
            ChangeState(Search);
        }

        public void ChangeState(IInfectedState next)
        {
            if (next == null || next == _currentState) return;

            _currentState?.Exit();
            _currentState = next;
            _currentState.Enter();
        }

        // ---------- Sensores ----------

        private bool EvaluateSight(float dt)
        {
            _hadSight = false;
            if (_player == null) return false;

            // Agachado: silueta más baja y más difícil de distinguir a distancia
            bool crouching = _playerMovement != null && _playerMovement.IsCrouching;
            float sightDistance = crouching ? _sightDistance * _crouchSightFactor : _sightDistance;
            float targetHeight = crouching ? _playerTargetHeight * 0.6f : _playerTargetHeight;

            Vector3 origin = _eyes.position;
            Vector3 target = _player.position + Vector3.up * targetHeight;
            Vector3 toPlayer = target - origin;
            float distance = toPlayer.magnitude;

            // Escondido: solo lo sigue viendo quien ya lo estaba viendo al esconderse (y de cerca)
            if (PlayerHiding.IsHidden && !(_canSeePlayer && distance <= 6f))
            {
                _awareness = Mathf.Max(0f, _awareness - dt * 0.5f);
                return false;
            }

            bool inSight = distance <= sightDistance
                && Vector3.Angle(transform.forward, toPlayer) <= _sightAngle * 0.5f
                // La máscara excluye la capa Player: si no hay obstáculo en medio, hay línea de visión
                && !Physics.Raycast(origin, toPlayer / distance, distance, _obstacleMask, QueryTriggerInteraction.Ignore);

            if (!inSight)
            {
                _awareness = Mathf.Max(0f, _awareness - dt * 0.5f);
                return false;
            }

            _hadSight = true;
            SuspicionPoint = _player.position;

            if (!_gradedDetection || distance <= InstantSightDistance || _currentState == Chase || _currentState == Attack)
            {
                _awareness = 1f;
                return true;
            }

            // Cerca se reconoce rápido; lejos, despacio. La linterna y correr delatan; agacharse ayuda.
            float distanceFactor = Mathf.Lerp(2.4f, 0.45f, distance / Mathf.Max(0.01f, sightDistance));
            float visibility = crouching ? 0.55f : 1f;
            if (_playerFlashlight != null && _playerFlashlight.IsOn) visibility *= 1.5f;
            if (_playerMovement != null && _playerMovement.IsSprinting) visibility *= 1.35f;

            _awareness = Mathf.Min(1f, _awareness + dt * distanceFactor * visibility / Mathf.Max(0.1f, _detectionTime));
            return _awareness >= 1f;
        }

        private void HandleNoiseEmitted(Vector3 position, float intensity)
        {
            if (_isDead) return;

            float multiplier = (_behavior == InfectedBehavior.Listener ? 1.3f : 1f) * (_memory != null ? _memory.HearingMultiplier : 1f);
            float range = Mathf.Clamp(intensity, 0f, 100f) / 100f * _maxHearingRange * multiplier;
            float sqrDistance = (position - transform.position).sqrMagnitude;
            if (sqrDistance > range * range) return;

            // Las paredes amortiguan el sonido: un ruido tras un muro solo se oye de cerca
            Vector3 ears = _eyes != null ? _eyes.position : transform.position + Vector3.up * 1.6f;
            bool occluded = Physics.Linecast(position + Vector3.up * 0.8f, ears, _obstacleMask, QueryTriggerInteraction.Ignore);
            float effectiveRange = occluded ? range * _occludedHearingFactor : range;
            if (sqrDistance > effectiveRange * effectiveRange) return;

            LastNoisePos = position;
            LastNoiseAlarming = intensity >= 85f || sqrDistance <= (effectiveRange * 0.45f) * (effectiveRange * 0.45f);
            _heardNoise = true;
            if (_memory != null) _memory.RecordNoise(position, intensity);
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

        /// <summary>
        /// Reacción común de los estados tranquilos (patrulla y regreso) a lo que percibe.
        /// Devuelve true si cambió de estado.
        /// </summary>
        public bool ReactToStimuli()
        {
            if (CanSeePlayer)
            {
                ChangeState(Chase);
                return true;
            }

            if (SeesSomething)
            {
                Suspicious.SetTarget(SuspicionPoint);
                ChangeState(Suspicious);
                return true;
            }

            if (ConsumeHeardNoise())
            {
                if (LastNoiseAlarming || _behavior == InfectedBehavior.Listener)
                {
                    Investigate.SetTarget(LastNoisePos);
                    ChangeState(Investigate);
                }
                else
                {
                    Suspicious.SetTarget(LastNoisePos);
                    ChangeState(Suspicious);
                }
                return true;
            }
            return false;
        }

        /// <summary>Al perder el rastro: registra el escondite probable y busca en los alrededores.</summary>
        public void LoseTrackOfPlayer()
        {
            if (_memory != null) _memory.RecordLostPlayer(LastKnownPlayerPos);
            Search.SetCenter(LastKnownPlayerPos);
            ChangeState(Search);
        }

        // ---------- Utilidades para los estados ----------

        public void MoveTo(Vector3 destination, float speed)
        {
            if (_agent == null || !_agent.isOnNavMesh) return;

            _agent.isStopped = false;
            _agent.speed = IsLimping ? speed * _limpSpeedFactor : speed;
            _navigation.SetDestination(destination);
        }

        public void StopMoving()
        {
            _navigation?.Clear();
            if (_agent == null || !_agent.isOnNavMesh) return;

            _agent.isStopped = true;
            _agent.ResetPath();
        }

        public bool HasArrived(float tolerance = 0.3f)
        {
            if (_agent == null || !_agent.isOnNavMesh || _agent.pathPending) return false;
            if (_navigation.IsOnDetour) return false;
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
        public void TriggerAttackAnimation() => SetAnimatorTrigger(AttackTriggerHash);

        /// <summary>
        /// Lanza el ataque si el cooldown lo permite. Devuelve true si se ejecutó.
        /// </summary>
        public bool TryAttack()
        {
            if (!CanAttackNow) return false;

            FacePlayer();
            TriggerAttackAnimation();
            DeliverAttack();
            return true;
        }

        /// <summary>Conecta el golpe (tras la anticipación de AttackState): PlayerHealth decide forcejeo o daño.</summary>
        public void DeliverAttack()
        {
            _lastAttackTime = Time.time;
            AnnounceCrawlerGrab();
            PlayerAttacked?.Invoke(this);
            // PlayerHealth decide: forcejeo (QTE), golpe acumulado o captura si se agota la vida
            GameEvents.RaisePlayerAttacked(transform);
        }

        public void FacePlayer()
        {
            if (_player == null) return;
            FacePoint(_player.position);
        }

        public void FacePoint(Vector3 point)
        {
            Vector3 flat = point - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(flat);
        }

        /// <summary>True si el jugador está delante (para que el golpe no conecte por la espalda del infectado).</summary>
        public bool IsPlayerInFront(float maxAngle = 70f)
        {
            if (_player == null) return false;
            Vector3 flat = _player.position - transform.position;
            flat.y = 0f;
            return Vector3.Angle(transform.forward, flat) <= maxAngle;
        }

        public void MarkSabotageUsed() => _lastSabotageTime = Time.time;
        public void MarkMimicryUsed() => _lastMimicryTime = Time.time;

        /// <summary>
        /// Reproduce la voz distorsionada (más grave y con eco) y envía la notificación falsa (IsFake) al HUD.
        /// </summary>
        public void PlayMimicry()
        {
            if (_voiceSource != null && _mimicClips != null && _mimicClips.Length > 0)
            {
                AudioClip clip = _mimicClips[Random.Range(0, _mimicClips.Length)];
                if (clip != null)
                {
                    _voiceSource.pitch = _mimicPitch;
                    _voiceSource.PlayOneShot(clip);
                }
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

        /// <summary>Gira suavemente hasta encarar un punto (reacción de sospecha).</summary>
        public void StartTurnTowards(Vector3 point, float degreesPerSecond = 180f)
        {
            StopLookAround();
            _lookAroundRoutine = StartCoroutine(TurnTowardsRoutine(point, degreesPerSecond));
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

        private IEnumerator TurnTowardsRoutine(Vector3 point, float degreesPerSecond)
        {
            Vector3 flat = point - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.001f) { _lookAroundRoutine = null; yield break; }

            Quaternion target = Quaternion.LookRotation(flat);
            while (Quaternion.Angle(transform.rotation, target) > 1f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, degreesPerSecond * Time.deltaTime);
                yield return null;
            }
            transform.rotation = target;
            _lookAroundRoutine = null;
        }

        /// <summary>Busca un punto aleatorio del NavMesh alcanzable alrededor de un centro.</summary>
        public bool TryGetRandomPoint(Vector3 center, float radius, out Vector3 point)
        {
            for (int i = 0; i < 6; i++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                Vector3 candidate = center + new Vector3(offset.x, 0f, offset.y);
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.5f, _agent.areaMask))
                {
                    point = hit.position;
                    return true;
                }
            }
            point = center;
            return false;
        }

        private void UpdateAnimatorSpeed()
        {
            if (_animator == null || _agent == null || !_animator.enabled) return;
            _animator.SetFloat(SpeedHash, _agent.enabled ? _agent.velocity.magnitude : 0f);
        }

        private void SetAnimatorTrigger(int hash)
        {
            if (_animator != null && _animator.enabled) _animator.SetTrigger(hash);
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
