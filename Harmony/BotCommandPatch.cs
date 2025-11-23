using HarmonyLib;
using System.Reflection;

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

[HarmonyPatch(typeof(ChatCommandManager))]
[HarmonyPatch("ExecuteCommand")]
public class ChatCommandPatch
{
    static bool Prefix(string _command, ClientInfo _cInfo)
    {
        if (_command != null && _command.StartsWith("/bot "))
        {
            Log.Out($"[Bot] {(_cInfo != null ? _cInfo.playerName : "Server")}: {_command}");
            return false;
        }
        return true;
    }
}
