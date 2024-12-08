using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using NAudio.Wave;
using NineSolsAPI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using System.Runtime.InteropServices;

namespace MuteSound {
    [BepInDependency(NineSolsAPICore.PluginGUID)]
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class MuteSound : BaseUnityPlugin {
        public static MuteSound Instance { get; private set; }
        public ConfigEntry<bool> isMute = null!;
        public ConfigEntry<bool> isToast = null!;
        public ConfigEntry<bool> isToastMute = null!;
        public ConfigEntry<string> muteSoundNames = null!;
        public ConfigEntry<bool> isReplaceSound = null!;

        private WaveOutEvent waveOut;
        private Mp3FileReader mp3FileReader;

        public HashSet<string> muteSoundSet = new(); // Store sound names for fast lookups
        public Dictionary<string, string> replaceSoundNames = new(); // Sound names to MP3 paths mapping
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

            isReplaceSound = Config.Bind("", "Enable Replace Sound", false, new ConfigDescription("Replace ", null,
                        new ConfigurationManagerAttributes { Order = 1 }));

            muteSoundNames = Config.Bind("Filter", "MuteSoundNames", "", "Comma-separated list of sound names to mute (e.g., sound1,sound2,sound3)");

            // Initialize mute sound set
            UpdateMuteSoundSet();

            LoadReplaceSoundNamesFromFile();

            // Listen for changes to mute sound names
            muteSoundNames.SettingChanged += (s, e) => OnMuteSoundNamesChanged();

            Log.Info($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }

        private void LoadReplaceSoundNamesFromFile() {
            string filePath = Path.Combine(Paths.ConfigPath, "replaceSoundNames.json");

            if (File.Exists(filePath)) {
                try {
                    string jsonContent = File.ReadAllText(filePath);
                    replaceSoundNames = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent) ?? new Dictionary<string, string>();
                    Log.Info("Successfully loaded replace sound names from file.");
                } catch (Exception ex) {
                    Log.Error("Failed to load replaceSoundNames from file: " + ex.Message);
                    replaceSoundNames = new Dictionary<string, string>();
                }
            } else {
                Log.Warning("replaceSoundNames.json file not found. Using default empty mapping.");
                replaceSoundNames = new Dictionary<string, string>();
            }
        }

        private void SaveReplaceSoundNames() {
            try {
                string jsonContent = JsonConvert.SerializeObject(replaceSoundNames, Formatting.Indented);
                File.WriteAllText(Path.Combine(Paths.ConfigPath, "replaceSoundNames.json"), jsonContent);
                Log.Info("Successfully saved replace sound names to file.");
            } catch (Exception ex) {
                Log.Error("Failed to save replaceSoundNames to file: " + ex.Message);
            }
        }

        public void PlayMP3(string filePath) {
            if (!File.Exists(filePath)) {
                ToastManager.Toast("MP3 file not found.");
                return;
            }

            try {
                mp3FileReader = new Mp3FileReader(filePath);
                waveOut = new WaveOutEvent();
                waveOut.Init(mp3FileReader);
                AdjustVolume();
                waveOut.Play();
                ToastManager.Toast("Playing MP3: " + filePath);
            } catch (Exception ex) {
                ToastManager.Toast("Error playing MP3: " + ex.Message);
            }
        }

        private void AdjustVolume() {
            float gameVolume = AudioListener.volume;
            float sfxVolume = SaveManager.Instance.SettingPlayerPref.SFX.CurrentValue;
            float normalizedSFXVolume = Mathf.Clamp01(sfxVolume / 10f);
            float finalVolume = gameVolume * normalizedSFXVolume;
            waveOut.Volume = finalVolume;
        }

        private void UpdateMuteSoundSet() {
            muteSoundSet = muteSoundNames.Value
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(name => name.Trim())
                .ToHashSet();
        }

        private void OnMuteSoundNamesChanged() {
            UpdateMuteSoundSet();
        }

        private void OnDestroy() {
            harmony.UnpatchSelf();
            waveOut?.Stop();
            mp3FileReader?.Dispose();
        }
    }
}
