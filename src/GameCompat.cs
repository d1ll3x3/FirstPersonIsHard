using System;
using System.Reflection;
using EHS;

namespace FirstPersonFIH
{
    /// <summary>
    /// Access to game members that do not exist in every build.
    ///
    /// The mod is compiled against one install's interop assemblies but has to run on
    /// several. A member that is missing from the running build throws
    /// MissingMethodException the moment the calling method is JITed, so the reference
    /// cannot be a direct call: it has to be resolved by reflection and cached, with a
    /// default when it is not there.
    ///
    /// GameManager.IsCustomizationScreenShown, for instance, exists in the 1.8 playtest but
    /// not in the Steam demo. All of the state flags go through here so the next added or
    /// removed one costs nothing.
    /// </summary>
    internal static class GameCompat
    {
        private static readonly Func<bool> GameEnded = StaticFlag("IsGameEnded");
        private static readonly Func<bool> PauseMenuShown = StaticFlag("IsPauseMenuShown");
        private static readonly Func<bool> GameFrozen = StaticFlag("IsGameFrozen");
        private static readonly Func<bool> BeingRespawned = StaticFlag("IsBeingRespawned");
        private static readonly Func<bool> BeingSummoned = StaticFlag("IsBeingSummoned");
        private static readonly Func<bool> CustomizationScreenShown = StaticFlag("IsCustomizationScreenShown");

        /// <summary>
        /// True while the game is in a state where the mod must give the camera back:
        /// menus, cutscenes and transitions all drive it themselves.
        /// </summary>
        public static bool CameraIsBusy =>
            Read(GameEnded) ||
            Read(PauseMenuShown) ||
            Read(GameFrozen) ||
            Read(BeingRespawned) ||
            Read(BeingSummoned) ||
            Read(CustomizationScreenShown);

        private static bool Read(Func<bool> flag)
        {
            if (flag == null)
            {
                return false;
            }

            try
            {
                return flag();
            }
            catch (Exception ex)
            {
                FirstPersonPlugin.Logger.LogError($"Reading a game state flag failed: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Binds a static bool property of GameManager, or null when this build does not
        /// have it. Bound once into a delegate so reading it later costs a plain call.
        /// </summary>
        private static Func<bool> StaticFlag(string name)
        {
            try
            {
                MethodInfo getter = typeof(GameManager)
                    .GetProperty(name, BindingFlags.Public | BindingFlags.Static)
                    ?.GetGetMethod();

                if (getter == null)
                {
                    FirstPersonPlugin.Logger.LogInfo($"GameManager.{name} is not in this build, treating it as false.");
                    return null;
                }

                return (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), getter);
            }
            catch (Exception ex)
            {
                FirstPersonPlugin.Logger.LogWarning($"Could not bind GameManager.{name}: {ex.Message}");
                return null;
            }
        }
    }
}
