using UnityEngine.UIElements;

namespace Nexo.UI.Menus
{
    /// <summary>
    /// Menú principal: NUEVA PARTIDA, OPCIONES, CRÉDITOS, SALIR (solo texto; el ítem enfocado se ilumina y se subraya).
    /// La accesibilidad vive en la pestaña de OPCIONES. Cambia la pista inferior por ítem y anima el fondo con MenuBackdrop.
    /// Teclado (W/S, flechas, Intro, Esc) y ratón vía MenuNavigator.
    /// </summary>
    public sealed class MainMenuController : IMenuScreen, IMenuNavigationHandler
    {
        private const int Play = 0;
        private const int Options = 1;
        private const int Credits = 2;
        private const int Quit = 3;

        // Posiciones de la spec a 1920x1080: lista en top 240, filas sin foco de 48 + 12 de margen
        private const float ListTop = 240f;
        private const float RowPitch = 60f;
        private const float FocusLineOffset = 64f;
        private const float FocusEndOffset = 58f;

        private static readonly string[] ItemNames =
        {
            "mm-item-play", "mm-item-options", "mm-item-credits", "mm-item-quit"
        };

        private static readonly string[] Hints =
        {
            "Inicia el protocolo en el Piso 12.",
            "Ajusta audio, video y controles.",
            "Conoce quién hizo el juego.",
            "Cierra el juego."
        };

        private readonly MenuNavigator _navigator;
        private readonly MenuBackdrop _backdrop = new MenuBackdrop();
        private readonly VisualElement[] _items = new VisualElement[ItemNames.Length];
        private MenuFlowController _flow;
        private Label _hint;
        private VisualElement _ruleFocus;
        private VisualElement _focusLine;
        private VisualElement _focusEnd;
        private int _lastIndex;

        public MainMenuController()
        {
            _navigator = new MenuNavigator(this);
        }

        public void Bind(MenuFlowController flow, VisualElement root, ScreenId screen)
        {
            _flow = flow;
            _hint = root.Q<Label>("mm-hint");
            _ruleFocus = root.Q<VisualElement>("mm-rule-focus");
            _focusLine = root.Q<VisualElement>("mm-focus-line");
            _focusEnd = root.Q<VisualElement>("mm-focus-end");

            for (int i = 0; i < ItemNames.Length; i++)
            {
                _items[i] = root.Q<VisualElement>(ItemNames[i]);
            }

            _navigator.Attach(root, _items, _lastIndex);
            _backdrop.Attach(root);
        }

        public void Unbind()
        {
            _navigator.Detach();
            _backdrop.Detach();
            _hint = null;
            _ruleFocus = null;
            _focusLine = null;
            _focusEnd = null;
        }

        public void OnItemFocused(int index)
        {
            _lastIndex = index;
            if (_hint != null) _hint.text = Hints[index];

            // La fila enfocada es la única alta; las anteriores miden siempre RowPitch (estilo calculado en ejecución)
            float top = ListTop + RowPitch * index;
            if (_ruleFocus != null) _ruleFocus.style.top = top;
            if (_focusLine != null) _focusLine.style.top = top + FocusLineOffset;
            if (_focusEnd != null) _focusEnd.style.top = top + FocusEndOffset;
        }

        public void OnItemSubmitted(int index)
        {
            switch (index)
            {
                case Play:
                    MenuEvents.RaisePlayRequested();
                    break;
                case Options:
                    _flow.OpenOptions(ScreenId.OptionsVideo, ScreenId.MainMenu);
                    break;
                case Credits:
                    _flow.ShowScreen(ScreenId.Credits);
                    break;
                case Quit:
                    MenuEvents.RaiseQuitRequested();
                    break;
            }
        }

        public void OnValueStep(int index, int direction)
        {
        }

        public void OnTabStep(int direction)
        {
        }

        public void OnCancel()
        {
        }
    }
}
