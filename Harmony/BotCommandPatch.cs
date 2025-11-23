using HarmonyLib;
using System.Reflection;
using UnityEngine;

public class BotCommandMod : IModApi
{
    public void InitMod(Mod _modInstance)
    {
        Debug.Log("[BotCommandMod] Loading");
        var harmony = new Harmony("com.botcommand.mod");
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        Debug.Log("[BotCommandMod] Loaded");
    }
}

[HarmonyPatch(typeof(NetPackageChat))]
[HarmonyPatch("ProcessPackage")]
public class ChatCommandPatch
{
    static bool Prefix(NetPackageChat __instance, World _world)
    {
        if (__instance.Message != null && __instance.Message.StartsWith("/bot "))
        {
            ClientInfo cInfo = ConnectionManager.Instance.Clients.ForEntityId(__instance.Sender);
            string playerName = cInfo != null ? cInfo.playerName : "Unknown";
            Debug.Log($"[Bot] {playerName}: {__instance.Message}");
            return false;
        }
        return true;
    }
}
