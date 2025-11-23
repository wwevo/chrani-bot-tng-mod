using HarmonyLib;
using System.Reflection;
using System.Collections.Generic;

public class BotCommandMod : IModApi
{
    public void InitMod(Mod _modInstance)
    {
        Log.Out("[BotCommandMod] Loading");
        var harmony = new Harmony("com.botcommand.mod");
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        Log.Out("[BotCommandMod] Loaded");
    }
}

[HarmonyPatch(typeof(ConsoleCmdAbstract))]
[HarmonyPatch("getCommands")]
public class ChatInterceptPatch
{
    static void Postfix(ref string __result)
    {
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
            Log.Out($"[Bot] {playerName}: {_msg}");
            return false;
        }
        return true;
    }
}
