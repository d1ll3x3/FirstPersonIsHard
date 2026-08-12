using System;
using EHS;
using UnityEngine;

namespace FirstPersonFIH
{
    /// <summary>
    /// The mod itself: puts the camera inside the phone without letting the phone rotate it.
    ///
    /// Cinemachine is left running exactly as it is. Its virtual camera keeps orbiting an
    /// invisible third person shot, and this rig overwrites the real camera's transform in
    /// LateUpdate, after the brain has written its own. Turning the mod off is just no
    /// longer writing, which is why nothing has to be undone in the game's camera stack.
    ///
    /// Two rules do all the anti-sickness work:
    ///
    ///  - The rotation never comes from the phone. It is the game's aim with the roll
    ///    dropped, so the horizon is level even in the middle of a flip.
    ///  - The position is anchored to the centre of mass, not to a point on the body. An
    ///    offset that rotated with the phone would orbit that centre several times a second
    ///    while flipping, and that high frequency wobble is what actually makes people sick.
    ///    The vertical axis on top of that is low-pass filtered with a hard cap on how far
    ///    it may lag, so the jolts are eaten but the view never drifts off the body.
    /// </summary>
    internal sealed class CameraRig
    {
        private readonly Settings settings;
        private readonly LookSource look = new();
        private readonly PlayerVisuals visuals = new();
        private readonly HitboxLines hitbox = new();
        private readonly PitchRange pitchRange = new();

        private Vector3 eye;
        private float velocityX;
        private float velocityY;
        private float velocityZ;
        private bool needsSnap = true;

        // Camera state to put back, and the camera it belongs to.
        private Camera held;
        private float savedFieldOfView;
        private float savedNearClip;
        private bool speedLinesTurnedOff;

        public CameraRig(Settings settings)
        {
            this.settings = settings;
        }

        /// <summary>True when the player has asked for first person.</summary>
        public bool Wanted { get; private set; }

        /// <summary>True while the rig is actually driving the camera.</summary>
        public bool Engaged => held != null;

        /// <summary>Speed of the phone in m/s, for the HUD and the comfort vignette.</summary>
        public float Speed { get; private set; }

        /// <summary>Degrees per second the view is turning, for the comfort vignette.</summary>
        public float AngularSpeed => look.AngularSpeed;

        public void Toggle()
        {
            Wanted = !Wanted;

            if (!Wanted)
            {
                Release();
            }

            FirstPersonPlugin.Logger.LogInfo($"First person {(Wanted ? "on" : "off")}.");
        }

        /// <summary>Called from LateUpdate, after Cinemachine's brain has had its say.</summary>
        public void LateTick()
        {
            try
            {
                if (!Wanted)
                {
                    return;
                }

                CameraManager cameras = GameRefs.Cameras;
                PlayerRef player = GameRefs.LocalPlayer;

                if (cameras == null || player == null)
                {
                    Release();
                    return;
                }

                // Menus, cutscenes, respawns and the customization screen all drive the
                // camera themselves. Fighting them looks broken and costs nothing to avoid.
                if (GameCompat.CameraIsBusy)
                {
                    Release();
                    return;
                }

                Camera camera = cameras.cam;
                Rigidbody body = player.rb;

                if (camera == null || body == null || !camera.isActiveAndEnabled)
                {
                    Release();
                    return;
                }

                if (!look.TryRead(cameras))
                {
                    Release();
                    return;
                }

                Take(camera, cameras, player);
                Speed = body.linearVelocity.magnitude;

                Vector3 target = Anchor(body);

                if (needsSnap || (target - eye).sqrMagnitude > Sqr(settings.TeleportSnapDistance.Value))
                {
                    // Respawn, checkpoint, beacon: cut, never sweep across the level.
                    eye = target;
                    velocityX = velocityY = velocityZ = 0f;
                    needsSnap = false;
                }
                else
                {
                    float horizontal = Mathf.Max(0f, settings.HorizontalSmoothing.Value);
                    float vertical = Mathf.Max(0f, settings.VerticalSmoothing.Value);
                    float lag = Mathf.Max(0f, settings.MaxVerticalLag.Value);

                    eye.x = Mathf.SmoothDamp(eye.x, target.x, ref velocityX, horizontal);
                    eye.z = Mathf.SmoothDamp(eye.z, target.z, ref velocityZ, horizontal);

                    float y = Mathf.SmoothDamp(eye.y, target.y, ref velocityY, vertical);
                    eye.y = Mathf.Clamp(y, target.y - lag, target.y + lag);
                }

                // Written here too, not only from the render hook: it keeps the camera right
                // for anything that reads its transform during the rest of the frame, and it
                // is the whole thing when the hook could not be attached.
                camera.transform.SetPositionAndRotation(eye, look.Rotation);

                if (settings.ShowHitboxLines.Value)
                {
                    hitbox.Tick(player, settings);
                }
                else
                {
                    hitbox.Clear();
                }
            }
            catch (Exception ex)
            {
                FirstPersonPlugin.Logger.LogError($"The camera rig threw, dropping back to third person: {ex}");
                Wanted = false;
                Release();
            }
        }

        /// <summary>
        /// Called from the render hook, once per camera, after every script has run.
        ///
        /// This is where the view is actually decided: Cinemachine's brain places the camera
        /// after LateUpdate, so whatever the rig wrote there has already been thrown away by
        /// the time this runs. The eye position is the one filtered this frame; only the aim
        /// is re-read, as late as it can be, so the mouse has no added lag.
        /// </summary>
        public void RenderTick(Camera camera)
        {
            if (held == null || camera != held)
            {
                return;
            }

            CameraManager cameras = GameRefs.Cameras;

            if (cameras == null)
            {
                return;
            }

            look.TryRead(cameras, false);
            camera.transform.SetPositionAndRotation(eye, look.Rotation);
        }

        /// <summary>Puts everything back: called on toggle off, on unload and when giving way.</summary>
        public void Release()
        {
            hitbox.Clear();
            visuals.Show();
            pitchRange.Restore();

            if (speedLinesTurnedOff)
            {
                SetSpeedLines(true);
            }

            if (held != null)
            {
                try
                {
                    held.fieldOfView = savedFieldOfView;
                    held.nearClipPlane = savedNearClip;
                }
                catch (Exception ex)
                {
                    FirstPersonPlugin.Logger.LogWarning($"Could not restore the camera: {ex.Message}");
                }

                held = null;
            }

            look.Reset();
            Speed = 0f;

            // Coming back from a menu or a respawn starts from the body, not from wherever
            // the camera was left.
            needsSnap = true;
        }

        /// <summary>
        /// The point the eye is pulled towards: the centre of mass plus an offset that is
        /// rotated by the view's yaw only. Yaw depends on where you look, never on how the
        /// phone tumbles, so the offset cannot introduce wobble of its own.
        /// </summary>
        private Vector3 Anchor(Rigidbody body)
        {
            Vector3 offset = new(settings.EyeOffsetX.Value, 0f, settings.EyeOffsetZ.Value);
            return body.worldCenterOfMass
                   + look.FlatRotation * offset
                   + Vector3.up * settings.EyeOffsetY.Value;
        }

        private void Take(Camera camera, CameraManager cameras, PlayerRef player)
        {
            if (held != camera)
            {
                Release();

                held = camera;
                savedFieldOfView = camera.fieldOfView;
                savedNearClip = camera.nearClipPlane;
                needsSnap = true;

                FirstPersonPlugin.Logger.LogInfo($"Driving camera '{camera.name}'.");
            }

            camera.fieldOfView = settings.FieldOfView.Value;
            camera.nearClipPlane = Mathf.Max(0.01f, settings.NearClipPlane.Value);

            if (settings.HidePlayerModel.Value)
            {
                visuals.Hide(player);
            }
            else
            {
                visuals.Show();
            }

            if (settings.ExtendPitchRange.Value)
            {
                pitchRange.Apply(cameras, Mathf.Clamp(settings.PitchLimit.Value, 10f, 89f));
            }
            else
            {
                pitchRange.Restore();
            }

            if (settings.DisableSpeedLines.Value && !speedLinesTurnedOff)
            {
                SetSpeedLines(false);
            }
            else if (!settings.DisableSpeedLines.Value && speedLinesTurnedOff)
            {
                SetSpeedLines(true);
            }
        }

        /// <summary>
        /// The game's speed lines are drawn for a camera that is metres behind the phone.
        /// From inside it they fill the screen, and streaks across the whole view are one of
        /// the reliable ways to make a first person mode unplayable.
        /// </summary>
        private void SetSpeedLines(bool on)
        {
            try
            {
                CameraManager cameras = GameRefs.Cameras;

                if (cameras == null || cameras.speedLinesController == null)
                {
                    speedLinesTurnedOff = false;
                    return;
                }

                cameras.speedLinesController.enabled = on;
                speedLinesTurnedOff = !on;
            }
            catch (Exception ex)
            {
                speedLinesTurnedOff = false;
                FirstPersonPlugin.Logger.LogWarning($"Could not touch the speed lines: {ex.Message}");
            }
        }

        private static float Sqr(float value) => value * value;
    }
}
