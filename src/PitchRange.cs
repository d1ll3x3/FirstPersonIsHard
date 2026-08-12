using System;
using System.Runtime.CompilerServices;
using EHS;
using Unity.Cinemachine;
using UnityEngine;

namespace FirstPersonFIH
{
    /// <summary>
    /// Widens the game's vertical look range while in first person, and puts it back.
    ///
    /// Third person only needs to look a little up and down; from inside the phone the same
    /// range feels like a stiff neck. The orbital rig's vertical axis is the one thing here
    /// that touches a Cinemachine type directly, so every call into it is isolated in a
    /// non-inlined method: on a build where CinemachineOrbitalFollow is gone the JIT throws
    /// when that method is entered, and the caller's catch turns it into a disabled feature
    /// instead of a broken mod.
    /// </summary>
    internal sealed class PitchRange
    {
        private CinemachineOrbitalFollow held;
        private Vector2 savedRange;
        private bool supported = true;

        public void Apply(CameraManager cameras, float limit)
        {
            if (!supported || held != null || cameras.cinCam == null)
            {
                return;
            }

            try
            {
                Widen(cameras, limit);
            }
            catch (Exception ex)
            {
                supported = false;
                held = null;
                FirstPersonPlugin.Logger.LogWarning(
                    $"Cannot widen the look range on this build, leaving it alone: {ex.Message}");
            }
        }

        public void Restore()
        {
            if (held == null)
            {
                return;
            }

            try
            {
                Narrow();
            }
            catch (Exception ex)
            {
                FirstPersonPlugin.Logger.LogError($"Could not restore the look range: {ex}");
            }
            finally
            {
                held = null;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Widen(CameraManager cameras, float limit)
        {
            CinemachineOrbitalFollow orbital = cameras.cinCam.GetComponent<CinemachineOrbitalFollow>();

            if (orbital == null)
            {
                supported = false;
                FirstPersonPlugin.Logger.LogInfo(
                    "The virtual camera has no orbital rig, leaving the look range as it is.");
                return;
            }

            InputAxis axis = orbital.VerticalAxis;
            savedRange = axis.Range;

            axis.Range = new Vector2(-limit, limit);
            orbital.VerticalAxis = axis;
            held = orbital;

            FirstPersonPlugin.Logger.LogInfo(
                $"Look range widened from {savedRange} to +-{limit} degrees.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Narrow()
        {
            InputAxis axis = held.VerticalAxis;

            axis.Range = savedRange;
            axis.Value = Mathf.Clamp(axis.Value, savedRange.x, savedRange.y);
            held.VerticalAxis = axis;
        }
    }
}
