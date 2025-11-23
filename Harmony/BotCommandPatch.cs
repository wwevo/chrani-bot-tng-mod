using HarmonyLib;
using System.Reflection;
using System;

public class BotCommandMod : IModApi
{
    public void InitMod(Mod _modInstance)
    {
        Console.WriteLine("[BotCommandMod] Loading");
        var harmony = new Harmony("com.botcommand.mod");
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        Console.WriteLine("[BotCommandMod] Loaded");
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

