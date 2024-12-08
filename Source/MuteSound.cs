using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using NAudio.Wave;
using NineSolsAPI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

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
            isReplaceSound = Config.Bind("Enable", "Replace Sound", false, "Replace specific sounds by name");
            isToast = Config.Bind("", "Toast Play SoundName", false, "Show toast messages for sound playback");
            isToastMute = Config.Bind("", "Toast Mute SoundName", false, "Show mute sound playback");
            isToastReplace = Config.Bind("", "Toast Replace", false, "Show replace sound");

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

                    // Display individual key-value pairs as toasts
                    DisplayReplaceSoundToasts();

                    // Summary toast
                    ToastManager.Toast($"Successfully loaded {replaceSoundNames.Count} replace sound names from file.");
                } catch (Exception ex) {
                    Log.Error("Failed to load replaceSoundNames from file: " + ex.Message);
                    replaceSoundNames = new Dictionary<string, string>();
                }
            } else {
                // If the file doesn't exist, create it with an empty structure
                Log.Warning("replaceSoundNames.json file not found. Creating a new file.");
                replaceSoundNames = new Dictionary<string, string>();
                SaveReplaceSoundNamesToFile(filePath);
            }
        }

        private void DisplayReplaceSoundToasts() {
            foreach (var pair in replaceSoundNames) {
                ToastManager.Toast($"Replace \"{pair.Key}\" to \"{pair.Value}\"");
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

                    // Display individual sound toasts and summary toast
                    DisplayIndividualSounds();
                    DisplayMuteSoundSummary();
                } catch (Exception ex) {
                    Log.Error("Failed to load muteSoundNames from file: " + ex.Message);
                    muteSoundSet = new HashSet<string>();
                }
            } else {
                // If the file doesn't exist, create it with an empty structure
                Log.Warning("muteSoundNames.json file not found. Creating a new file.");
                muteSoundSet = new HashSet<string>();
                SaveMuteSoundNamesToFile(filePath);
            }
        }

        private void DisplayIndividualSounds() {
            foreach (var soundName in muteSoundSet) {
                ToastManager.Toast(soundName); // Toast each sound individually
            }
        }

        private void DisplayMuteSoundSummary() {
            string message = $"Loaded {muteSoundSet.Count} mute sound names.";
            ToastManager.Toast(message); // Display a summary toast
        }

        // Save methods to write default empty JSON content to the files
        private void SaveReplaceSoundNamesToFile(string filePath) {
            try {
                string jsonContent = JsonConvert.SerializeObject(replaceSoundNames, Formatting.Indented);
                File.WriteAllText(filePath, jsonContent);
                Log.Info("Successfully created replaceSoundNames.json.");
            } catch (Exception ex) {
                Log.Error("Failed to create replaceSoundNames.json: " + ex.Message);
            }
        }

        private void SaveMuteSoundNamesToFile(string filePath) {
            try {
                string jsonContent = JsonConvert.SerializeObject(new List<string>(), Formatting.Indented);
                File.WriteAllText(filePath, jsonContent);
                Log.Info("Successfully created muteSoundNames.json.");
            } catch (Exception ex) {
                Log.Error("Failed to create muteSoundNames.json: " + ex.Message);
            }
        }

        public async void PlaySoundAsync(string filePath) {
            try {
                string extension = Path.GetExtension(filePath).ToLower();

                if (extension == ".mp3") {
                    // Play MP3 file
                    await PlayMp3(filePath);
                } else if (extension == ".wav") {
                    // Play WAV file
                    await PlayWav(filePath);
                } else {
                    Log.Error($"Unsupported file format: {filePath}");
                }
            } catch (Exception ex) {
                Log.Error($"Error playing sound file {filePath}: {ex.Message}");
            }
        }

        private async Task PlayMp3(string filePath) {
            using (var reader = new Mp3FileReader(filePath))
            using (waveOut = new WaveOutEvent()) {
                waveOut.Init(reader);
                AdjustVolume();
                waveOut.Play();
                while (waveOut.PlaybackState == PlaybackState.Playing) {
                    await Task.Delay(100);
                }
            }
        }

        private async Task PlayWav(string filePath) {
            using (var reader = new WaveFileReader(filePath))
            using (waveOut = new WaveOutEvent()) {
                waveOut.Init(reader);
                AdjustVolume();
                waveOut.Play();
                while (waveOut.PlaybackState == PlaybackState.Playing) {
                    await Task.Delay(100);
                }
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
