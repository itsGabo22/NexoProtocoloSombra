using UnityEngine;
using UnityEngine.UIElements;

namespace Nexo.UI.Menus
{
    /// <summary>
    /// Pantalla de inicio. Si el layout de BootAwakening tiene «splash-prompt» (layout actual): «PRESIONA [ENTER] PARA
    /// CONTINUAR» con fondo animado (MenuBackdrop) y el aviso parpadeando; espera a Intro, cualquier tecla o clic → menú.
    /// Con el layout antiguo: Despertar (progreso 0→100 % en ~3 s y párpados) → Escaneo biométrico (~2.5 s) → Menú.
    /// Temporizadores con VisualElement.schedule (sin Update).
    /// </summary>
    public sealed class BootSequenceController : IMenuScreen
    {
        private const long TickMs = 50;
        private const float ProgressDurationMs = 3000f;
        private const long LidOpenMs = 1200;   // misma duración que la transición de .boot-lid en nexo-menu-screens.uss
        private const long ScanDurationMs = 2500;
        private const string LidOpenClass = "boot-lid--open";
        private const long PromptBlinkMs = 900;
        private const string PromptDimClass = "aw-prompt--dim";

        private MenuFlowController _flow;
        private VisualElement _root;
        private VisualElement _progressFill;
        private VisualElement _lidTop;
        private VisualElement _lidBottom;
        private Label _progressValue;
        private IVisualElementScheduledItem _progressTick;
        private IVisualElementScheduledItem _nextStep;
        private long _elapsedMs;
        private readonly MenuBackdrop _backdrop = new MenuBackdrop();
        private Label _prompt;
        private IVisualElementScheduledItem _blink;

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

            _prompt = screen == ScreenId.BootAwakening ? _root.Q<Label>("splash-prompt") : null;
            if (_prompt != null)
            {
                // Modo splash: sin avance automático; el aviso parpadea (quieto con «reducir movimiento»)
                _backdrop.Attach(_root);
                _blink = _prompt.schedule.Execute(BlinkPrompt).Every(PromptBlinkMs);
            }
            else if (screen == ScreenId.BootAwakening)
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
            _blink?.Pause();
            _progressTick = null;
            _nextStep = null;
            _blink = null;
            _prompt = null;
            _backdrop.Detach();

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

        private void BlinkPrompt()
        {
            if (_prompt == null || _root == null) return;
            bool reduce = false;
            for (VisualElement e = _root; e != null; e = e.hierarchy.parent) reduce |= e.ClassListContains("reduce-motion");
            _prompt.EnableInClassList(PromptDimClass, !reduce && !_prompt.ClassListContains(PromptDimClass));
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
