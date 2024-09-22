using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using HarmonyLib;
using Player;
using Props.Elevator;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace StanleyElevator;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInProcess("DDSS.exe")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Class Declaration", "BepInEx002:Classes with BepInPlugin attribute must inherit from BaseUnityPlugin", Justification = "It inherits from BaseUnityPlugin")]
public class Plugin : BaseUnityPlugin
{
    private AudioSource audioSource;
    private Lazy<PlayerController> localPlayer = new(() => LobbyManager.instance.GetLocalPlayerController());

    private void Awake()
    {
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        CreateAudioSource();
        InitMusic();

        InitPatches();

        var harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        harmony.PatchAll();
    }

    #region Music
    private void CreateAudioSource()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.loop = false; // Just in case it won't work
    }

    private void InitMusic()
    {
        AudioClip musicClip = LoadFromDiskToAudioClip(Path.Combine(Paths.PluginPath, "resources", "spe.mp3"), AudioType.MPEG);
        audioSource.clip = musicClip;
    }

    public AudioClip LoadFromDiskToAudioClip(string path, AudioType type)
    {
        AudioClip clip = null;
        using (UnityWebRequest uwr = UnityWebRequestMultimedia.GetAudioClip(path, type))
        {
            uwr.SendWebRequest();
            try
            {
                while (!uwr.isDone)
                {

                }

                if (uwr.result != UnityWebRequest.Result.Success)
                {
                    Logger.LogError($"Failed to load {type} AudioClip from path: {path} Full error: {uwr.error}");
                }
                else
                {
                    clip = DownloadHandlerAudioClip.GetContent(uwr);
                }
            }
            catch (Exception err)
            {
                Logger.LogError($"{err.Message}, {err.StackTrace}");
            }
        }

        return clip;
    }
    #endregion

    private void InitPatches()
    {
        ElevatorPatch.PlayElevatorMusic = (bool isMoving) =>
        {
            if (audioSource.clip == null) return;

            var musicVolume = GameSettingsManager.instance.GetSetting("Music Volume");
            var masterVolume = GameSettingsManager.instance.GetSetting("Master Volume");
            audioSource.volume = musicVolume * masterVolume;

            if (isMoving && localPlayer.Value.isInElevator && !audioSource.isPlaying)
            {
                audioSource.Play();
            }
            else if (!isMoving && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        };
        ElevatorPatch._Logger = Logger;
    }
}

[HarmonyPatch(typeof(ElevatorController), nameof(ElevatorController.Update))]
class ElevatorPatch
{
    public static Action<bool> PlayElevatorMusic { get; set; }
    public static ManualLogSource _Logger { get; set; }

    private static void Prefix(ElevatorController __instance)
    {
        PlayElevatorMusic.Invoke(__instance.isMoving);
    }
}

static class PlayerHelper
{
    public static PlayerController GetLocalPlayerController(this LobbyManager lobbyManager)
    {
        return lobbyManager.GetLocalPlayer().playerController.GetComponent<PlayerController>();
    }
}