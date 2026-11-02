using Nexo.Core;
using Nexo.Interaction;
using UnityEngine;

namespace Nexo.Narrative
{
    /// <summary>
    /// BioScanner maestro del vestíbulo (Piso 1). Al pulsar [E] emite NarrativeEvents.BioScannerHandPlaced una sola vez
    /// y deja de ofrecer interacción. Opcionalmente tiñe sus luces y renderers de cian a rojo cuando el giro se dispara.
    /// Necesita un Collider en la capa que detecta PlayerInteractor (raycast de 2.5 m desde la cámara).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BioScanner : MonoBehaviour, IInteractable
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Header("Interacción")]
        [SerializeField] private string _promptText = "Poner la mano en el escáner";

        [Header("Indicadores opcionales (cian → rojo con el giro)")]
        [SerializeField] private Light[] _indicatorLights;
        [SerializeField] private Renderer[] _indicatorRenderers;
        [SerializeField] private Color _idleColor = new Color(0.13f, 0.83f, 0.93f);
        [SerializeField] private Color _twistColor = new Color(1f, 0.23f, 0.23f);

        private MaterialPropertyBlock _block;
        private bool _used;

        /// <summary>Vacío tras el uso: PlayerInteractor oculta el prompt y Interact() ya no hace nada.</summary>
        public string Prompt => _used ? string.Empty : $"[E] {_promptText}";

        public bool IsUsed => _used;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            ApplyColor(_idleColor);
        }

        private void OnEnable()
        {
            GameEvents.OnPlotTwistTriggered += HandlePlotTwistTriggered;
        }

        private void OnDisable()
        {
            GameEvents.OnPlotTwistTriggered -= HandlePlotTwistTriggered;
        }

        public void Interact()
        {
            if (_used) return;

            _used = true;
            NarrativeEvents.RaiseBioScannerHandPlaced();
        }

        private void HandlePlotTwistTriggered()
        {
            ApplyColor(_twistColor);
        }

        private void ApplyColor(Color color)
        {
            if (_indicatorLights != null)
            {
                foreach (Light indicator in _indicatorLights)
                {
                    if (indicator != null) indicator.color = color;
                }
            }

            if (_indicatorRenderers == null) return;
            foreach (Renderer rend in _indicatorRenderers)
            {
                if (rend == null) continue;

                // MaterialPropertyBlock: tiñe sin instanciar materiales. Standard usa _Color; URP Lit usa _BaseColor.
                // La emisión del Standard solo se ve si el material ya tiene activa la palabra clave _EMISSION.
                Material material = rend.sharedMaterial;
                if (material == null) continue;

                rend.GetPropertyBlock(_block);
                if (material.HasProperty(ColorId)) _block.SetColor(ColorId, color);
                if (material.HasProperty(BaseColorId)) _block.SetColor(BaseColorId, color);
                if (material.HasProperty(EmissionColorId)) _block.SetColor(EmissionColorId, color * 2f);
                rend.SetPropertyBlock(_block);
            }
        }
    }
}
