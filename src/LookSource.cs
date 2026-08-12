using EHS;
using UnityEngine;

namespace FirstPersonFIH
{
    /// <summary>
    /// Where the first person view is pointing.
    ///
    /// It is taken from the game's own Cinemachine virtual camera, never from the phone:
    /// the phone rolls and tumbles, the virtual camera does not. Reusing the game's aim has
    /// a second reason beyond comfort — movement is camera relative (CameraManager.AlignByYaw),
    /// so a view that pointed anywhere else would desync the controls from what you see.
    ///
    /// The real camera's own transform cannot be the source: the rig overwrites it every
    /// frame, so reading it back would feed the rig its own output and freeze the view.
    /// The virtual camera is the one Cinemachine keeps updating and the mod never touches.
    ///
    /// Yaw and pitch come out of the forward vector rather than eulerAngles, which drops any
    /// residual roll by construction. That is the whole trick that keeps the horizon level.
    /// </summary>
    internal sealed class LookSource
    {
        private float yaw;
        private float pitch;
        private bool warned;

        public float Yaw => yaw;
        public float Pitch => pitch;

        /// <summary>Degrees of view rotation in the last frame, for the comfort vignette.</summary>
        public float AngularSpeed { get; private set; }

        /// <param name="measure">
        /// False when the aim is being re-read a second time in the same frame, just before
        /// rendering: the turn rate has already been measured and measuring it again over a
        /// sliver of a frame would read as standing still.
        /// </param>
        public bool TryRead(CameraManager cameras, bool measure = true)
        {
            Transform source = null;

            if (cameras.cinCam != null)
            {
                source = cameras.cinCam.transform;
            }

            if (source == null)
            {
                if (!warned)
                {
                    warned = true;
                    FirstPersonPlugin.Logger.LogWarning(
                        "No Cinemachine virtual camera on the CameraManager, first person cannot aim. " +
                        "This build is not supported.");
                }

                return false;
            }

            Vector3 forward = source.forward;
            float previousYaw = yaw;
            float previousPitch = pitch;

            // Straight up or down leaves the yaw undefined, so the last one is kept.
            if (Mathf.Abs(forward.y) < 0.9999f)
            {
                yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            }

            pitch = -Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg;

            if (measure)
            {
                float delta = Mathf.Abs(Mathf.DeltaAngle(previousYaw, yaw)) +
                              Mathf.Abs(previousPitch - pitch);
                AngularSpeed = Time.deltaTime > 0f ? delta / Time.deltaTime : 0f;
            }

            return true;
        }

        /// <summary>The rotation to hand the camera: the game's aim, with the roll thrown away.</summary>
        public Quaternion Rotation => Quaternion.Euler(pitch, yaw, 0f);

        /// <summary>Horizontal-only rotation, used to place the eye offset relative to the view.</summary>
        public Quaternion FlatRotation => Quaternion.Euler(0f, yaw, 0f);

        public void Reset()
        {
            AngularSpeed = 0f;
        }
    }
}
