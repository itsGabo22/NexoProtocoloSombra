using System.Collections;
using Nexo.AI.Infected;
using Nexo.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Nexo.Player
{
    /// <summary>
    /// Cuchillo [X]: arma cuerpo a cuerpo que se consigue como botín (el patrullero del Piso 12 lo suelta al morir).
    /// Tajo rápido, corto alcance y poco ruido: 2 puntos de daño de frente y ejecución silenciosa (letal) si el infectado
    /// no ha detectado al jugador y se le ataca por la espalda. Se conserva entre pisos (GameSession) y el HUD muestra su
    /// estado en el panel del arma (GameEvents.OnKnifeChanged). El modelo del cuchillo es procedural (hoja + mango)
    /// y solo se ve durante el tajo. Sin Update: acción por callback y corrutina corta de animación.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerKnife : MonoBehaviour
    {
        private const int InfectedMask = (1 << 0) | (1 << 9); // Default | Infected

        [SerializeField] private PlayerMovement _movement;
        [SerializeField] private Transform _view;
        [SerializeField] private AudioSource _audio;
        [SerializeField] private AudioClip _slashClip;
        [SerializeField] private AudioClip _hitClip;
        [SerializeField] private Material _bladeMaterial;
        [SerializeField] private Material _handleMaterial;

        private static readonly RaycastHit[] s_hits = new RaycastHit[8];
        private InputAction _action;
        private float _lastUse = -10f;
        private Transform _model;
        private Coroutine _swing;

        public static PlayerKnife Current { get; private set; }
        public bool HasKnife { get; private set; }
        public bool IsReady => HasKnife && MeleeRules.Ready(Time.time, _lastUse, MeleeRules.KnifeCooldown);
        public int Hits { get; private set; }
        public int Takedowns { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Current = null;

        private void Awake()
        {
            if (_movement == null) _movement = GetComponent<PlayerMovement>();
            if (_view == null)
            {
                Camera cam = GetComponentInChildren<Camera>();
                _view = cam != null ? cam.transform : transform;
            }
            _action = new InputAction("Knife", InputActionType.Button, "<Keyboard>/x");
            _action.AddBinding("<Gamepad>/buttonNorth");
        }

        private void OnEnable()
        {
            Current = this;
            _action.performed += HandleKnifePerformed;
            _action.Enable();
        }

        private void OnDisable()
        {
            if (Current == this) Current = null;
            _action.performed -= HandleKnifePerformed;
            _action.Disable();
            if (_model != null) _model.gameObject.SetActive(false);
        }

        private void OnDestroy() => _action?.Dispose();

        /// <summary>Da o quita el cuchillo (botín, persistencia entre pisos o partida nueva).</summary>
        public void SetHasKnife(bool has)
        {
            if (HasKnife == has) return;
            HasKnife = has;
            GameEvents.RaiseKnifeChanged(has);
        }

        private void HandleKnifePerformed(InputAction.CallbackContext context) => TrySlash();

        public bool TrySlash()
        {
            if (!IsReady || UiTextEntry.BlocksHotkeys || Time.timeScale <= 0f) return false;
            if ((_movement != null && _movement.IsInputLocked) || PlayerHiding.IsHidden) return false;

            _lastUse = Time.time;
            if (_audio != null && _slashClip != null) _audio.PlayOneShot(_slashClip, 0.6f);
            GameEvents.RaiseNoiseEmitted(transform.position, MeleeRules.KnifeNoise);
            if (_swing != null) StopCoroutine(_swing);
            _swing = StartCoroutine(SwingRoutine());

            SmartInfectedBrain target = FindTarget();
            if (target == null) return false;

            bool stealth = MeleeRules.IsStealthTakedown(target.IsAwareOfPlayer, target.transform.forward, target.transform.position, transform.position);
            target.ApplyMeleeHit(transform.position, stealth);
            Hits++;
            if (stealth)
            {
                Takedowns++;
                GameEvents.RaiseSubtitle("(Lo derribas en silencio por la espalda)");
            }
            if (_audio != null && _hitClip != null) _audio.PlayOneShot(_hitClip, 0.7f);
            return true;
        }

        private SmartInfectedBrain FindTarget()
        {
            if (_view == null) return null;
            int count = Physics.SphereCastNonAlloc(_view.position, 0.3f, _view.forward, s_hits, MeleeRules.KnifeRange, InfectedMask, QueryTriggerInteraction.Ignore);
            SmartInfectedBrain best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                SmartInfectedBrain brain = s_hits[i].collider.GetComponentInParent<SmartInfectedBrain>();
                if (brain == null || brain.IsDead) continue;
                if (s_hits[i].distance < bestDist) { bestDist = s_hits[i].distance; best = brain; }
            }
            if (best != null) return best;
            // Muy pegado (dentro de la cápsula del infectado el SphereCast no lo detecta): cono corto delante
            Collider[] near = Physics.OverlapSphere(transform.position + Vector3.up, MeleeRules.KnifeRange * 0.7f, InfectedMask, QueryTriggerInteraction.Ignore);
            foreach (Collider c in near)
            {
                SmartInfectedBrain brain = c.GetComponentInParent<SmartInfectedBrain>();
                if (brain != null && !brain.IsDead && MeleeRules.InCone(transform.position, _view.forward, brain.transform.position, MeleeRules.KnifeRange, MeleeRules.KnifeHalfAngle)) return brain;
            }
            return null;
        }

        // ---------- Modelo y animación del tajo ----------

        private IEnumerator SwingRoutine()
        {
            Transform model = EnsureModel();
            if (model == null) yield break;
            model.gameObject.SetActive(true);
            Quaternion from = Quaternion.Euler(-20f, -55f, 35f), to = Quaternion.Euler(10f, 50f, -25f);
            Vector3 rest = new Vector3(0.28f, -0.25f, 0.45f);
            const float duration = 0.22f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                model.localRotation = Quaternion.Slerp(from, to, k * k * (3f - 2f * k));
                model.localPosition = rest + new Vector3(-0.25f * k, 0.05f * Mathf.Sin(k * Mathf.PI), 0.12f * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            yield return new WaitForSeconds(0.08f);
            model.gameObject.SetActive(false);
            _swing = null;
        }

        private Transform EnsureModel()
        {
            if (_model != null) return _model;
            if (_view == null) return null;
            _model = BuildKnifeModel(_view, _bladeMaterial, _handleMaterial).transform;
            _model.gameObject.SetActive(false);
            return _model;
        }

        /// <summary>Cuchillo procedural (hoja + guarda + mango) sin colisiones. Lo usan también el objeto de botín y el Editor.</summary>
        public static GameObject BuildKnifeModel(Transform parent, Material blade, Material handle)
        {
            var root = new GameObject("Knife_Model");
            root.transform.SetParent(parent, false);
            AddPart(root.transform, "Blade", new Vector3(0f, 0f, 0.13f), new Vector3(0.012f, 0.035f, 0.2f), blade);
            AddPart(root.transform, "Tip", new Vector3(0f, -0.004f, 0.235f), new Vector3(0.01f, 0.022f, 0.04f), blade);
            AddPart(root.transform, "Guard", new Vector3(0f, 0f, 0.025f), new Vector3(0.03f, 0.06f, 0.012f), handle);
            AddPart(root.transform, "Handle", new Vector3(0f, 0f, -0.045f), new Vector3(0.026f, 0.032f, 0.12f), handle);
            return root;
        }

        private static void AddPart(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Collider col = go.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(col); else DestroyImmediate(col);
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            if (mat != null) r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
