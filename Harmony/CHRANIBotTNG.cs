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
    static bool Prefix(ClientInfo _cInfo, string _msg, List<int> _recipientEntityIds)
    {
        if (_msg != null && _msg.StartsWith("/bot "))
        {
            string playerName = _cInfo != null ? _cInfo.playerName : "Server";

            // Write to server log (visible in telnet)
            Console.WriteLine($"Chat (from '{playerName}', entity id '{(_cInfo != null ? _cInfo.entityId.ToString() : "-1")}', to '{(_recipientEntityIds != null && _recipientEntityIds.Count > 0 ? "players" : "all")}'): '{_msg}'");

            // Block in-game chat display
            return false;
        }
        return true;
    }
}