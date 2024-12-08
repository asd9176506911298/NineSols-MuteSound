using HarmonyLib;
using NineSolsAPI;
using System.IO;
using UnityEngine;

namespace MuteSound;

[HarmonyPatch]
public class Patches {

    // Patch for SoundManager.PlaySound to mute specified sounds
    [HarmonyPatch(typeof(SoundManager), nameof(SoundManager.PlaySound))]
    [HarmonyPrefix]
    private static bool PatchPlaySound(
    SoundManager __instance,
    string soundName,
    GameObject soundEmitter,
    AkCallbackManager.EventCallback endCallback = null) {

        // Toast for all sound attempts if enabled
        if (MuteSound.Instance.isToast.Value) {
            ToastManager.Toast($"Play sound: {soundName}");
        }

        if (MuteSound.Instance.isReplaceSound.Value) {
            if (MuteSound.Instance.replaceSoundNames.ContainsKey(soundName)) {
                string mp3Path = MuteSound.Instance.replaceSoundNames[soundName];

                MuteSound.Instance.PlayMP3(mp3Path);
                if (MuteSound.Instance.isToastReplace.Value) {
                    string mp3FileName = Path.GetFileName(mp3Path);
                    ToastManager.Toast($"Replace \"{soundName}\" to \"{mp3FileName}\"");  // Show only the filename in the toast

                }

                return false;
            }
        }


        // If the sound is in the mute set
        if (MuteSound.Instance.muteSoundSet.Contains(soundName)) {
            // Toast for muted sounds if enabled
            if (MuteSound.Instance.isToastMute.Value) {
                ToastManager.Toast($"Muted sound: {soundName}");
            }

            // Mute sound if muting is enabled
            return !MuteSound.Instance.isMute.Value;
        }

        // Allow sound to play
        return true;
    }
}
