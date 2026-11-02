using UnityEngine;
using UnityEngine.UIElements;

namespace Nexo.UI.Menus
{
    /// <summary>
    /// Secuencia de arranque: Despertar (progreso 0→100 % en ~3 s y apertura de párpados) → Escaneo biométrico (~2.5 s) → Menú.
    /// Cualquier tecla, clic o botón de navegación salta directamente al menú. La calibración óptica queda fuera de alcance.
    /// Temporizadores con VisualElement.schedule (sin Update).
    /// </summary>
    public sealed class BootSequenceController : IMenuScreen
    {
        private const long TickMs = 50;
        private const float ProgressDurationMs = 3000f;
        private const long LidOpenMs = 1200;   // misma duración que la transición de .boot-lid en nexo-menu-screens.uss
        private const long ScanDurationMs = 2500;
        private const string LidOpenClass = "boot-lid--open";

        private MenuFlowController _flow;
        private VisualElement _root;
        private VisualElement _progressFill;
        private VisualElement _lidTop;
        private VisualElement _lidBottom;
        private Label _progressValue;
        private IVisualElementScheduledItem _progressTick;
        private IVisualElementScheduledItem _nextStep;
        private long _elapsedMs;

        public void Bind(MenuFlowController flow, VisualElement root, ScreenId screen)
        {
            _flow = flow;
            _root = root;

            _root.focusable = true;
            _root.RegisterCallback<KeyDownEvent>(HandleSkipKey);
            _root.RegisterCallback<PointerDownEvent>(HandleSkipPointer);
            _root.RegisterCallback<NavigationSubmitEvent>(HandleSkipSubmit);
            _root.RegisterCallback<NavigationCancelEvent>(HandleSkipCancel);
            _root.Focus();

            if (screen == ScreenId.BootAwakening)
            {
                _progressFill = _root.Q<VisualElement>("boot-progress-fill");
                _progressValue = _root.Q<Label>("boot-progress-value");
                _lidTop = _root.Q<VisualElement>("boot-lid-top");
                _lidBottom = _root.Q<VisualElement>("boot-lid-bottom");

                _elapsedMs = 0;
                SetProgress(0f);
                _progressTick = _root.schedule.Execute(TickProgress).Every(TickMs);
            }
            else
            {
                _nextStep = _root.schedule.Execute(GoToMainMenu).StartingIn(ScanDurationMs);
            }
        }

        public void Unbind()
        {
            _progressTick?.Pause();
            _nextStep?.Pause();
            _progressTick = null;
            _nextStep = null;

            if (_root != null)
            {
                _root.UnregisterCallback<KeyDownEvent>(HandleSkipKey);
                _root.UnregisterCallback<PointerDownEvent>(HandleSkipPointer);
                _root.UnregisterCallback<NavigationSubmitEvent>(HandleSkipSubmit);
                _root.UnregisterCallback<NavigationCancelEvent>(HandleSkipCancel);
            }

            _root = null;
            _progressFill = null;
            _progressValue = null;
            _lidTop = null;
            _lidBottom = null;
        }

        private void TickProgress()
        {
            _elapsedMs += TickMs;
            float t = Mathf.Clamp01(_elapsedMs / ProgressDurationMs);
            SetProgress(t);
            if (t < 1f) return;

            _progressTick?.Pause();
            _lidTop?.AddToClassList(LidOpenClass);
            _lidBottom?.AddToClassList(LidOpenClass);
            if (_root != null) _nextStep = _root.schedule.Execute(GoToBiometricScan).StartingIn(LidOpenMs);
        }

        private void SetProgress(float t)
        {
            if (_progressFill != null) _progressFill.style.width = Length.Percent(t * 100f);
            if (_progressValue != null) _progressValue.text = $"{Mathf.RoundToInt(t * 100f)}%";
        }

        private void GoToBiometricScan()
        {
            if (_root != null) _flow.ShowScreen(ScreenId.BootBiometricScan);
        }

        private void GoToMainMenu()
        {
            if (_root != null) _flow.ShowScreen(ScreenId.MainMenu);
        }

        private void HandleSkipKey(KeyDownEvent evt)
        {
            GoToMainMenu();
        }

        private void HandleSkipPointer(PointerDownEvent evt)
        {
            GoToMainMenu();
        }

        private void HandleSkipSubmit(NavigationSubmitEvent evt)
        {
            GoToMainMenu();
        }

        private void HandleSkipCancel(NavigationCancelEvent evt)
        {
            GoToMainMenu();
        }
    }
}
