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
    static void Prefix(string _msg, ref List<int> _recipientEntityIds)
    {
        if (_msg != null && _msg.StartsWith("/bot "))
        {
            // Clear recipients so no players see it in-game, but server still logs it
            if (_recipientEntityIds == null)
            {
                _recipientEntityIds = new List<int>();
            }
            else
            {
                _recipientEntityIds.Clear();
            }
        }
    }
}