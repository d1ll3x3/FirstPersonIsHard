using UnityEngine;

namespace FirstPersonFIH
{
    /// <summary>
    /// The two screen-space comfort aids.
    ///
    /// A vignette that closes in while you turn or move fast: cutting peripheral vision
    /// during the motion that causes it is the one trick from VR that transfers directly to
    /// a flat screen. And a dot in the middle, because a fixed reference point on a moving
    /// image is the cheapest thing there is against motion sickness.
    ///
    /// Both are drawn from the mod rather than through the game's post processing volume,
    /// so nothing has to be put back when the mod is turned off.
    /// </summary>
    internal sealed class ComfortOverlay
    {
        private const float TurnReference = 220f;   // deg/s that counts as "turning fast"
        private const float SpeedReference = 35f;   // m/s that counts as "moving fast"

        private readonly Settings settings;

        private Texture2D vignette;
        private GUIStyle statusStyle;
        private float intensity;

        public ComfortOverlay(Settings settings)
        {
            this.settings = settings;
        }

        /// <summary>Called from Update: the strength follows the motion, not the frame rate.</summary>
        public void Tick(CameraRig rig)
        {
            if (!rig.Engaged)
            {
                intensity = 0f;
                return;
            }

            float target = Mathf.Clamp01(rig.AngularSpeed / TurnReference) * 0.7f +
                           Mathf.Clamp01(rig.Speed / SpeedReference) * 0.6f;

            target = Mathf.Clamp01(target) * Mathf.Clamp01(settings.ComfortVignette.Value);

            // Framerate independent approach, so the vignette does not flicker on a hitch.
            intensity = Mathf.Lerp(intensity, target, 1f - Mathf.Exp(-6f * Time.deltaTime));
        }

        public void Draw(CameraRig rig)
        {
            if (!rig.Engaged)
            {
                return;
            }

            if (intensity > 0.002f)
            {
                EnsureVignette();

                Color previous = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, intensity);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), vignette);
                GUI.color = previous;
            }

            if (settings.CenterDot.Value)
            {
                float size = Mathf.Max(2f, Screen.height / 320f);
                Gui.Fill(new Rect((Screen.width - size) * 0.5f, (Screen.height - size) * 0.5f, size, size),
                    new Color(1f, 1f, 1f, 0.55f));
            }

            if (settings.ShowStatus.Value)
            {
                statusStyle ??= Gui.Style(13, FontStyle.Bold, Gui.Accent, TextAnchor.UpperLeft);

                GUI.Label(new Rect(16f, 16f, 420f, 20f),
                    $"FIRST PERSON   ·   {settings.ToggleKey.Value} = off   ·   {settings.MenuKey.Value} = menu",
                    statusStyle);
            }
        }

        /// <summary>
        /// A white texture whose alpha grows towards the edges. The tint is applied at draw
        /// time, so the same texture serves every intensity.
        /// </summary>
        private void EnsureVignette()
        {
            if (vignette != null)
            {
                return;
            }

            const int size = 128;
            vignette = new Texture2D(size, size, TextureFormat.ARGB32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color[size * size];
            float half = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy) / Mathf.Sqrt(2f);

                    // Clear well past the middle, then ramping up to solid at the corners.
                    float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, distance));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            vignette.SetPixels(pixels);
            vignette.Apply(false, false);
        }
    }
}
