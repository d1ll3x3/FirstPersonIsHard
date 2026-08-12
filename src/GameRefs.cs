using System;
using EHS;
using EHS.Bootstraps;
using UnityEngine.SceneManagement;

namespace FirstPersonFIH
{
    /// <summary>
    /// Resolves the game objects the camera rig needs.
    ///
    /// PlayerRef.LocalPlayer is deliberately never cached across frames: an IL2CPP wrapper
    /// kept over a respawn points at freed memory and crashes the process. Only the camera
    /// manager is cached, and it is dropped when the scene changes.
    /// </summary>
    internal static class GameRefs
    {
        private static CameraManager cameras;
        private static int cachedSceneHandle;

        public static PlayerRef LocalPlayer => PlayerRef.LocalPlayer;

        public static CameraManager Cameras
        {
            get
            {
                Invalidate();

                if (cameras == null)
                {
                    cameras = Resolve()?.CameraManager;
                }

                return cameras;
            }
        }

        public static void Clear()
        {
            cameras = null;
            cachedSceneHandle = 0;
        }

        private static GameReferences Resolve()
        {
            try
            {
                return PlayerRef.LocalPlayer?.GameRefs;
            }
            catch (Exception ex)
            {
                FirstPersonPlugin.Logger.LogError($"Could not reach GameReferences: {ex}");
                return null;
            }
        }

        private static void Invalidate()
        {
            int handle = SceneManager.GetActiveScene().handle;
            if (handle == cachedSceneHandle)
            {
                return;
            }

            cachedSceneHandle = handle;
            cameras = null;
        }
    }
}
