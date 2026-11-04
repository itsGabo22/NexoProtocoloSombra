using System.Collections;
using Nexo.AI.Infected;
using Nexo.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Nexo.Player
{
    /// <summary>
    /// Forcejeo (QTE) cuando un infectado agarra al jugador ileso. Abajo aparece una barra roja con una zona azul y
    /// un indicador que va y viene; hay que pulsar [Espacio] cuando el indicador está dentro de la zona azul.
    /// Dos aciertos seguidos empujan al infectado; cada fallo es un golpe (PlayerHealth) y tres fallos o agotar
    /// el tiempo hacen perder un punto de vida. Durante el forcejeo no se puede mover ni disparar.
    /// UI Toolkit construida en código sobre el UIDocument asignado (sin uGUI).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StruggleQTE : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private UIDocument _document;
        [SerializeField] private PlayerMovement _playerMovement;
        [SerializeField] private PlayerHealth _health;

        [Header("Reglas")]
        [SerializeField, Min(1)] private int _hitsToPush = 2;
        [SerializeField, Min(1)] private int _missesToFail = 3;
        [SerializeField] private float _timeLimit = 7f;
        [SerializeField] private float _indicatorSpeed = 0.9f;
        [SerializeField, Range(0.06f, 0.4f)] private float _zoneWidth = 0.16f;

        private InputAction _pushAction;
        private VisualElement _panel;
        private VisualElement _zone;
        private VisualElement _indicator;
        private Label _label;
        private SmartInfectedBrain _attacker;
        private Coroutine _routine;
        private float _indicatorPos;
        private float _direction = 1f;
        private float _zoneStart;
        private float _speed;
        private int _streak;
        private int _misses;

        public bool IsActive { get; private set; }

        private void Awake()
        {
            _pushAction = new InputAction("StrugglePush", InputActionType.Button);
            _pushAction.AddBinding("<Keyboard>/space");
            _pushAction.AddBinding("<Gamepad>/buttonSouth");
            if (_playerMovement == null) _playerMovement = GetComponent<PlayerMovement>();
            if (_health == null) _health = GetComponent<PlayerHealth>();
        }

        private void OnEnable()
        {
            _pushAction.performed += HandlePush;
            GameEvents.OnPlayerCaught += HandleAbort;
            GameEvents.OnFloorChanged += HandleFloorChanged;
            GameEvents.OnCinematicStarted += HandleCinematicStarted; // cinematics-ready
            BuildUi();
        }

        private void OnDisable()
        {
            _pushAction.performed -= HandlePush;
            _pushAction.Disable();
            GameEvents.OnPlayerCaught -= HandleAbort;
            GameEvents.OnFloorChanged -= HandleFloorChanged;
            GameEvents.OnCinematicStarted -= HandleCinematicStarted; // cinematics-ready
            if (IsActive) Finish(false, false);
        }

        private void OnDestroy() => _pushAction?.Dispose();

        private void HandleAbort() { if (IsActive) Finish(false, false); }

        private void HandleFloorChanged(int floor) { if (IsActive) Finish(false, false); }

        // cinematics-ready: Espacio salta la cinemática; si siguiera el forcejeo contaría como empujón fallido (y golpe)
        private void HandleCinematicStarted(string id) { if (IsActive) Finish(false, false); }

        // ---------- UI ----------

        private void BuildUi()
        {
            if (_document == null || _document.rootVisualElement == null || _panel != null) return;

            _panel = new VisualElement { pickingMode = PickingMode.Ignore };
            _panel.style.position = Position.Absolute;
            _panel.style.left = Length.Percent(25); _panel.style.right = Length.Percent(25);
            _panel.style.bottom = Length.Percent(12);
            _panel.style.alignItems = Align.Center;
            _panel.style.display = DisplayStyle.None;

            _label = new Label { pickingMode = PickingMode.Ignore };
            _label.style.color = Color.white;
            _label.style.fontSize = 20;
            _label.style.unityFontStyleAndWeight = FontStyle.Bold;
            _label.style.marginBottom = 8;
            _panel.Add(_label);

            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.style.width = Length.Percent(100); bar.style.height = 26;
            bar.style.backgroundColor = new Color(0.75f, 0.08f, 0.08f, 0.92f);
            bar.style.borderTopWidth = bar.style.borderBottomWidth = bar.style.borderLeftWidth = bar.style.borderRightWidth = 2;
            bar.style.borderTopColor = bar.style.borderBottomColor = bar.style.borderLeftColor = bar.style.borderRightColor = new Color(0.1f, 0.02f, 0.02f);
            _panel.Add(bar);

            _zone = new VisualElement { pickingMode = PickingMode.Ignore };
            _zone.style.position = Position.Absolute; _zone.style.top = 0; _zone.style.bottom = 0;
            _zone.style.backgroundColor = new Color(0.15f, 0.55f, 1f, 0.95f);
            bar.Add(_zone);

            _indicator = new VisualElement { pickingMode = PickingMode.Ignore };
            _indicator.style.position = Position.Absolute; _indicator.style.top = -6; _indicator.style.bottom = -6;
            _indicator.style.width = 6; _indicator.style.marginLeft = -3;
            _indicator.style.backgroundColor = Color.white;
            bar.Add(_indicator);

            _document.rootVisualElement.Add(_panel);
        }

        private void RefreshUi()
        {
            if (_panel == null) return;
            _zone.style.left = Length.Percent(_zoneStart * 100f);
            _zone.style.width = Length.Percent(_zoneWidth * 100f);
            _indicator.style.left = Length.Percent(_indicatorPos * 100f);
            _label.text = $"¡FORCEJEO! Pulsa [ESPACIO] en la zona azul  ({_streak}/{_hitsToPush})";
        }

        // ---------- Flujo ----------

        /// <summary>Inicia el forcejeo contra el infectado indicado.</summary>
        public void Begin(SmartInfectedBrain attacker)
        {
            if (IsActive || attacker == null) return;
            BuildUi();

            IsActive = true;
            _attacker = attacker;
            _streak = 0;
            _misses = 0;
            _speed = _indicatorSpeed;
            _indicatorPos = 0f;
            _direction = 1f;
            RelocateZone();

            attacker.BeginGrapple();
            if (_playerMovement != null) _playerMovement.SetInputLocked(true, false);
            Vector3 toAttacker = attacker.transform.position - transform.position;
            toAttacker.y = 0f;
            if (toAttacker.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(toAttacker);

            if (_panel != null) _panel.style.display = DisplayStyle.Flex;
            _pushAction.Enable();
            GameEvents.RaiseSubtitle("(¡Te agarra! Forcejea para quitártelo de encima)");
            GameEvents.RaiseNoiseEmitted(transform.position, 60f);
            _routine = StartCoroutine(StruggleRoutine());
        }

        private IEnumerator StruggleRoutine()
        {
            float endTime = Time.time + _timeLimit;
            while (IsActive)
            {
                _indicatorPos += _direction * _speed * Time.deltaTime;
                if (_indicatorPos >= 1f) { _indicatorPos = 1f; _direction = -1f; }
                else if (_indicatorPos <= 0f) { _indicatorPos = 0f; _direction = 1f; }
                RefreshUi();

                if (Time.time >= endTime)
                {
                    Finish(false, true);
                    yield break;
                }
                yield return null;
            }
        }

        private void HandlePush(InputAction.CallbackContext context)
        {
            if (!IsActive) return;

            bool inZone = _indicatorPos >= _zoneStart && _indicatorPos <= _zoneStart + _zoneWidth;
            if (inZone)
            {
                _streak++;
                if (_streak >= _hitsToPush)
                {
                    Finish(true, false);
                    return;
                }
                _speed *= 1.25f;
            }
            else
            {
                _streak = 0;
                _misses++;
                if (_health != null) _health.RegisterHit();
                if (!IsActive) return;
                if (_misses >= _missesToFail)
                {
                    Finish(false, true);
                    return;
                }
            }
            RelocateZone();
            RefreshUi();
        }

        private void RelocateZone() => _zoneStart = Random.Range(0.05f, 0.95f - _zoneWidth);

        private void Finish(bool pushedBack, bool losePoint)
        {
            IsActive = false;
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            _pushAction.Disable();
            if (_panel != null) _panel.style.display = DisplayStyle.None;

            if (_attacker != null) _attacker.EndGrapple(pushedBack);
            _attacker = null;

            if (pushedBack) GameEvents.RaiseSubtitle("(Lo empujas con todas tus fuerzas. ¡Cae hacia atrás!)");
            else if (losePoint && _health != null) _health.LosePoint();

            if (_playerMovement != null && (_health == null || _health.Points > 0)) _playerMovement.SetInputLocked(false, false);
        }
    }
}
