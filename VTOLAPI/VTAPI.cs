global using static VTOLAPI.Logger;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
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

    
    private static Dictionary<string, VTModVariables> ModVariables = new Dictionary<string, VTModVariables>();


    private void Awake()
    {
        instance = this;
        
        SceneManager.activeSceneChanged += ActiveSceneChanged;
    }
    
    public override void UnLoad()
    {
        Log("Bye Bye :~)");
    }

    #region Scenes

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

    #region Objects

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
    /// Returns which vehicle the player is using in an Enum.
    /// </summary>
    /// <returns></returns>
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

    public static VTOLVehicles GetVehicleEnum(GameObject vehicle)
    {
        VehicleMaster vehicleMaster = vehicle.GetComponentInChildren<VehicleMaster>(true);

        if (vehicleMaster == null)
        {
            LogError($"Could not find a VehicleMaster component in GameObject '{vehicle.name}'");
            return VTOLVehicles.None;
        }
        
        PlayerVehicle playerVehicle = vehicleMaster.playerVehicle;

        if (playerVehicle == null)
        {
            LogError($"Could not find a PlayerVehicle component in GameObject '{vehicle.name}'");
            return VTOLVehicles.None;
        }

        string vehicleName = playerVehicle.vehicleName;
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

    /// <summary>
    /// Searches the GameObject for a certain child.
    /// Useful for if you just want a GameObject within a large hierarchy
    /// </summary>
    /// <returns></returns>
    public static GameObject GetChildWithName(GameObject obj, string name)
    {
        Transform[] children = obj.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child.name == name || child.name.Contains(name + "(clone"))
            {
                return child.gameObject;
            }
        }


        return null;
    }

    /// <summary>
    /// Searches the GameObject for a certain interactable by the name of the interactable.
    /// </summary>
    /// <returns></returns>
    public static VRInteractable FindInteractable(string interactableName)
    {
        GameObject playerGameObject = GetPlayersVehicleGameObject();

        if (playerGameObject == null)
        {
            LogError($"Could not find VRInteractable, players game object is null.");
            return null;
        }
        
        foreach (VRInteractable interactable in GetPlayersVehicleGameObject().GetComponentsInChildren<VRInteractable>(true))
        {
            if (interactable.interactableName == interactableName)
            {
                return interactable;
            }
        }

        LogError($"Could not find VRInteractable: '{interactableName}'");
        return null;
    }

    /// <summary>
    /// Searches the GameObject for a certain interactable by the name of the interactable.
    /// </summary>
    /// <returns></returns>
    public static VRInteractable FindInteractable(GameObject gameObject, string interactableName)
    {
        
        foreach (VRInteractable interactable in gameObject.GetComponentsInChildren<VRInteractable>(true))
        {
            if (interactable.interactableName == interactableName)
            {
                return interactable;
            }
        }

        LogError($"Could not find VRInteractable: '{interactableName}'");
        return null;
    }

    #endregion

    #region ModVariables

    /// <summary>
    /// Registers a variable that other mods can access to modify your variables.
    /// </summary>
    /// <param name="modId">Mod ID must be specific to your mod.</param>
    /// <param name="modVariable">Class that contains the actions to modify the variable.</param>
    /// <code>
    /// string epicString = "C-137 Is soooo coool, i love his A-10 mod :~)";
    /// VTModVariable modVariable = new VTModVariable("Epic Float", epicString, OnSetValue, OnGetValue);
    ///  
    /// VTAPI.RegisterVariable("Danku-UniqueModID", modVariable);
    ///  
    /// void OnSetValue(object value) {
    ///     epicString = (string)value; // Value is type checked.
    /// }
    /// void OnGetValue(ref object value) {
    ///     value = epicString;
    /// }
    /// </code>
    public static void RegisterVariable(string modId, VTModVariable modVariable)
    {
        ModVariables ??= new Dictionary<string, VTModVariables>();

        if (ModVariables.TryGetValue(modId, out var modVariables))
        {
            modVariables.RegisterVariable(modVariable);
        }
        else
        {
            modVariables = new VTModVariables(modId);
            modVariables.RegisterVariable(modVariable);
            
            ModVariables.Add(modId, modVariables);
        }
    }
    
    /// <summary>
    /// Unregisters a variable for the mod.
    /// </summary>
    /// <param name="modId">Mod ID must be specific to your mod.</param>
    /// <param name="variableName">Name of the variable to unregister, must be the same as the one you registered.</param>
    public static void UnregisterVariable(string modId, string variableName)
    {
        if (!ModVariables.TryGetValue(modId, out var modVariables))
        {
            LogWarn($"Tried to unregister variable '{modId}:{variableName}' but couldn't find it in ModVariables?");
            return;
        }
        modVariables.UnregisterVariable(variableName);
    }
    
    /// <summary>
    /// Unregisters your mod so that nothing can access its variables anymore.
    /// </summary>
    /// <param name="modId">Mod ID must be specific to your mod.</param>
    public static void UnregisterMod(string modId)
    {
        if (!ModVariables.TryGetValue(modId, out var modVariables)) return;
        
        modVariables.Unregistered = true;
        ModVariables.Remove(modId);
    }
    
    /// <param name="modId">Mod ID must be specific to your mod.</param>
    /// <param name="modVariables">Class that contains all the variables for the modId.</param>
    /// <returns>True if the modId is registered</returns>
    /// <code>
    /// VTModVariables modVariables;
    /// if (TryGetModVariables("Danku-UniqueModID", out modVariables))
    /// {
    ///     if (modVariables.TryGetValue("Epic String", out var epicString))
    ///     {
    ///         Debug.Log($"Got EPIC string '{epicString}'");
    ///     }
    /// }
    /// </code>
    public static bool TryGetModVariables(string modId, out VTModVariables modVariables)
    {
        return ModVariables.TryGetValue(modId, out modVariables);

    }

    #endregion

    #region SteamItems

    public static IReadOnlyCollection<SteamItem> FindSteamItems()
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

            var pageResults = ModLoader.SteamQuery.SteamQueries.Instance.GetSubscribedItems(currentPage);
            if (pageResults.result == null)
            {
                break;
            }

            if (!pageResults.result.HasValues)
            {
                LogWarn("Get Subscribed Items didn't have any values");
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

    public static IReadOnlyCollection<SteamItem> FindLocalItems()
    {
        return ModLoader.ModLoader.Instance.FindLocalItems();
    }
    
    public static bool IsItemLoaded(string directory)
    {
        return ModLoader.ModLoader.Instance.IsItemLoaded(directory);
    }

    // Incase somebody needs it.
    public static UniTask<bool> TryLoadSteamItem(SteamItem item)
    {
        return ModLoader.ModLoader.Instance.LoadSteamItem(item);
    }

    public static void LoadSteamItem(SteamItem item)
    {
        ModLoader.ModLoader.Instance.LoadSteamItem(item);
    }

    // Incase somebody needs it.
    public static UniTask TaskDisableSteamItem(SteamItem item)
    {
        return ModLoader.ModLoader.Instance.DisableSteamItem(item);
    }
    
    public static void DisableSteamItem(SteamItem item)
    {
        ModLoader.ModLoader.Instance.DisableSteamItem(item);
    }

    #endregion
}