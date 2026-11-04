using UnityEngine;

namespace Nexo.Interaction
{
    /// <summary>
    /// Resalta un objeto interactuable mientras el jugador lo mira (PlayerInteractor llama a SetHighlighted): sube la
    /// emisión de sus renderers con un MaterialPropertyBlock (sin duplicar materiales ni tocar los assets).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractableHighlight : MonoBehaviour
    {
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private Color _glow = new Color(0.55f, 0.5f, 0.38f, 1f);
        [SerializeField] private Renderer[] _renderers;

        private MaterialPropertyBlock _block;
        private bool _on;

        public bool IsHighlighted => _on;

        private void Awake()
        {
            if (_renderers == null || _renderers.Length == 0) _renderers = GetComponentsInChildren<Renderer>();
            _block = new MaterialPropertyBlock();
            foreach (Renderer r in _renderers)
            {
                if (r == null) continue;
                foreach (Material m in r.sharedMaterials) if (m != null) m.EnableKeyword("_EMISSION");
            }
        }

        public void SetHighlighted(bool on)
        {
            if (_on == on || _renderers == null) return;
            _on = on;
            foreach (Renderer r in _renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(EmissionId, on ? _glow : Color.black);
                r.SetPropertyBlock(_block);
            }
        }

        private void OnDisable() => SetHighlighted(false);
    }
}
