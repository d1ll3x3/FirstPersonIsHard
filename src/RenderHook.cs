using System;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Rendering;

namespace FirstPersonFIH
{
    /// <summary>
    /// Runs a callback right before a camera renders.
    ///
    /// LateUpdate is not late enough. Cinemachine 3's brain runs at a very high execution
    /// order, on purpose, so that gameplay scripts have had their say before it places the
    /// camera — which means it also runs after this mod's LateUpdate and overwrites it, and
    /// the view stays in third person. The render pipeline's beginCameraRendering fires
    /// after every script and before culling, so it is the last honest place to move a
    /// camera, and URP is what this game renders with.
    ///
    /// If the subscription cannot be made on some build, the rig keeps writing from
    /// LateUpdate: worse, but not broken, and the log says so.
    /// </summary>
    internal sealed class RenderHook
    {
        private Il2CppSystem.Action<ScriptableRenderContext, Camera> handle;
        private Action<Camera> callback;

        public bool Attached => handle != null;

        public bool TryAttach(Action<Camera> onCameraRendering)
        {
            if (handle != null)
            {
                return true;
            }

            callback = onCameraRendering;

            try
            {
                handle = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<ScriptableRenderContext, Camera>>(
                    (Action<ScriptableRenderContext, Camera>)Forward);

                RenderPipelineManager.add_beginCameraRendering(handle);

                FirstPersonPlugin.Logger.LogInfo("Camera written from beginCameraRendering.");
                return true;
            }
            catch (Exception ex)
            {
                handle = null;
                FirstPersonPlugin.Logger.LogWarning(
                    $"Could not hook the render pipeline, falling back to LateUpdate: {ex.Message}");
                return false;
            }
        }

        public void Detach()
        {
            if (handle == null)
            {
                return;
            }

            try
            {
                RenderPipelineManager.remove_beginCameraRendering(handle);
            }
            catch (Exception ex)
            {
                FirstPersonPlugin.Logger.LogWarning($"Could not unhook the render pipeline: {ex.Message}");
            }

            handle = null;
            callback = null;
        }

        private void Forward(ScriptableRenderContext context, Camera camera)
        {
            try
            {
                callback?.Invoke(camera);
            }
            catch (Exception ex)
            {
                // This runs inside the render loop: an exception escaping here would be
                // thrown once per camera per frame.
                FirstPersonPlugin.Logger.LogError($"Render hook failed, detaching: {ex}");
                Detach();
            }
        }
    }
}
