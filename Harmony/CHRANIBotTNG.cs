using HarmonyLib;
using System.Reflection;
using System;

public class CHRANIBotTNG : IModApi
{
    public void InitMod(Mod _modInstance)
    {
        Console.WriteLine("[CHRANIBotTNG] Loading");
        var harmony = new Harmony("com.chranibottng.mod");
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        Console.WriteLine("[CHRANIBotTNG] Loaded");
    }
}

[HarmonyPatch(typeof(ChatCommandManager), "ProcessCommand")]
public class ChatCommandPatch
{
    static bool Prefix(ChatCommandManager __instance, string _chatText, ClientInfo _cInfo)
    {
        if (_chatText != null && _chatText.StartsWith("/bot "))
        {
            string playerName = _cInfo != null ? _cInfo.playerName : "Server";
            Console.WriteLine($"[Bot] {playerName}: {_chatText}");
            return false;
        }
        return true;
    }
}