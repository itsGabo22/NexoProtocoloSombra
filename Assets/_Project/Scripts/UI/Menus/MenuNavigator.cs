using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nexo.UI.Menus
{
    /// <summary>
    /// Navegación de una pantalla de menú sobre los eventos de UI Toolkit (sin Update):
    /// ↑ ↓ mueven el foco, ← → cambian valor, Enter/clic confirman, Esc cancela y Q/E cambian de pestaña.
    /// El foco real de UI Toolkit lo tiene la raíz de la pantalla; el ítem activo se marca con la clase
    /// is-focused (la que usan los USS de NexoOS), así el foco por defecto no compite con el visual.
    /// </summary>
    public sealed class MenuNavigator
    {
        public const string FocusedClass = "is-focused";

        // Evita que la misma pulsación que abrió la pantalla la confirme o la cierre
        private const float InputGuardSeconds = 0.2f;

        private readonly IMenuNavigationHandler _handler;
        private readonly List<VisualElement> _items = new List<VisualElement>();
        private VisualElement _root;
        private int _index = -1;
        private float _inputGuardUntil;

        public MenuNavigator(IMenuNavigationHandler handler)
        {
            _handler = handler;
        }

        public int Index => _index;

        /// <summary>
        /// Engancha la navegación a la raíz de la pantalla. Los ítems nulos se conservan para mantener los índices y se saltan.
        /// </summary>
        public void Attach(VisualElement root, IReadOnlyList<VisualElement> items, int initialIndex)
        {
            Detach();
            if (root == null) return;

            _root = root;
            for (int i = 0; i < items.Count; i++)
            {
                VisualElement item = items[i];
                _items.Add(item);
                if (item == null) continue;

                // El estado estático del UXML (is-focused de ejemplo) se limpia; solo la raíz recibe foco
                item.RemoveFromClassList(FocusedClass);
                item.focusable = false;
                item.RegisterCallback<PointerEnterEvent, int>(HandlePointerEnter, i);
                item.RegisterCallback<ClickEvent, int>(HandleClick, i);
            }

            _root.focusable = true;
            _root.RegisterCallback<NavigationMoveEvent>(HandleMove);
            _root.RegisterCallback<NavigationSubmitEvent>(HandleSubmit);
            _root.RegisterCallback<NavigationCancelEvent>(HandleCancel);
            _root.RegisterCallback<KeyDownEvent>(HandleKeyDown);

            _inputGuardUntil = Time.unscaledTime + InputGuardSeconds;
            SetIndex(initialIndex);
            _root.Focus();
        }

        public void Detach()
        {
            if (_root != null)
            {
                _root.UnregisterCallback<NavigationMoveEvent>(HandleMove);
                _root.UnregisterCallback<NavigationSubmitEvent>(HandleSubmit);
                _root.UnregisterCallback<NavigationCancelEvent>(HandleCancel);
                _root.UnregisterCallback<KeyDownEvent>(HandleKeyDown);
            }

            for (int i = 0; i < _items.Count; i++)
            {
                VisualElement item = _items[i];
                if (item == null) continue;

                item.UnregisterCallback<PointerEnterEvent, int>(HandlePointerEnter);
                item.UnregisterCallback<ClickEvent, int>(HandleClick);
                item.RemoveFromClassList(FocusedClass);
            }

            _items.Clear();
            _root = null;
            _index = -1;
        }

        /// <summary>
        /// Mueve el foco visual al ítem indicado y avisa al controlador.
        /// </summary>
        public void SetIndex(int index)
        {
            if (index < 0 || index >= _items.Count || _items[index] == null) return;

            if (_index >= 0 && _index < _items.Count && _items[_index] != null)
            {
                _items[_index].RemoveFromClassList(FocusedClass);
            }

            _index = index;
            _items[_index].AddToClassList(FocusedClass);
            _handler.OnItemFocused(_index);
        }

        private void Step(int direction)
        {
            int count = _items.Count;
            if (count == 0) return;

            int next = _index < 0 ? (direction > 0 ? -1 : 0) : _index;
            for (int n = 0; n < count; n++)
            {
                next = (next + direction + count) % count;
                if (_items[next] != null)
                {
                    SetIndex(next);
                    return;
                }
            }
        }

        private bool IsGuarded()
        {
            return Time.unscaledTime < _inputGuardUntil;
        }

        private void HandleMove(NavigationMoveEvent evt)
        {
            switch (evt.direction)
            {
                case NavigationMoveEvent.Direction.Up:
                    Step(-1);
                    break;
                case NavigationMoveEvent.Direction.Down:
                    Step(1);
                    break;
                case NavigationMoveEvent.Direction.Left:
                    if (_index >= 0) _handler.OnValueStep(_index, -1);
                    break;
                case NavigationMoveEvent.Direction.Right:
                    if (_index >= 0) _handler.OnValueStep(_index, 1);
                    break;
                default:
                    return;
            }

            evt.StopPropagation();
        }

        private void HandleSubmit(NavigationSubmitEvent evt)
        {
            evt.StopPropagation();
            if (IsGuarded() || _index < 0) return;
            _handler.OnItemSubmitted(_index);
        }

        private void HandleCancel(NavigationCancelEvent evt)
        {
            evt.StopPropagation();
            if (IsGuarded()) return;
            _handler.OnCancel();
        }

        private void HandleKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Q)
            {
                _handler.OnTabStep(-1);
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.E)
            {
                _handler.OnTabStep(1);
                evt.StopPropagation();
            }
        }

        private void HandlePointerEnter(PointerEnterEvent evt, int index)
        {
            SetIndex(index);
        }

        private void HandleClick(ClickEvent evt, int index)
        {
            SetIndex(index);
            _handler.OnItemSubmitted(index);
        }
    }
}
