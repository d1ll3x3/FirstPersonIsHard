using BepInEx.Configuration;
using UnityEngine.InputSystem;

namespace FirstPersonFIH
{
    /// <summary>
    /// Everything the mod can be told to do, stored in FirstPersonFIH.cfg next to the dll.
    ///
    /// The comfort values are the ones worth touching: what stops a first person view from
    /// being sickening is not one setting but where the smoothing, the float and the field
    /// of view land together, and that is only found by playing. The in-game menu edits
    /// these very entries, so a value tuned mid-run is already saved.
    /// </summary>
    internal sealed class Settings
    {
        private readonly ConfigFile file;

        public ConfigEntry<Key> ToggleKey { get; }
        public ConfigEntry<Key> MenuKey { get; }

        public ConfigEntry<float> EyeOffsetX { get; }
        public ConfigEntry<float> EyeOffsetY { get; }
        public ConfigEntry<float> EyeOffsetZ { get; }

        public ConfigEntry<float> HorizontalSmoothing { get; }
        public ConfigEntry<float> VerticalSmoothing { get; }
        public ConfigEntry<float> MaxVerticalLag { get; }
        public ConfigEntry<float> TeleportSnapDistance { get; }

        public ConfigEntry<float> FieldOfView { get; }
        public ConfigEntry<float> NearClipPlane { get; }
        public ConfigEntry<bool> ExtendPitchRange { get; }
        public ConfigEntry<float> PitchLimit { get; }

        public ConfigEntry<float> ComfortVignette { get; }
        public ConfigEntry<bool> CenterDot { get; }
        public ConfigEntry<bool> DisableSpeedLines { get; }
        public ConfigEntry<bool> ShowStatus { get; }

        public ConfigEntry<bool> HidePlayerModel { get; }
        public ConfigEntry<bool> ShowHitboxLines { get; }
        public ConfigEntry<float> HitboxLineWidth { get; }
        public ConfigEntry<float> HitboxOpacity { get; }

        public Settings(ConfigFile configFile)
        {
            file = configFile;

            ToggleKey = file.Bind("Binds", "ToggleKey", Key.F1,
                "Turns first person on and off.");
            MenuKey = file.Bind("Binds", "MenuKey", Key.F2,
                "Opens the in-game settings menu.");

            EyeOffsetX = file.Bind("Camera", "EyeOffsetX", 0f,
                "Eye offset from the phone's centre of mass, in world space. Right (+) / left (-).");
            EyeOffsetY = file.Bind("Camera", "EyeOffsetY", 0.12f,
                "Eye offset from the phone's centre of mass, in world space. Up (+) / down (-). " +
                "The offset is NOT rotated by the phone: a rotating offset would orbit the centre " +
                "of mass on every flip, and that wobble is what makes first person sickening.");
            EyeOffsetZ = file.Bind("Camera", "EyeOffsetZ", 0f,
                "Eye offset from the phone's centre of mass, in world space, along the view direction.");

            FieldOfView = file.Bind("Camera", "FieldOfView", 70f,
                "Field of view while in first person. Restored on exit. Higher feels faster, " +
                "which helps some players and hurts others: try both.");
            NearClipPlane = file.Bind("Camera", "NearClipPlane", 0.02f,
                "Near clip plane while in first person. Small, because the camera sits inside the phone.");
            ExtendPitchRange = file.Bind("Camera", "ExtendPitchRange", true,
                "Widens the game's vertical look range while in first person, and puts it back on exit. " +
                "Silently ignored on a build where the axis cannot be resolved.");
            PitchLimit = file.Bind("Camera", "PitchLimit", 80f,
                "Degrees up and down the widened look range allows.");

            HorizontalSmoothing = file.Bind("Comfort", "HorizontalSmoothing", 0.05f,
                "Seconds of smoothing on horizontal movement. Short: this axis is not what makes you sick.");
            VerticalSmoothing = file.Bind("Comfort", "VerticalSmoothing", 0.12f,
                "Seconds of smoothing on vertical movement. Longer than the horizontal one on purpose: " +
                "the phone bounces up and down constantly and those jolts are the worst offender.");
            MaxVerticalLag = file.Bind("Comfort", "MaxVerticalLag", 1f,
                "Metres the camera may lag behind the phone vertically. The smoothing eats the fast " +
                "jolts, this cap stops the view from ever drifting away from the body for real.");
            TeleportSnapDistance = file.Bind("Comfort", "TeleportSnapDistance", 5f,
                "Metres of movement in a single frame that counts as a teleport. Past it the camera " +
                "cuts instead of sweeping across the level (respawns, checkpoints, beacons).");

            ComfortVignette = file.Bind("Comfort", "ComfortVignette", 0.6f,
                "Strength of the vignette that closes in while turning fast or moving fast. 0 disables it.");
            CenterDot = file.Bind("Comfort", "CenterDot", true,
                "Draws a fixed dot in the middle of the screen. A static reference point is one of the " +
                "cheapest things that reduces motion sickness.");
            DisableSpeedLines = file.Bind("Comfort", "DisableSpeedLines", true,
                "Turns the game's speed lines off while in first person, and back on when leaving.");
            ShowStatus = file.Bind("Comfort", "ShowStatus", true,
                "Small corner readout saying first person is on and which key opens the menu.");

            HidePlayerModel = file.Bind("Body", "HidePlayerModel", true,
                "Hides your own phone locally. Nobody else's view changes.");
            ShowHitboxLines = file.Bind("Body", "ShowHitboxLines", true,
                "Draws the edges of your collider. They rotate with the phone, so you can still read " +
                "how your body is spinning without the view spinning with it.");
            HitboxLineWidth = file.Bind("Body", "HitboxLineWidth", 0.008f,
                "Thickness of the hitbox lines, in metres.");
            HitboxOpacity = file.Bind("Body", "HitboxOpacity", 0.5f,
                "Opacity of the hitbox lines.");
        }

        public void Save() => file.Save();
    }
}
