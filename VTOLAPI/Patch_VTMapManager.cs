using HarmonyLib;

namespace VTOLAPI;

[HarmonyPatch(typeof(VTMapManager), nameof(VTMapManager.RestartCurrentScenario))]
public class Patch_VTMapManager
{
    public static void Postfix()
    {
        VTAPI.instance.WaitForScenarioReload();
    }
}
