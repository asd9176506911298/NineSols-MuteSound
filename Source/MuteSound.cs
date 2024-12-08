using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using NAudio.Wave;
using NineSolsAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;


namespace MuteSound;

[BepInDependency(NineSolsAPICore.PluginGUID)]
[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class MuteSound : BaseUnityPlugin {
    public static MuteSound Instance { get; private set; }
    public ConfigEntry<bool> isMute = null!;
    public ConfigEntry<bool> isToast = null!;
    public ConfigEntry<bool> isToastMute = null!;
    public ConfigEntry<string> muteSoundNames = null!; // Store multiple sound names as a single, comma-separated string
    public ConfigEntry<string> soundPath = null!; // Store multiple sound names as a single, comma-separated string


    private WaveOutEvent waveOut;
    private Mp3FileReader mp3FileReader;

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

        KeybindManager.Add(this, test, () => new KeyboardShortcut(KeyCode.A, KeyCode.LeftControl));

        muteSoundNames.SettingChanged += (s, e) => OnMuteSoundNamesChanged();

        Log.Info($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
    }

    private void PlayMP3(string filePath) {
        if (!File.Exists(filePath)) {
            ToastManager.Toast("MP3 file not found.");
            return;
        }

        try {
            // Initialize MP3 reader
            mp3FileReader = new Mp3FileReader(filePath);
            waveOut = new WaveOutEvent();

            // Set the output to play the MP3 file
            waveOut.Init(mp3FileReader);

            // Adjust the volume to match the game volume
            AdjustVolume();

            // Start playing
            waveOut.Play();

            // Log status
            ToastManager.Toast("Playing MP3: " + filePath);
        } catch (Exception ex) {
            ToastManager.Toast("Error playing MP3: " + ex.Message);
        }
    }

    private void AdjustVolume() {
        // Get the global game volume from AudioListener (usually between 0.0f and 1.0f)
        float gameVolume = AudioListener.volume;

        // Get the SFX volume from your settings, assuming it's between 0 and 10
        float sfxVolume = SaveManager.Instance.SettingPlayerPref.SFX.CurrentValue;

        // Normalize SFX volume to a range of 0.0f to 1.0f (since it is currently from 0 to 10)
        float normalizedSFXVolume = Mathf.Clamp01(sfxVolume / 10f);

        // Combine the game volume with the SFX volume, adjusting as needed
        // For example, you can choose to multiply both volumes together to blend them
        float finalVolume = gameVolume * normalizedSFXVolume;

        // Set the MP3 playback volume to match the final calculated volume
        waveOut.Volume = finalVolume;

        // Optionally log the values for debugging purposes
        ToastManager.Toast("Game Volume: " + gameVolume + ", SFX Volume: " + sfxVolume + ", Final Volume: " + finalVolume);
    }


    void test() {
        ToastManager.Toast("Attempting to play MP3");
        PlayMP3("E:/Download/VO_NuWa_Emotion_Impatience_v1.mp3");
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

        waveOut?.Stop();
        mp3FileReader?.Dispose();
    }
}
