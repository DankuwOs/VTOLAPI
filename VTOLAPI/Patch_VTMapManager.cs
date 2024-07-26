using HarmonyLib;

namespace VTOLAPI;

[HarmonyPatch(typeof(VTMapManager), nameof(VTMapManager.RestartCurrentScenario))]
public class Patch_VTMapManager
{
    static void Postfix(VTMapManager __instance)
    {
        VTAPI.instance.WaitForScenarioReload();
    }
}
