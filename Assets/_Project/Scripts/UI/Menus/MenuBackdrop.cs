using UnityEngine;
using UnityEngine.UIElements;

namespace Nexo.UI.Menus
{
    /// <summary>
    /// Fondo del menú de inicio (estilo monocromo salvia/carbón): degradado oscuro procedural, viñeta, franja salvia del
    /// título y grano de película con una leve deriva. Las texturas se generan una vez en memoria (sin assets nuevos) y la
    /// animación usa VisualElement.schedule (sin Update). Con «reducir movimiento» el grano y la deriva quedan quietos.
    /// Todas las capas tienen pickingMode Ignore: no capturan el ratón.
    /// </summary>
    public sealed class MenuBackdrop
    {
        private const long GrainTickMs = 70;
        private const long DriftTickMs = 50;
        private const float DriftPeriodSeconds = 38f;
        private const float DriftPixels = 14f;

        private static readonly Color Charcoal = new Color32(0x2A, 0x2A, 0x2A, 0xFF);
        private static readonly Color NearBlack = new Color32(0x11, 0x11, 0x11, 0xFF);
        private static readonly Color Sage = new Color32(0xA3, 0xB8, 0xAC, 0xFF);

        private static Texture2D _gradient;
        private static Texture2D _vignette;
        private static Texture2D _grain;
        private static Texture2D _strip;

        private VisualElement _root;
        private VisualElement _bg;
        private VisualElement _grainLayer;
        private IVisualElementScheduledItem _grainTick;
        private IVisualElementScheduledItem _driftTick;
        private float _time;
        private uint _seed = 2166136261u;

        /// <summary>Aplica las texturas a las capas aw-bg, aw-vignette, aw-grain y aw-strip (las que existan) y arranca la animación.</summary>
        public void Attach(VisualElement root)
        {
            Detach();
            _root = root;
            if (root == null) return;

            _bg = Layer(root, "aw-bg", Gradient());
            Layer(root, "aw-vignette", Vignette());
            _grainLayer = Layer(root, "aw-grain", Grain());
            Layer(root, "aw-strip", Strip());

            _time = 0f;
            if (_grainLayer != null) _grainTick = _grainLayer.schedule.Execute(TickGrain).Every(GrainTickMs);
            if (_bg != null) _driftTick = _bg.schedule.Execute(TickDrift).Every(DriftTickMs);
        }

        public void Detach()
        {
            _grainTick?.Pause();
            _driftTick?.Pause();
            _grainTick = null;
            _driftTick = null;
            _root = null;
            _bg = null;
            _grainLayer = null;
        }

        private bool ReduceMotion
        {
            get
            {
                for (VisualElement e = _root; e != null; e = e.hierarchy.parent)
                {
                    if (e.ClassListContains("reduce-motion")) return true;
                }
                return false;
            }
        }

        private void TickGrain()
        {
            if (_grainLayer == null || ReduceMotion) return;
            // Desplazamiento pseudoaleatorio (xorshift) de ±40 px: el grano «vibra» como película
            _seed ^= _seed << 13;
            _seed ^= _seed >> 17;
            _seed ^= _seed << 5;
            float x = (_seed & 0xFF) / 255f * 80f - 40f;
            float y = ((_seed >> 8) & 0xFF) / 255f * 80f - 40f;
            _grainLayer.style.translate = new Translate(x, y);
        }

        private void TickDrift()
        {
            if (_bg == null || ReduceMotion) return;
            _time += DriftTickMs / 1000f;
            float a = _time / DriftPeriodSeconds * Mathf.PI * 2f;
            _bg.style.translate = new Translate(Mathf.Sin(a) * DriftPixels, Mathf.Cos(a * 0.7f) * DriftPixels * 0.6f);
        }

        private static VisualElement Layer(VisualElement root, string name, Texture2D texture)
        {
            VisualElement layer = root.Q<VisualElement>(name);
            if (layer == null) return null;
            layer.pickingMode = PickingMode.Ignore;
            layer.style.backgroundImage = new StyleBackground(texture);
            return layer;
        }

        // ---------- Texturas procedurales (una sola vez) ----------

        private static Texture2D NewTexture(int w, int h, TextureWrapMode wrap)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.DontSave,
                wrapMode = wrap,
                filterMode = FilterMode.Bilinear
            };
        }

        /// <summary>Degradado diagonal carbón (arriba-izquierda) → casi negro (abajo-derecha) con un leve brillo frío.</summary>
        private static Texture2D Gradient()
        {
            if (_gradient != null) return _gradient;
            const int w = 256, h = 144;
            _gradient = NewTexture(w, h, TextureWrapMode.Clamp);
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // Textura con y hacia arriba: arriba-izquierda = (0, h-1)
                    float u = x / (float)(w - 1);
                    float v = 1f - y / (float)(h - 1);
                    float t = Mathf.Clamp01((u * 0.65f + v * 0.35f));
                    Color c = Color.Lerp(Charcoal, NearBlack, Mathf.SmoothStep(0f, 1f, t));
                    // Halo salvia muy tenue detrás del bloque del título
                    float dx = u - 0.18f, dy = v - 0.22f;
                    float halo = Mathf.Exp(-(dx * dx * 6f + dy * dy * 10f)) * 0.05f;
                    pixels[y * w + x] = Color.Lerp(c, Sage, halo);
                }
            }
            _gradient.SetPixels(pixels);
            _gradient.Apply(false, true);
            return _gradient;
        }

        /// <summary>Viñeta radial: transparente en el centro, negro en los bordes.</summary>
        private static Texture2D Vignette()
        {
            if (_vignette != null) return _vignette;
            const int w = 256, h = 144;
            _vignette = NewTexture(w, h, TextureWrapMode.Clamp);
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x / (float)(w - 1) - 0.5f) * 2f;
                    float dy = (y / (float)(h - 1) - 0.5f) * 2f;
                    float d = Mathf.Sqrt(dx * dx * 0.8f + dy * dy * 1.1f);
                    float a = Mathf.SmoothStep(0.45f, 1.35f, d) * 0.85f;
                    pixels[y * w + x] = new Color(0f, 0f, 0f, a);
                }
            }
            _vignette.SetPixels(pixels);
            _vignette.Apply(false, true);
            return _vignette;
        }

        /// <summary>Grano: ruido blanco/negro con alfa bajo (la capa es mayor que la pantalla y se desplaza).</summary>
        private static Texture2D Grain()
        {
            if (_grain != null) return _grain;
            const int size = 512;
            _grain = NewTexture(size, size, TextureWrapMode.Repeat);
            _grain.filterMode = FilterMode.Point;
            var pixels = new Color[size * size];
            uint s = 0x9E3779B9u;
            for (int i = 0; i < pixels.Length; i++)
            {
                s ^= s << 13;
                s ^= s >> 17;
                s ^= s << 5;
                float n = (s & 0xFFFF) / 65535f;
                pixels[i] = n > 0.5f ? new Color(1f, 1f, 1f, (n - 0.5f) * 0.10f) : new Color(0f, 0f, 0f, (0.5f - n) * 0.16f);
            }
            _grain.SetPixels(pixels);
            _grain.Apply(false, true);
            return _grain;
        }

        /// <summary>Franja salvia del título: opaca a la izquierda y se desvanece a la derecha.</summary>
        private static Texture2D Strip()
        {
            if (_strip != null) return _strip;
            const int w = 256;
            _strip = NewTexture(w, 1, TextureWrapMode.Clamp);
            var pixels = new Color[w];
            for (int x = 0; x < w; x++)
            {
                float a = 1f - Mathf.SmoothStep(0f, 1f, x / (float)(w - 1));
                pixels[x] = new Color(Sage.r, Sage.g, Sage.b, a * 0.9f);
            }
            _strip.SetPixels(pixels);
            _strip.Apply(false, true);
            return _strip;
        }
    }
}
