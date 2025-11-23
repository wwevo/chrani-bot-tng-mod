using HarmonyLib;
using System.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;

public class CHRANIBotTNG : IModApi
{
    public static HashSet<string> MutedPlayers = new HashSet<string>();

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
    static void Prefix(ClientInfo _cInfo, string _msg, ref List<int> _recipientEntityIds)
    {
        // Handle /bot commands
        if (_msg != null && _msg.StartsWith("/bot "))
        {
            string[] parts = _msg.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 3 && parts[1].ToLower() == "mute")
            {
                string targetId = parts[2];
                CHRANIBotTNG.MutedPlayers.Add(targetId);
                Console.WriteLine($"[CHRANIBotTNG] Muted player: {targetId}");
            }
            else if (parts.Length >= 3 && parts[1].ToLower() == "unmute")
            {
                string targetId = parts[2];
                if (CHRANIBotTNG.MutedPlayers.Remove(targetId))
                {
                    Console.WriteLine($"[CHRANIBotTNG] Unmuted player: {targetId}");
                }
                else
                {
                    Console.WriteLine($"[CHRANIBotTNG] Player was not muted: {targetId}");
                }
            }

            // Clear recipients so no players see /bot commands in-game
            if (_recipientEntityIds == null)
            {
                _recipientEntityIds = new List<int>();
            }
            else
            {
                _recipientEntityIds.Clear();
            }
            return;
        }

        // Check if sender is muted
        if (_cInfo != null && IsPlayerMuted(_cInfo))
        {
            Console.WriteLine($"[CHRANIBotTNG] Blocked message from muted player: {_cInfo.playerName}");

            // Clear recipients so no players see the message
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

    static bool IsPlayerMuted(ClientInfo _cInfo)
    {
        // Check EntityID
        if (CHRANIBotTNG.MutedPlayers.Contains(_cInfo.entityId.ToString()))
            return true;

        // Check player name
        if (CHRANIBotTNG.MutedPlayers.Contains(_cInfo.playerName))
            return true;

        // Check SteamID (InternalId)
        if (_cInfo.InternalId != null && CHRANIBotTNG.MutedPlayers.Contains(_cInfo.InternalId.ReadablePlatformUserIdentifier))
            return true;

        return false;
    }
}