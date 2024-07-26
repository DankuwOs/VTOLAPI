global using static VTOLAPI.Logger;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Mod_Loader.Classes;
using ModLoader.Framework;
using ModLoader.Framework.Attributes;
using SteamQueries.Models;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using VTOLVR.Multiplayer;

namespace VTOLAPI;

[ItemId("danku-vtolapi")]
public class VTAPI : VtolMod
{
    public static VTAPI instance { get; private set; }

    /// <summary>
    /// This gets invoked when the scene has changed and finished loading. 
    /// This should be the safest way to start running code when a level is loaded.
    /// </summary>
    public static UnityAction<VTScenes> SceneLoaded;

    /// <summary>
    /// This gets invoked when the mission as been reloaded by the player.
    /// </summary>
    public static UnityAction MissionReloaded;

    /// <summary>
    /// The current scene which is active.
    /// </summary>
    public static VTScenes currentScene { get; private set; }



    private void Awake()
    {
        instance = this;

        SceneManager.activeSceneChanged += ActiveSceneChanged;
    }


    #region Scene Stuff

    private void ActiveSceneChanged(Scene current, Scene next)
    {
        Log($"Active Scene Changed to [{next.buildIndex}]{next.name}");
        var scene = (VTScenes)next.buildIndex;
        switch (scene)
        {
            case VTScenes.Akutan:
            case VTScenes.CustomMapBase:
            case VTScenes.CustomMapBase_OverCloud:
                StartCoroutine(WaitForScenario(scene));
                break;
            default:
                CallSceneLoaded(scene);
                break;
        }
    }

    private IEnumerator WaitForScenario(VTScenes Scene)
    {
        while (VTMapManager.fetch == null || !VTMapManager.fetch.scenarioReady)
        {
            yield return null;
        }

        CallSceneLoaded(Scene);
    }

    private void CallSceneLoaded(VTScenes Scene)
    {
        currentScene = Scene;
        if (SceneLoaded != null)
            SceneLoaded.Invoke(Scene);
    }

    /// <summary>
    /// Please don't use this, this is for the mod loader only.
    /// </summary>
    public void WaitForScenarioReload()
    {
        StartCoroutine(Wait());
    }

    private IEnumerator Wait()
    {
        while (!VTMapManager.fetch.scenarioReady)
        {
            yield return null;
        }

        if (MissionReloaded != null)
            MissionReloaded.Invoke();
    }


    #endregion


    /// <summary>
    /// [MP Supported]
    /// Searches for the game object of the player by using the prefab name appending (Clone).
    /// For multiplayer it uses the lobby manager to get the local player
    /// </summary>
    /// <returns></returns>
    public static GameObject GetPlayersVehicleGameObject()
    {
        if (VTOLMPUtils.IsMultiplayer())
        {
            return VTOLMPLobbyManager.localPlayerInfo.vehicleObject;
        }

        string vehicleName = PilotSaveManager.currentVehicle.vehiclePrefab.name;
        return GameObject.Find($"{vehicleName}(Clone)");
    }

    /// <summary>
    /// Returns which vehicle the player is using in a Enum.
    /// </summary>
    /// <returns></returns>
    [Obsolete]
    public static VTOLVehicles GetPlayersVehicleEnum()
    {
        if (PilotSaveManager.currentVehicle == null)
            return VTOLVehicles.None;

        string vehicleName = PilotSaveManager.currentVehicle.vehicleName;
        switch (vehicleName)
        {
            case "AV-42C":
                return VTOLVehicles.AV42C;
            case "F/A-26B":
                return VTOLVehicles.FA26B;
            case "F-45A":
                return VTOLVehicles.F45A;
            case "AH-94":
                return VTOLVehicles.AH94;
            case "T-55":
                return VTOLVehicles.T55;
            case "EF-24G":
                return VTOLVehicles.EF24G;
            default:
            {
                return string.IsNullOrEmpty(vehicleName) ? VTOLVehicles.None : VTOLVehicles.Custom;
            }
        }
    }


    public override void UnLoad()
    {

    }

    public IReadOnlyCollection<SteamItem> FindSteamItems()
    {
        var currentPage = 1;
        const int maxPages = 100;
        var returnValue = new List<SteamItem>();

        while (true)
        {
            if (currentPage > maxPages)
            {
                // Just stopping it if it goes too far
                break;
            }

            var pageResults = ModLoader.ModLoader.Instance._steamQueries.GetSubscribedItems(currentPage);
            if (pageResults.result == null)
            {
                break;
            }

            if (!pageResults.result.HasValues)
            {
                Debug.LogWarning("Get Subscribed Items didn't have any values");
                break;
            }

            var visibleItems = pageResults.result.Items.ToArray();

            if (!visibleItems.Any())
            {
                // No more subbed items
                break;
            }

            returnValue.AddRange(visibleItems);

            currentPage++;
        }

        return returnValue;
    }

    public IReadOnlyCollection<SteamItem> FindLocalItems()
    {
        return ModLoader.ModLoader.Instance.FindLocalItems();
    }

    public bool IsItemLoaded(string directory)
    {
        return ModLoader.ModLoader.Instance.IsItemLoaded(directory);
    }

    // Incase somebody needs it.
    public UniTask<bool> TryLoadSteamItem(SteamItem item)
    {
        return ModLoader.ModLoader.Instance.LoadSteamItem(item);
    }

    public void LoadSteamItem(SteamItem item)
    {
        ModLoader.ModLoader.Instance.LoadSteamItem(item);
    }

    // Incase somebody needs it.
    public UniTask TaskDisableSteamItem(SteamItem item)
    {
        return ModLoader.ModLoader.Instance.DisableSteamItem(item);
    }
    
    public void DisableSteamItem(SteamItem item)
    {
        ModLoader.ModLoader.Instance.DisableSteamItem(item);
    }
}