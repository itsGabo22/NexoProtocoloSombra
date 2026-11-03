using System;
using System.Collections.Generic;
using Nexo.Core;
using Nexo.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Nexo.UI
{
    /// <summary>
    /// Rueda radial de órdenes (OrderWheel.uxml). Se abre manteniendo [Q] (acción Hold), se elige sector con el
    /// puntero y se confirma al soltar; [Esc] cancela. Hasta 6 sectores con icono + texto.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DefaultExecutionOrder(100)]
    public sealed class OrderWheelController : MonoBehaviour
    {
        private const string HiddenClass = "hidden";
        private const string SelectedClass = "order-sector--selected";
        private const int MaxSectors = 6;
        private const float SectorRadius = 150f;
        private const float DeadZone = 45f;

        /// <summary>
        /// Sector configurable de la rueda. QuickQuestionIndex >= 0 reenvía la orden a Elena como pregunta rápida.
        /// </summary>
        [Serializable]
        public sealed class OrderEntry
        {
            public string OrderId = "ORDER";
            public string Label = "Orden";
            public Texture2D Icon;
            public int QuickQuestionIndex = -1;
        }

        [Header("Sectores (máx. 6)")]
        [SerializeField] private OrderEntry[] _entries =
        {
            new OrderEntry { OrderId = "SAFE_PATH", Label = "Ruta segura", QuickQuestionIndex = 0 },
            new OrderEntry { OrderId = "OPEN_DOOR", Label = "Abrir puerta", QuickQuestionIndex = 1 },
            new OrderEntry { OrderId = "FEAR", Label = "Tengo miedo", QuickQuestionIndex = 2 }
        };

        [Header("Entrada (NexoControls/Player)")]
        [SerializeField] private InputActionReference _wheelAction;
        [SerializeField] private InputActionReference _cancelAction;

        [Header("Referencias (Systems_Persistent)")]
        [SerializeField] private PlayerMovement _playerMovement;
        [SerializeField] private TerminalController _terminalController;

        private readonly List<VisualElement> _sectors = new();
        private VisualElement _root;
        private VisualElement _ring;
        private Label _center;
        private int _selected = -1;
        private bool _isOpen;
        private bool _isLocked;
        private bool _isCaptured;

        /// <summary>Se emite con el OrderId confirmado (para órdenes que no son preguntas a Elena).</summary>
        public event Action<string> OrderSelected;

        public bool IsOpen => _isOpen;

        private void OnEnable()
        {
            var document = GetComponent<UIDocument>();
            _root = document != null && document.rootVisualElement != null
                ? document.rootVisualElement.Q<VisualElement>("order-wheel-root")
                : null;

            if (_root == null)
            {
                Debug.LogWarning("[OrderWheelController] No se encontró 'order-wheel-root' en el UIDocument.", this);
                return;
            }

            _ring = _root.Q<VisualElement>("order-wheel-ring");
            _center = _root.Q<Label>("order-wheel-center");
            _root.AddToClassList(HiddenClass);

            _root.RegisterCallback<PointerMoveEvent>(HandlePointerMove);
            _root.RegisterCallback<PointerDownEvent>(HandlePointerDown);
            GameEvents.OnPlotTwistTriggered += HandlePlotTwistTriggered;
            GameEvents.OnPlayerCaught += HandlePlayerCaught;
            GameEvents.OnFloorChanged += HandleFloorChanged;

            if (_wheelAction != null)
            {
                _wheelAction.action.performed += HandleWheelPerformed;
                _wheelAction.action.canceled += HandleWheelCanceled;
                _wheelAction.action.Enable();
            }
            if (_cancelAction != null)
            {
                _cancelAction.action.performed += HandleCancelPerformed;
                _cancelAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            if (_root != null)
            {
                _root.UnregisterCallback<PointerMoveEvent>(HandlePointerMove);
                _root.UnregisterCallback<PointerDownEvent>(HandlePointerDown);
            }
            GameEvents.OnPlotTwistTriggered -= HandlePlotTwistTriggered;
            GameEvents.OnPlayerCaught -= HandlePlayerCaught;
            GameEvents.OnFloorChanged -= HandleFloorChanged;

            if (_wheelAction != null)
            {
                _wheelAction.action.performed -= HandleWheelPerformed;
                _wheelAction.action.canceled -= HandleWheelCanceled;
            }
            if (_cancelAction != null)
            {
                _cancelAction.action.performed -= HandleCancelPerformed;
            }

            Close();
        }

        // ---------- Entrada ----------

        private void HandleWheelPerformed(InputAction.CallbackContext context) => Open();

        private void HandleWheelCanceled(InputAction.CallbackContext context)
        {
            // Con la interacción Hold, soltar Q antes de tiempo también cancela: solo confirmamos si estaba abierta
            if (_isOpen) Confirm();
        }

        private void HandleCancelPerformed(InputAction.CallbackContext context) => Close();

        private void HandlePlayerCaught()
        {
            // Cerrar sin confirmar ni devolver el control: PlayerCaptureHandler congela al jugador
            _isCaptured = true;
            Close(false);
        }

        private void HandleFloorChanged(int floor)
        {
            _isCaptured = false;
        }

        private void HandlePlotTwistTriggered()
        {
            _isLocked = true;
            Close();
        }

        private void HandlePointerMove(PointerMoveEvent evt)
        {
            if (!_isOpen || _ring == null) return;

            Vector2 local = (Vector2)evt.position - _ring.worldBound.center;
            SetSelected(SectorFromOffset(local));
        }

        private void HandlePointerDown(PointerDownEvent evt)
        {
            if (_isOpen) Confirm();
        }

        // ---------- Ciclo de vida ----------

        public void Open()
        {
            if (_root == null || _isOpen || _isLocked || _isCaptured) return;
            if (_terminalController != null && _terminalController.IsOpen) return;

            _isOpen = true;
            BuildSectors();
            SetSelected(-1);

            _root.RemoveFromClassList(HiddenClass);
            if (_playerMovement != null) _playerMovement.SetInputLocked(true, true);
        }

        public void Close() => Close(true);

        /// <summary>
        /// Cierra la rueda sin confirmar. Con restoreInput = false no devuelve el control al jugador.
        /// </summary>
        private void Close(bool restoreInput)
        {
            if (!_isOpen) return;

            _isOpen = false;
            _root.AddToClassList(HiddenClass);
            if (restoreInput && _playerMovement != null) _playerMovement.SetInputLocked(false, false);
        }

        private void Confirm()
        {
            int selected = _selected;
            int count = SectorCount;
            Close();

            if (selected < 0 || selected >= count) return;

            OrderEntry entry = _entries[selected];
            if (entry.QuickQuestionIndex >= 0)
            {
                GameEvents.RaiseQuickQuestionSelected(entry.QuickQuestionIndex);
            }
            OrderSelected?.Invoke(entry.OrderId);
        }

        // ---------- Construcción y selección ----------

        private int SectorCount => _entries == null ? 0 : Mathf.Min(_entries.Length, MaxSectors);

        private void BuildSectors()
        {
            if (_ring == null) return;

            foreach (VisualElement sector in _sectors) sector.RemoveFromHierarchy();
            _sectors.Clear();

            int count = SectorCount;
            float ringSize = _ring.resolvedStyle.width > 0f ? _ring.resolvedStyle.width : 420f;
            float half = ringSize * 0.5f;

            for (int i = 0; i < count; i++)
            {
                OrderEntry entry = _entries[i];
                float angle = i * Mathf.PI * 2f / count; // 0 = arriba, sentido horario

                var sector = new VisualElement();
                sector.AddToClassList("order-sector");
                sector.style.left = half + Mathf.Sin(angle) * SectorRadius;
                sector.style.top = half - Mathf.Cos(angle) * SectorRadius;

                var icon = new VisualElement();
                icon.AddToClassList("order-sector__icon");
                if (entry.Icon != null)
                {
                    icon.style.backgroundImage = new StyleBackground(entry.Icon);
                }
                else
                {
                    icon.AddToClassList(HiddenClass);
                }

                var label = new Label(entry.Label);
                label.AddToClassList("order-sector__label");

                sector.Add(icon);
                sector.Add(label);
                _ring.Add(sector);
                _sectors.Add(sector);
            }
        }

        private int SectorFromOffset(Vector2 offset)
        {
            int count = SectorCount;
            if (count == 0 || offset.sqrMagnitude < DeadZone * DeadZone) return -1;

            // Coordenadas de panel: Y crece hacia abajo. Ángulo 0 arriba, sentido horario.
            float angle = Mathf.Atan2(offset.x, -offset.y) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;

            float slice = 360f / count;
            return Mathf.FloorToInt(((angle + slice * 0.5f) % 360f) / slice);
        }

        private void SetSelected(int index)
        {
            if (index == _selected && index >= 0) return;
            _selected = index;

            for (int i = 0; i < _sectors.Count; i++)
            {
                _sectors[i].EnableInClassList(SelectedClass, i == index);
            }

            if (_center != null)
            {
                _center.text = index >= 0 ? _entries[index].Label.ToUpperInvariant() : "ÓRDENES";
            }
        }
    }
}
