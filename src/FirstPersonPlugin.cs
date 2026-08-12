using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using FirstPersonFIH.Menu;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FirstPersonFIH
{
    [BepInPlugin(Guid, "First Person is Hard", "1.0.0")]
    public class FirstPersonPlugin : BasePlugin
    {
        public const string Guid = "com.dani.firstpersonfih";

        internal static ManualLogSource Logger { get; private set; }
        internal static Settings Settings { get; private set; }

        /// <summary>Set by the behaviour once it exists, so unloading can put the game back.</summary>
        internal static CameraRig Rig { get; set; }

        internal static FirstPersonMenu MenuInstance { get; set; }

        internal static RenderHook Hook { get; set; }

        public override void Load()
        {
            Logger = Log;

            // The config file lives next to the dll instead of BepInEx\config, so the whole
            // mod is one folder you can copy between installs and keep your settings.
            string folder = Path.GetDirectoryName(typeof(FirstPersonPlugin).Assembly.Location) ?? Paths.ConfigPath;
            string path = Path.Combine(folder, "FirstPersonFIH.cfg");

            Settings = new Settings(new ConfigFile(path, true));

            ClassInjector.RegisterTypeInIl2Cpp<FirstPersonBehaviour>();

            var host = new GameObject("FirstPersonFIH");
            Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<FirstPersonBehaviour>();

            Logger.LogInfo($"Loaded. {Settings.ToggleKey.Value} = first person, {Settings.MenuKey.Value} = menu.");
            Logger.LogInfo($"Settings file: {path}");
        }

        public override bool Unload()
        {
            // Last chance to hand the camera, the player's renderers and the game's own
            // input back before the mod goes away with first person still on.
            Hook?.Detach();
            MenuInstance?.Close();
            Rig?.Release();
            Settings?.Save();
            GameRefs.Clear();
            return true;
        }
    }
}
