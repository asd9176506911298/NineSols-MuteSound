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
        public ConfigEntry<bool> isToastReplace = null!;
        public ConfigEntry<bool> isReplaceSound = null!;


        private WaveOutEvent waveOut;
        private Mp3FileReader mp3FileReader;

        public HashSet<string> muteSoundSet = new(); // Store sound names for fast lookups
        public Dictionary<string, string> replaceSoundNames = new(); // Sound names to MP3 paths mapping
        private Harmony harmony = null!;

        private FileSystemWatcher replaceSoundFileWatcher; // Watch for replace sound file changes
        private FileSystemWatcher muteSoundFileWatcher; // Watch for mute sound file changes

        private void Awake() {
            Instance = this;
            Log.Init(Logger);
            RCGLifeCycle.DontDestroyForever(gameObject);

            harmony = Harmony.CreateAndPatchAll(typeof(MuteSound).Assembly);

            // Configuration options
            isMute = Config.Bind("Enable", "Mute Sound", false, "Mute specific sounds by name");

            isReplaceSound = Config.Bind("Enable", "Replace Sound", false, new ConfigDescription("Replace specific sounds by name", null,
                        new ConfigurationManagerAttributes { Order = 1 }));

            isToast = Config.Bind("", "Toast Play SoundName", false, new ConfigDescription("Show toast messages for sound playback", null,
                        new ConfigurationManagerAttributes { Order = 2 }));
            isToastMute = Config.Bind("", "Toast Mute SoundName", false, new ConfigDescription("Show mute sound playback", null,
                        new ConfigurationManagerAttributes { Order = 1 }));

            isToastReplace = Config.Bind("", "Toast Replace", false, new ConfigDescription("Show replace sound", null,
                        new ConfigurationManagerAttributes { Order = 1 }));


            // Initialize mute sound set
            LoadMuteSoundNamesFromFile();
            LoadReplaceSoundNamesFromFile();

            // Set up file watchers to monitor changes
            SetUpFileWatchers();

            Log.Info($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }

        private void SetUpFileWatchers() {
            string replaceSoundFilePath = Path.Combine(Paths.ConfigPath, "replaceSoundNames.json");
            string muteSoundFilePath = Path.Combine(Paths.ConfigPath, "muteSoundNames.json");

            // Watch for changes in the replaceSoundNames file
            replaceSoundFileWatcher = new FileSystemWatcher(Path.GetDirectoryName(replaceSoundFilePath)!, "replaceSoundNames.json") {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
            };
            replaceSoundFileWatcher.Changed += (sender, e) => {
                if (e.ChangeType == WatcherChangeTypes.Changed) {
                    Log.Info("replaceSoundNames.json file has changed. Reloading...");
                    LoadReplaceSoundNamesFromFile();
                }
            };
            replaceSoundFileWatcher.EnableRaisingEvents = true;

            // Watch for changes in the muteSoundNames file
            muteSoundFileWatcher = new FileSystemWatcher(Path.GetDirectoryName(muteSoundFilePath)!, "muteSoundNames.json") {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
            };
            muteSoundFileWatcher.Changed += (sender, e) => {
                if (e.ChangeType == WatcherChangeTypes.Changed) {
                    Log.Info("muteSoundNames.json file has changed. Reloading...");
                    LoadMuteSoundNamesFromFile();
                }
            };
            muteSoundFileWatcher.EnableRaisingEvents = true;
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

        private void LoadMuteSoundNamesFromFile() {
            string filePath = Path.Combine(Paths.ConfigPath, "muteSoundNames.json");

            if (File.Exists(filePath)) {
                try {
                    string jsonContent = File.ReadAllText(filePath);
                    var loadedMuteNames = JsonConvert.DeserializeObject<List<string>>(jsonContent);
                    muteSoundSet = new HashSet<string>(loadedMuteNames ?? new List<string>());
                    Log.Info("Successfully loaded mute sound names from file.");
                } catch (Exception ex) {
                    Log.Error("Failed to load muteSoundNames from file: " + ex.Message);
                    muteSoundSet = new HashSet<string>();
                }
            } else {
                Log.Warning("muteSoundNames.json file not found. Using default empty set.");
                muteSoundSet = new HashSet<string>();
            }
        }

        public void PlayMP3(string filePath) {
            if (!File.Exists(filePath)) {
                ToastManager.Toast($"MP3 file not found. path:{filePath}");
                return;
            }

            try {
                mp3FileReader = new Mp3FileReader(filePath);
                waveOut = new WaveOutEvent();
                waveOut.Init(mp3FileReader);
                AdjustVolume();
                waveOut.Play();
                //ToastManager.Toast("Playing MP3: " + filePath);
            } catch (Exception ex) {
                Log.Error("Error playing MP3: " + ex.Message);
            }
        }

        private void AdjustVolume() {
            float gameVolume = AudioListener.volume;
            float sfxVolume = SaveManager.Instance.SettingPlayerPref.SFX.CurrentValue;
            float normalizedSFXVolume = Mathf.Clamp01(sfxVolume / 10f);
            float finalVolume = gameVolume * normalizedSFXVolume;
            waveOut.Volume = finalVolume;
        }

        private void OnDestroy() {
            harmony.UnpatchSelf();
            waveOut?.Stop();
            mp3FileReader?.Dispose();

            // Dispose of file watchers
            replaceSoundFileWatcher?.Dispose();
            muteSoundFileWatcher?.Dispose();
        }
    }
}
