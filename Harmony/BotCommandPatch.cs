using HarmonyLib;
using System.Reflection;
using System.Collections.Generic;
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

[HarmonyPatch(typeof(GameManager))]
[HarmonyPatch("ChatMessageServer")]
public class ChatMessagePatch
{
    static bool Prefix(ClientInfo _cInfo, EChatType _type, int _senderId, string _msg, string _mainName, bool _localizeMain, List<int> _recipientEntityIds)
    {
        if (_msg != null && _msg.StartsWith("/bot "))
        {
            string playerName = _cInfo != null ? _cInfo.playerName : "Server";
            Console.WriteLine($"[Bot] {playerName}: {_msg}");
            return false;
        }
        return true;
    }
}
