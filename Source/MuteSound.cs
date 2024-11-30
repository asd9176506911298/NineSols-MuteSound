using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using NineSolsAPI;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace MuteSound;

[BepInDependency(NineSolsAPICore.PluginGUID)]
[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class MuteSound : BaseUnityPlugin {
    public static MuteSound Instance { get; private set; }
    public ConfigEntry<bool> isMute = null!;
    public ConfigEntry<bool> isToast = null!;
    public ConfigEntry<bool> isToastMute = null!;
    public ConfigEntry<string> muteSoundNames = null!; // Store multiple sound names as a single, comma-separated string

    public HashSet<string> muteSoundSet = new(); // Store sound names as a HashSet for fast lookups
    private Harmony harmony = null!;

    private void Awake() {
        Instance = this;
        Log.Init(Logger);
        RCGLifeCycle.DontDestroyForever(gameObject);

        harmony = Harmony.CreateAndPatchAll(typeof(MuteSound).Assembly);

        // Configuration options
        isMute = Config.Bind("Enable", "Mute Sound", false, "Mute specific sounds by name");
        isToast = Config.Bind("", "Toast Play SoundName", false, new ConfigDescription("Show toast messages for sound playback", null,
                        new ConfigurationManagerAttributes { Order = 2 }));
        isToastMute = Config.Bind("", "Toast Mute SoundName", false, new ConfigDescription("Show mute sound playback", null,
                        new ConfigurationManagerAttributes { Order = 1 }));

        muteSoundNames = Config.Bind("Filter",
            "MuteSoundNames",
            "",
            "Comma-separated list of sound names to mute (e.g., sound1,sound2,sound3)");

        // Initialize the HashSet with the configured sound names
        UpdateMuteSoundSet();

        muteSoundNames.SettingChanged += (s, e) => OnMuteSoundNamesChanged();

        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
    }

    private void UpdateMuteSoundSet() {
        // Split the comma-separated string and add sound names to the HashSet
        muteSoundSet = muteSoundNames.Value
            .Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries)
            .Select(name => name.Trim())
            .ToHashSet();
    }

    // Call this method whenever `muteSoundNames` is changed
    private void OnMuteSoundNamesChanged() {
        UpdateMuteSoundSet();
    }

    private void OnDestroy() {
        harmony.UnpatchSelf();
    }
}
