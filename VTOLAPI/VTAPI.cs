global using static VTOLAPI.Logger;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using ModLoader.Framework;
using ModLoader.Framework.Attributes;
using OC;
using SteamQueries.Models;
using Steamworks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityStandardAssets.ImageEffects;
using VTOLVR;
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

    /// <summary>
    /// Array containing all of the shaders from the original game, before any mods have loaded their own.
    /// </summary>
    public static Shader[] AllVanillaShaders;

    
    /// <summary>
    /// List containing all of the HPEquippables from the original game, before any mods have loaded their own.
    /// </summary>
    public static readonly List<HPEquippable> AllVanillaHPEquips = new List<HPEquippable>();
    
    /// <summary>
    /// List containing all of the Missiles from the original game, before any mods have loaded their own.
    /// </summary>
    public static readonly List<Missile> AllVanillaMissiles = new List<Missile>();
    

    private void Awake()
    {
        instance = this;

        AllVanillaShaders = Resources.FindObjectsOfTypeAll<Shader>();

        
        // Grab HPEquips and Missiles from Player Vehicles
        foreach (var pv in VTResources.playerVehicles.playerVehicles.Where(pv => GetVehicleEnum(pv.vehiclePrefab) != VTOLVehicles.Custom))
        {
            foreach (var pvEquipPrefab in pv.allEquipPrefabs)
            {
                var hpEquippable = pvEquipPrefab.GetComponent<HPEquippable>();
                
                if (AllVanillaHPEquips.All(hpEquip => hpEquip.name != hpEquippable.name))
                    AllVanillaHPEquips.Add(hpEquippable);

                foreach (var missileLauncher in hpEquippable.GetComponentsInChildren<MissileLauncher>(true))
                {
                    if (AllVanillaMissiles.All(missile => missile.name != missileLauncher.missilePrefab.name))
                        AllVanillaMissiles.Add(missileLauncher.missilePrefab.GetComponent<Missile>());
                }
            }
        }
        
        // Grab HPEquips and Missiles from AI Units
        UnitCatalogue.UpdateCatalogue();
        foreach (var unitPrefabKvP in UnitCatalogue.unitPrefabs)
        {
            
            foreach (var missileLauncher in unitPrefabKvP.Value.GetComponentsInChildren<MissileLauncher>(true))
            {
                if (AllVanillaMissiles.All(missile => missile.name != missileLauncher.missilePrefab.name))
                    AllVanillaMissiles.Add(missileLauncher.missilePrefab.GetComponent<Missile>());
            }
            
            var unitSpawnEquippable = unitPrefabKvP.Value.GetComponent<AIUnitSpawnEquippable>();
            if (!unitSpawnEquippable)
                continue;
            
            foreach (var equipPrefab in unitSpawnEquippable.equipPrefabs)
            {
                var hpEquippable = equipPrefab.GetComponent<HPEquippable>();
                
                if (AllVanillaHPEquips.All(hpEquip => hpEquip.name != hpEquippable.name))
                    AllVanillaHPEquips.Add(hpEquippable);
                
                
                foreach (var missileLauncher in hpEquippable.GetComponentsInChildren<MissileLauncher>(true))
                {
                    if (AllVanillaMissiles.All(missile => missile.name != missileLauncher.missilePrefab.name))
                        AllVanillaMissiles.Add(missileLauncher.missilePrefab.GetComponent<Missile>());
                }
            }
        }
        
        SceneManager.activeSceneChanged += ActiveSceneChanged;
    }
    
    public override void UnLoad()
    {
        SceneManager.activeSceneChanged -= ActiveSceneChanged;
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
    /// Searches for the game object of the player by using the flight scene manager.
    /// For multiplayer it uses the lobby manager to get the local player
    /// </summary>
    /// <returns></returns>
    public static GameObject GetPlayersVehicleGameObject()
    {
        if (VTOLMPUtils.IsMultiplayer())
        {
            return VTOLMPLobbyManager.localPlayerInfo.vehicleObject;
        }
        
        return FlightSceneManager.instance?.playerActor != null ? FlightSceneManager.instance.playerActor.gameObject : null;
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


    private static bool TryFixShader(object component, FieldInfo fieldInfo, Type targetType, Action<object> action)
    {
        Type fieldType = fieldInfo.FieldType;

        switch (fieldType)
        {
            case { } t when t == targetType:
                action?.Invoke(fieldInfo.GetValue(component));
                return true;
            case { IsArray: true } t when t.GetElementType() == targetType:

                if (fieldInfo.GetValue(component) is Array array)
                {
                    for (int i = 0; i < array.Length; i++)
                    {
                        action?.Invoke(array.GetValue(i));
                    }
                    return true;
                }
                
                break;
            case { IsGenericType: true } t when t.GetGenericArguments().Any(a => a == targetType):
                
                if (fieldInfo.GetValue(component) is IList list)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        action?.Invoke(list[i]);
                    }

                    return true;
                }
                
                break;
        }
        
        return false;
    }

    /// <summary>
    /// Finds all shaders on object and tries to fix them
    /// </summary>
    /// <param name="rootObject"></param>
    /// <param name="prefabs">Search through referenced prefabs as well</param>
    /// <param name="allComponents">Also searches all components (apart from graphics) and checks all fields to find materials and shaders (including lists) instead of just the common components</param>
    public static void FixShaders(GameObject rootObject, bool fixDankuShaders = false, bool prefabs = true, bool allComponents = true)
    {
        if (allComponents || prefabs)
        {
            var components = rootObject.GetComponentsInChildren<Component>(true);

            Shader shader;
            Shader result;
            
            foreach (var component in components)
            {
                var type = component.GetType();
                if (type == typeof(MeshRenderer)
                    || type == typeof(SkinnedMeshRenderer)
                    || type == typeof(CameraReplacementShader)
                    || type == typeof(Grayscale)
                    || type.IsSubclassOf(typeof(Graphic)))
                    continue;
                
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var fieldInfo in fields)
                {
                    if (allComponents)
                    {
                        // SHADER
                        if (TryFixShader(component, fieldInfo, typeof(Shader), o =>
                            {
                                shader = (Shader)o;
                                if (shader == null)
                                    return;
                                if (shader != null && TryFixShader(shader, out result, fixDankuShaders))
                                    fieldInfo.SetValue(component, result);
                            }))
                            continue;

                        // MATERIAL
                        if (TryFixShader(component, fieldInfo, typeof(Material), o =>
                            {
                                var mat = (Material)o;
                                if (mat == null)
                                    return;
                                if (mat != null && TryFixShader(mat.shader, out result, fixDankuShaders))
                                    mat.shader = result;
                            }))
                            continue;
                    }
                    
                    // PREFABS
                    if (prefabs && TryFixShader(component, fieldInfo, typeof(GameObject), o =>
                        {
                            var go = (GameObject)o;
                            if (go == null || go.transform.IsChildOf(rootObject.transform))
                                return;
                            FixShaders(go);
                        }))
                        continue;
                }
            }
        }
        
        var meshRenderers = rootObject.GetComponentsInChildren<MeshRenderer>(true);
        foreach (var renderer in meshRenderers)
        {
            foreach (var rendererSharedMaterial in renderer.sharedMaterials)
            {
                if (rendererSharedMaterial && TryFixShader(rendererSharedMaterial.shader, out var result, fixDankuShaders))
                    rendererSharedMaterial.shader = result;
            }
        }
        
        var skinnedMeshRenderers = rootObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var renderer in skinnedMeshRenderers)
        {
            foreach (var skinnedRendererSharedMaterial in renderer.sharedMaterials)
            {
                if (skinnedRendererSharedMaterial && TryFixShader(skinnedRendererSharedMaterial.shader, out var result, fixDankuShaders))
                    skinnedRendererSharedMaterial.shader = result;
            }
        }
        
        var cameraReplacementComponents = rootObject.GetComponentsInChildren<CameraReplacementShader>(true);
        foreach (var replacementShader in cameraReplacementComponents)
        {
            if (TryFixShader(replacementShader.shader, out var result, fixDankuShaders))
                replacementShader.shader = result;
        }
        
        var grayscaleComponents = rootObject.GetComponentsInChildren<Grayscale>(true);
        foreach (var grayscale in grayscaleComponents)
        {
            if (TryFixShader(grayscale.shader, out var result, fixDankuShaders))
                grayscale.shader = result;
        }
    }


    private static bool TryFixShader(Shader s, out Shader result, bool fixDankuShaders = false) // gross, i know, lmao!
    {
        // i added cloud shadows to a lot of stuff
        if(fixDankuShaders && s.name.ToLower().Contains("(danku"))
        {
            int idx = s.name.ToLower().IndexOf("(danku", StringComparison.Ordinal);
            string shaderName = s.name.Remove(idx).TrimEnd(' ');
            if (AllVanillaShaders.Any(shader => shaderName == shader.name))
            {
                result = AllVanillaShaders.First(shader => shaderName == shader.name);
                return true;
            }
        }
        if (AllVanillaShaders.Any(shader => s.name == shader.name))
        {
            result = AllVanillaShaders.First(shader => s.name == shader.name);
            return true;
        }
        
        result = null;
        return false;
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

    [Obsolete("Synchronous version no longer works for some reason, switch to FindSteamItemsCoroutine or FindSteamItemsAsync.", true)]
    public static IReadOnlyCollection<SteamItem> FindSteamItems()
    {
        throw new Exception("Please use FindSteamItemsCoroutine or FindSteamItemsAsync instead, synchronous method no longer works.");
    }

    public static IEnumerator FindSteamItemsCoroutine(Action<IReadOnlyCollection<SteamItem>> resultHandler)
    {
        yield return FindSteamItemsAsync().ToCoroutine(resultHandler);
    }
    
    public static async UniTask<IReadOnlyCollection<SteamItem>> FindSteamItemsAsync()
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

            var pageResults = await ModLoader.SteamQuery.SteamQueries.Instance.GetSubscribedItems(currentPage);
            if (pageResults == null)
            {
                break;
            }

            if (!pageResults.HasValues)
            {
                LogWarn("Get Subscribed Items didn't have any values");
                break;
            }

            var visibleItems = pageResults.Items.ToArray();

            if (!visibleItems.Any())
            {
                // No more subbed items
                break;
            }

            returnValue.AddRange(visibleItems);

            currentPage++;
            await UniTask.WaitForEndOfFrame(instance);
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

    #region EpicOCMenu (Gone

    private VTOverCloudTester _overCloudTester;

    private void OnGUI()
    {
        if (!_overCloudTester)
            _overCloudTester = FindObjectOfType<VTOverCloudTester>();
        if (!_overCloudTester)
            return;
        
		if (_overCloudTester.showDebug)
		{
			int num = Mathf.RoundToInt(120f / _overCloudTester.lineHeight);
			OverCloud.VolumetricClouds volumetricClouds = OverCloud.volumetricClouds;
			GUI.Box(_overCloudTester.SettingRect(num, 3), string.Empty);
			GUI.Label(_overCloudTester.SettingRect(num++, 1), string.Format("FPS: {0}", Mathf.RoundToInt(_overCloudTester.avgFps)));
			GUI.Label(_overCloudTester.SettingRect(num++, 1), string.Format("OC Buffer Res: {0} x {1}", OverCloud.bufferWidth, OverCloud.bufferHeight));
			GUI.Label(_overCloudTester.SettingRect(num++, 1), string.Format("OC Downsample Res: {0} x {1}", OverCloud.bufferWidthDS, OverCloud.bufferHeightDS));
			if (_overCloudTester.msaaIdx == -1)
			{
				_overCloudTester.msaaIdx = _overCloudTester.msaaOpts.IndexOf(QualitySettings.antiAliasing);
			}
			GUI.Box(_overCloudTester.SettingRect(num, 2), string.Empty);
			GUI.Label(_overCloudTester.SettingRect(num++, 1), string.Format("MSAA: {0}x", QualitySettings.antiAliasing));
			_overCloudTester.msaaIdx = Mathf.RoundToInt(GUI.HorizontalSlider(_overCloudTester.SliderRect(num++), (float)_overCloudTester.msaaIdx, 0f, 3f));
			if (_overCloudTester.msaaOpts[_overCloudTester.msaaIdx] != QualitySettings.antiAliasing)
			{
				QualitySettings.antiAliasing = _overCloudTester.msaaOpts[_overCloudTester.msaaIdx];
			}
			GUI.Box(_overCloudTester.SettingRect(num, 2), string.Empty);
			GUI.Label(_overCloudTester.SettingRect(num++, 1), "VCloud Radius: " + volumetricClouds.cloudPlaneRadius.ToString());
			volumetricClouds.cloudPlaneRadius = GUI.HorizontalSlider(_overCloudTester.SliderRect(num++), volumetricClouds.cloudPlaneRadius, 1f, 32000f);
			GUI.Box(_overCloudTester.SettingRect(num, 2), string.Empty);
			GUI.Label(_overCloudTester.SettingRect(num++, 1), "Particle Count: " + volumetricClouds.particleCount.ToString());
			float num2 = GUI.HorizontalSlider(_overCloudTester.SliderRect(num++), (float)volumetricClouds.particleCount, 0f, 16000f);
			volumetricClouds.particleCount = Mathf.RoundToInt(num2);
			GUI.Box(_overCloudTester.SettingRect(num, 1), string.Empty);
			OverCloud.Lighting.CloudShadows cloudShadows = OverCloud.lighting.cloudShadows;
			cloudShadows.enabled = GUI.Toggle(_overCloudTester.SettingRect(num++, 1), cloudShadows.enabled, "Cloud Shadows");
			GUI.Box(_overCloudTester.SettingRect(num, 2), string.Empty);
			GUI.Label(_overCloudTester.SettingRect(num++, 1), "Cloud Shadow Coverage: " + cloudShadows.coverage.ToString());
			_overCloudTester.csCoverageOpt = Mathf.RoundToInt(GUI.HorizontalSlider(_overCloudTester.SliderRect(num++), (float)_overCloudTester.csCoverageOpt, 0f, 2f));
			cloudShadows.coverage = _overCloudTester.csCoverageOptions[_overCloudTester.csCoverageOpt];
			GUI.Box(_overCloudTester.SettingRect(num, 4), string.Empty);
			OverCloud.TimeOfDay timeOfDay = OverCloud.timeOfDay;
			float num3 = (float)VT_OCTimeOfDay.UTCToLocal(timeOfDay.time, OverCloud.timeOfDay.longitude);
			GUI.Label(_overCloudTester.SettingRect(num++, 1), "Time of Day: " + UIUtils.FormattedTime(num3 * 3600f, false));
			if (VTScenarioEditor.editorRunning)
			{
				GUI.Label(_overCloudTester.SettingRect(num++, 1), " - set in Scenario Info");
			}
			else if (VTScenario.isScenarioHost)
			{
				float num4 = GUI.HorizontalSlider(_overCloudTester.SliderRect(num++), num3, 0f, 23.9f);
				if (Mathf.Abs(num4 - num3) > 1E-45f)
				{
					num3 = num4;
					timeOfDay.time = VT_OCTimeOfDay.LocalToUTC((double)num3, timeOfDay.longitude);
					_overCloudTester.SnapTimeNextFrame(num3);
				}
			}
			else
			{
				GUI.Label(_overCloudTester.SettingRect(num++, 1), " - set by host");
			}
			if (VTScenarioEditor.editorRunning)
			{
				GUI.Label(_overCloudTester.SettingRect(num++, 1), "ToD Speed can't be changed in editor");
				num++;
			}
			else if (!VTOLMPUtils.IsMultiplayer())
			{
				GUI.Label(_overCloudTester.SettingRect(num++, 1), "ToD Speed: " + timeOfDay.playSpeed.ToString());
				timeOfDay.playSpeed = GUI.HorizontalSlider(_overCloudTester.SliderRect(num++), timeOfDay.playSpeed, 1f, 200f);
			}
			else
			{
				GUI.Label(_overCloudTester.SettingRect(num++, 1), "ToD Speed can't be changed in MP");
				num++;
			}
			num++;
			OverCloud.instance.GetPresetNames(_overCloudTester.presetNames);
			GUI.Box(_overCloudTester.SettingRect(num, 1 + _overCloudTester.presetNames.Count), string.Empty);
			GUI.Label(_overCloudTester.SettingRect(num++, 1), "Presets");
			if (VTScenarioEditor.editorRunning)
			{
				GUI.Label(_overCloudTester.SettingRect(num++, 1), " - set in Scenario Info");
				num += 7;
			}
			else
			{
				if (VTScenario.isScenarioHost)
				{
					int num5 = 0;
					using (List<string>.Enumerator enumerator = _overCloudTester.presetNames.GetEnumerator())
					{
						while (enumerator.MoveNext())
						{
							string text = enumerator.Current;
							_overCloudTester.PresetButton(num5, text, num++);
							num5++;
						}
						goto IL_0534;
					}
				}
				GUI.Label(_overCloudTester.SettingRect(num++, 1), "Only the host can change weather presets.");
				num += 7;
			}
			IL_0534:
			num++;
			if (GUI.Button(_overCloudTester.SliderRect(num++), "Get Camera Settings"))
			{
				_overCloudTester.oCam = Camera.main.GetComponent<OverCloudCamera>();
			}
			if (_overCloudTester.oCam != null)
			{
				GUI.Box(_overCloudTester.SettingRect(num, 6), string.Empty);
				_overCloudTester.oCam.renderVolumetricClouds = GUI.Toggle(_overCloudTester.SettingRect(num++, 1), _overCloudTester.oCam.renderVolumetricClouds, "Render Volumetric Clouds");
				_overCloudTester.oCam.render2DFallback = GUI.Toggle(_overCloudTester.SettingRect(num++, 1), _overCloudTester.oCam.render2DFallback, "Render 2D Fallback");
				_overCloudTester.oCam.renderScatteringMask = GUI.Toggle(_overCloudTester.SettingRect(num++, 1), _overCloudTester.oCam.renderScatteringMask, "Render Scatter Mask");
				OverCloud.doBlurScatterMask = GUI.Toggle(_overCloudTester.SettingRect(num++, 1), OverCloud.doBlurScatterMask, "Blur Scatter Mask");
				_overCloudTester.oCam.includeCascadedShadows = GUI.Toggle(_overCloudTester.SettingRect(num++, 1), _overCloudTester.oCam.includeCascadedShadows, "Include Cascaded Shadow");
				_overCloudTester.oCam.downsample2DClouds = GUI.Toggle(_overCloudTester.SettingRect(num++, 1), _overCloudTester.oCam.downsample2DClouds, "Downsample 2D Clouds");
				if (_overCloudTester.currDsFac == -1)
				{
					_overCloudTester.currDsFac = _overCloudTester.dsOpts.IndexOf(_overCloudTester.oCam.downsampleFactor);
				}
				GUI.Box(_overCloudTester.SettingRect(num, 2), string.Empty);
				GUI.Label(_overCloudTester.SettingRect(num++, 1), string.Format("Cloud Downsample Factor: {0}", _overCloudTester.dsOpts[_overCloudTester.currDsFac]));
				_overCloudTester.currDsFac = Mathf.RoundToInt(GUI.HorizontalSlider(_overCloudTester.SliderRect(num++), (float)_overCloudTester.currDsFac, 0f, (float)(_overCloudTester.dsOpts.Length - 1)));
				if (_overCloudTester.oCam.downsampleFactor != _overCloudTester.dsOpts[_overCloudTester.currDsFac])
				{
					_overCloudTester.oCam.downsampleFactor = _overCloudTester.dsOpts[_overCloudTester.currDsFac];
				}
			}
		}
	}

    #endregion
}