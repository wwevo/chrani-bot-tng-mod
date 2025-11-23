using HarmonyLib;
using System.Reflection;
using System;
using System.Collections.Generic;

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

[HarmonyPatch(typeof(GameManager), "ChatMessageServer")]
public class ChatMessagePatch
{
    static bool Prefix(ClientInfo _cInfo, EChatType _chatType, int _senderEntityId, string _msg, List<int> _recipientEntityIds, EMessageSender _msgSender, BbCodeSupportMode _bbMode)
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