using System;
using FirstPersonFIH.Menu;
using UnityEngine;

namespace FirstPersonFIH
{
    /// <summary>
    /// Resident component. Needs the IntPtr constructor for IL2CPP interop.
    ///
    /// The split between Update and LateUpdate is the important part: hotkeys and the menu
    /// run in Update, and the camera is written in LateUpdate, after Cinemachine's brain has
    /// placed it. Writing it any earlier would just be overwritten in the same frame.
    /// </summary>
    public class FirstPersonBehaviour : MonoBehaviour
    {
        public FirstPersonBehaviour(IntPtr ptr) : base(ptr) { }

        private CameraRig rig;
        private FirstPersonMenu menu;
        private ComfortOverlay overlay;
        private RenderHook renderHook;

        private void Awake()
        {
            Settings settings = FirstPersonPlugin.Settings;

            rig = new CameraRig(settings);
            menu = new FirstPersonMenu(settings);
            overlay = new ComfortOverlay(settings);

            renderHook = new RenderHook();
            renderHook.TryAttach(rig.RenderTick);

            FirstPersonPlugin.Rig = rig;
            FirstPersonPlugin.MenuInstance = menu;
            FirstPersonPlugin.Hook = renderHook;
        }

        private void OnDestroy()
        {
            renderHook?.Detach();
        }

        private void Update()
        {
            try
            {
                Settings settings = FirstPersonPlugin.Settings;

                if (InputReader.WasPressedThisFrame(settings.MenuKey.Value))
                {
                    menu.Toggle();
                }

                menu.Tick();

                // Toggling first person from inside the menu would fight whatever row is
                // being edited, and the menu key is the way out anyway.
                if (!menu.IsOpen && InputReader.WasPressedThisFrame(settings.ToggleKey.Value))
                {
                    rig.Toggle();
                }

                overlay.Tick(rig);
            }
            catch (Exception ex)
            {
                FirstPersonPlugin.Logger.LogError($"Update failed: {ex}");
            }
        }

        private void LateUpdate()
        {
            rig.LateTick();
        }

        private void OnGUI()
        {
            try
            {
                overlay.Draw(rig);
                menu.Draw();
            }
            catch (Exception ex)
            {
                FirstPersonPlugin.Logger.LogError($"Drawing failed: {ex}");
            }
        }
    }
}
