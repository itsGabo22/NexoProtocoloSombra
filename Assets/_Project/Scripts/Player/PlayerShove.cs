using Nexo.AI.Infected;
using Nexo.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nexo.Player
{
    /// <summary>
    /// Empujón [G] (siempre disponible, enfriamiento 1,5 s): aparta al infectado más cercano que esté delante, lo
    /// desequilibra ~1,2 s y lo hace retroceder, con algo de ruido. Si un infectado tiene agarrado al jugador
    /// (forcejeo), el empujón rompe el agarre al instante. Sin Update: acción de entrada por callback.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerShove : MonoBehaviour
    {
        private static readonly Collider[] s_hits = new Collider[16];
        private const int InfectedMask = (1 << 0) | (1 << 9); // Default | Infected

        [SerializeField] private PlayerMovement _movement;
        [SerializeField] private StruggleQTE _struggle;
        [SerializeField] private Transform _view;
        [SerializeField] private AudioSource _audio;
        [SerializeField] private AudioClip _shoveClip;

        private InputAction _action;
        private float _lastUse = -10f;

        public float LastUse => _lastUse;
        public bool IsReady => MeleeRules.Ready(Time.time, _lastUse, MeleeRules.ShoveCooldown);
        /// <summary>Infectados apartados (comprobaciones del Editor).</summary>
        public int Shoves { get; private set; }

        private void Awake()
        {
            if (_movement == null) _movement = GetComponent<PlayerMovement>();
            if (_struggle == null) _struggle = GetComponent<StruggleQTE>();
            if (_view == null)
            {
                Camera cam = GetComponentInChildren<Camera>();
                _view = cam != null ? cam.transform : transform;
            }
            _action = new InputAction("Shove", InputActionType.Button, "<Keyboard>/g");
            _action.AddBinding("<Gamepad>/buttonEast");
        }

        private void OnEnable()
        {
            _action.performed += HandleShovePerformed;
            _action.Enable();
        }

        private void OnDisable()
        {
            _action.performed -= HandleShovePerformed;
            _action.Disable();
        }

        private void OnDestroy() => _action?.Dispose();

        private void HandleShovePerformed(InputAction.CallbackContext context) => TryShove();

        /// <summary>Intenta empujar; devuelve true si apartó a un infectado o rompió un agarre.</summary>
        public bool TryShove()
        {
            if (!IsReady || UiTextEntry.BlocksHotkeys || Time.timeScale <= 0f) return false;

            // Agarrado: el empujón rompe el forcejeo (cuenta como éxito)
            if (_struggle != null && _struggle.IsActive)
            {
                _lastUse = Time.time;
                _struggle.BreakFree();
                Shoves++;
                Feedback();
                return true;
            }

            if (_movement != null && _movement.IsInputLocked) return false;
            if (PlayerHiding.IsHidden) return false;

            _lastUse = Time.time;
            SmartInfectedBrain target = FindTarget();
            Feedback();
            GameEvents.RaiseNoiseEmitted(transform.position, MeleeRules.ShoveNoise);
            if (target == null) return false;

            target.ApplyShove(transform.position, MeleeRules.ShoveStagger, MeleeRules.ShovePushMeters);
            Shoves++;
            GameEvents.RaiseSubtitle("(Lo empujas lejos de ti)");
            return true;
        }

        private SmartInfectedBrain FindTarget()
        {
            Vector3 origin = transform.position;
            Vector3 forward = _view != null ? _view.forward : transform.forward;
            int count = Physics.OverlapSphereNonAlloc(origin + Vector3.up, MeleeRules.ShoveRange + 0.5f, s_hits, InfectedMask, QueryTriggerInteraction.Ignore);
            SmartInfectedBrain best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                SmartInfectedBrain brain = s_hits[i].GetComponentInParent<SmartInfectedBrain>();
                if (brain == null || brain.IsDead) continue;
                Vector3 p = brain.transform.position;
                if (!MeleeRules.InCone(origin, forward, p, MeleeRules.ShoveRange, MeleeRules.ShoveHalfAngle)) continue;
                float d = (p - origin).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = brain; }
            }
            return best;
        }

        private void Feedback()
        {
            if (_audio != null && _shoveClip != null) _audio.PlayOneShot(_shoveClip, 0.8f);
        }
    }
}
