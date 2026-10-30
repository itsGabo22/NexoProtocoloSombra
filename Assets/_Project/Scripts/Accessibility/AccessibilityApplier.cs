using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nexo.Accessibility
{
    public enum UiScale
    {
        Normal,
        Large,
        ExtraLarge
    }

    public enum ColorblindMode
    {
        None,
        Deutan,
        Protan,
        Tritan
    }

    [Serializable]
    public struct AccessibilityOptions
    {
        public UiScale Scale;
        public ColorblindMode Colorblind;
        public bool HighContrast;
        public bool ReduceMotion;
        public bool Subtitles;
    }

    public static class AccessibilitySettings
    {
        public static event Action<AccessibilityOptions> Changed;
        public static AccessibilityOptions Current { get; private set; } = new AccessibilityOptions
        {
            Scale = UiScale.Normal,
            Colorblind = ColorblindMode.None,
            HighContrast = false,
            ReduceMotion = false,
            Subtitles = true
        };

        public static void Apply(AccessibilityOptions options)
        {
            Current = options;
            Changed?.Invoke(options);
        }
    }

    /// <summary>
    /// Aplica dinámicamente las clases USS de accesibilidad (WCAG 2.2 y Game Accessibility Guidelines)
    /// sobre el elemento raíz del HUD sin recrear el árbol visual.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    [DefaultExecutionOrder(110)]
    public sealed class AccessibilityApplier : MonoBehaviour
    {
        private VisualElement _root;

        private void OnEnable()
        {
            var uiDoc = GetComponent<UIDocument>();
            if (uiDoc != null && uiDoc.rootVisualElement != null)
            {
                _root = uiDoc.rootVisualElement.Q<VisualElement>("hud-root");
            }

            AccessibilitySettings.Changed += Apply;
            Apply(AccessibilitySettings.Current);
        }

        private void OnDisable()
        {
            AccessibilitySettings.Changed -= Apply;
        }

        private void Apply(AccessibilityOptions o)
        {
            if (_root == null) return;

            _root.EnableInClassList("a11y-hc", o.HighContrast);
            _root.EnableInClassList("reduce-motion", o.ReduceMotion);
            _root.EnableInClassList("ui-scale-lg", o.Scale == UiScale.Large);
            _root.EnableInClassList("ui-scale-xl", o.Scale == UiScale.ExtraLarge);
            _root.EnableInClassList("cb-deutan", o.Colorblind == ColorblindMode.Deutan);
            _root.EnableInClassList("cb-protan", o.Colorblind == ColorblindMode.Protan);
            _root.EnableInClassList("cb-tritan", o.Colorblind == ColorblindMode.Tritan);

            var subtitles = _root.Q<Label>("hud-subtitles");
            if (subtitles != null)
            {
                subtitles.EnableInClassList("hidden", !o.Subtitles);
            }
        }
    }
}
