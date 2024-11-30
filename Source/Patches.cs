using HarmonyLib;
using NineSolsAPI;
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
