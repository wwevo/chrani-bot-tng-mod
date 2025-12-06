using HarmonyLib;
using System.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Xml.Linq;

public class CHRANIBotTNG : IModApi
{
    public static HashSet<string> MutedPlayers = new HashSet<string>();
    private static string modPath;

    public void InitMod(Mod _modInstance)
    {
        Log.Out("[CHRANIBotTNG] Loading");

        modPath = _modInstance.Path;

        // Load persisted mute list
        MuteStorage.Initialize(modPath);
        MutedPlayers = MuteStorage.LoadMutedPlayers();

        // Load admin list from serveradmin.xml
        AdminManager.Initialize();

        var harmony = new Harmony("com.chranibottng.mod");
        harmony.PatchAll(Assembly.GetExecutingAssembly());

        Log.Out($"[CHRANIBotTNG] Loaded - {MutedPlayers.Count} muted players, {AdminManager.GetAdminCount()} admins");
    }
}

// ==================== MUTE STORAGE (JSON Persistence) ====================
public static class MuteStorage
{
    private static string muteFilePath;
    private static readonly object fileLock = new object();

    public static void Initialize(string modPath)
    {
        string dataDir = Path.Combine(modPath, "Data");
        if (!Directory.Exists(dataDir))
        {
            Directory.CreateDirectory(dataDir);
        }
        muteFilePath = Path.Combine(dataDir, "muted_players.json");
    }

    public static HashSet<string> LoadMutedPlayers()
    {
        lock (fileLock)
        {
            try
            {
                if (File.Exists(muteFilePath))
                {
                    string json = File.ReadAllText(muteFilePath);
                    var players = ParseJsonArray(json);
                    Log.Out($"[CHRANIBotTNG] Loaded {players.Count} muted players");
                    return players;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[MuteStorage] Error loading: {e.Message}");
            }
        }
        return new HashSet<string>();
    }

    public static void SaveMutedPlayers(HashSet<string> players)
    {
        lock (fileLock)
        {
            try
            {
                string json = ToJsonArray(players);
                File.WriteAllText(muteFilePath, json);
                Log.Out($"[CHRANIBotTNG] Saved {players.Count} muted players");
            }
            catch (Exception e)
            {
                Console.WriteLine($"[MuteStorage] Error saving: {e.Message}");
            }
        }
    }

    private static string ToJsonArray(HashSet<string> players)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"MutedPlayers\": [");

        var list = players.ToList();
        for (int i = 0; i < list.Count; i++)
        {
            sb.Append($"    \"{EscapeJson(list[i])}\"");
            if (i < list.Count - 1) sb.Append(",");
            sb.AppendLine();
        }

        sb.AppendLine("  ]");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static HashSet<string> ParseJsonArray(string json)
    {
        HashSet<string> result = new HashSet<string>();
        try
        {
            // Simple parser: extract strings between quotes in the array
            bool inArray = false;
            for (int i = 0; i < json.Length; i++)
            {
                if (json[i] == '[') inArray = true;
                if (json[i] == ']') break;

                if (inArray && json[i] == '"')
                {
                    int start = i + 1;
                    int end = json.IndexOf('"', start);
                    if (end > start)
                    {
                        string value = json.Substring(start, end - start);
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            result.Add(value);
                        }
                        i = end;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"[MuteStorage] Parse error: {e.Message}");
        }
        return result;
    }

    private static string EscapeJson(string str)
    {
        if (string.IsNullOrEmpty(str)) return "";
        return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }
}

public static class AdminManager
{
    private static HashSet<string> adminSteamIDs = new HashSet<string>();
    private static string serverAdminPath;

    public static void Initialize()
    {
        serverAdminPath = FindServerAdminXml();
        if (serverAdminPath != null)
        {
            LoadAdmins();
        }
        else
        {
            Console.WriteLine("[AdminManager] serveradmin.xml not found - admin checks disabled");
        }
    }

    private static string FindServerAdminXml()
    {
        List<string> attemptedPaths = new List<string>();

        try
        {
            string[] possiblePaths = new[]
            {
                Path.Combine(GameIO.GetSaveGameDir(), "..", "..", "serveradmin.xml"),
                Path.Combine(GameIO.GetSaveGameDir(), "..", "serveradmin.xml"),
                Path.Combine(GameIO.GetSaveGameDir(), "serveradmin.xml"),
                "serveradmin.xml"
            };

            foreach (string path in possiblePaths)
            {
                try
                {
                    string fullPath = Path.GetFullPath(path);
                    attemptedPaths.Add(fullPath);

                    if (File.Exists(fullPath))
                    {
                        Console.WriteLine($"[AdminManager] Found serveradmin.xml: {fullPath}");
                        return fullPath;
                    }
                }
                catch (Exception e)
                {
                    attemptedPaths.Add($"{path} (Error: {e.Message})");
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"[AdminManager] Error finding serveradmin.xml: {e.Message}");
        }

        Console.WriteLine("[AdminManager] serveradmin.xml not found. Attempted paths:");
        foreach (var path in attemptedPaths)
        {
            Console.WriteLine($"  - {path}");
        }

        return null;
    }

    private static void LoadAdmins()
    {
        try
        {
            var permStr = "";
            XDocument doc = XDocument.Load(serverAdminPath);

            adminSteamIDs = doc.Descendants("user")
                .Where(u =>
                {
                    permStr = u.Attribute("permission_level")?.Value;
                    return int.TryParse(permStr, out int perm) && perm < 1000;
                })
                .Select(u => u.Attribute("userid")?.Value)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToHashSet();

            Log.Out($"[CHRANIBotTNG] Loaded {adminSteamIDs.Count} admins (permission < 1000)");
            foreach (var id in adminSteamIDs)
            {
                Log.Out($"[CHRANIBotTNG]     Admin: {id} ({permStr})");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"[AdminManager] Error loading serveradmin.xml: {e.Message}");
        }
    }

    public static bool IsAdmin(ClientInfo _cInfo)
    {
        if (_cInfo == null || _cInfo.PlatformId == null) return false;

        DumpObject(_cInfo);
        string steamId = _cInfo.PlatformId.ReadablePlatformUserIdentifier;

        Log.Out($"[CHRANIBotTNG] Checking admin-permissions for user: {steamId}");

        // Try exact match first
        if (adminSteamIDs.Contains(steamId))
            return true;

        // Try without "Steam_" prefix (serveradmin.xml might not have the prefix)
        if (steamId.StartsWith("Steam_"))
        {
            string steamIdWithoutPrefix = steamId.Substring(6); // Remove "Steam_"
            if (adminSteamIDs.Contains(steamIdWithoutPrefix))
                return true;
        }

        return false;
    }

    public static int GetAdminCount()
    {
        return adminSteamIDs.Count;
    }

    public static void Reload()
    {
        if (serverAdminPath != null)
        {
            Log.Out($"[CHRANIBotTNG] Reloading serveradmin.xml");
            LoadAdmins();
        }
    }

    public static void DumpObject(object obj)
    {
        if (obj == null)
        {
            Console.WriteLine("Object is null");
            return;
        }

        var type = obj.GetType();

        Console.WriteLine($"Type: {type.Name}");

        // Alle öffentlichen Eigenschaften
        Console.WriteLine("Public Properties:");
        foreach (var prop in type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            try
            {
                var val = prop.GetValue(obj);
                Console.WriteLine($"  {prop.Name} = {val}");
            }
            catch { }
        }

        // Alle privaten und geschützten Eigenschaften
        Console.WriteLine("All Properties (inkl. non-public):");
        foreach (var prop in type.GetProperties(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            try
            {
                var val = prop.GetValue(obj);
                Console.WriteLine($"  {prop.Name} = {val}");
            }
            catch { }
        }

        // Alle Felder (inkl. private)
        Console.WriteLine("Fields:");
        foreach (var field in type.GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            try
            {
                var val = field.GetValue(obj);
                Console.WriteLine($"  {field.Name} = {val}");
            }
            catch { }
        }
    }
}

// ==================== PLAYERLIST COMMAND ====================
public static class PlayerListCommand
{
    public static void Execute(ClientInfo _cInfo)
    {
        try
        {
            Log.Out($"[CHRANIBotTNG] Admin {_cInfo?.playerName} requested playerlist");

            // Get persistent player data
            PersistentPlayerList persistentPlayers = GameManager.Instance?.persistentPlayers;

            if (persistentPlayers == null)
            {
                Log.Out("[CHRANIBotTNG] ERROR: Could not access persistent player list");
                return;
            }

            // Get all player data
            var allPlayers = new List<PersistentPlayerData>();
            foreach (var playerData in persistentPlayers.Players.Values)
            {
                allPlayers.Add(playerData);
            }

            // Sort by last login time (most recent first)
            allPlayers.Sort((a, b) => b.LastLogin.CompareTo(a.LastLogin));

            Log.Out($"[CHRANIBotTNG] ===== Registered Players ({allPlayers.Count}) =====");
            Log.Out($"[CHRANIBotTNG] Format: Name | Platform ID | Entity ID | Last Seen");
            Log.Out($"[CHRANIBotTNG] {new string('-', 80)}");

            // Get currently online players
            var onlinePlayers = ConnectionManager.Instance?.Clients?.List;
            var onlineIds = new HashSet<string>();

            if (onlinePlayers != null)
            {
                foreach (var client in onlinePlayers)
                {
                    if (client?.PlatformId != null)
                    {
                        onlineIds.Add(client.PlatformId.ReadablePlatformUserIdentifier);
                    }
                }
            }

            foreach (var player in allPlayers)
            {
                string playerId = player.PrimaryId?.ReadablePlatformUserIdentifier ?? "Unknown";
                string playerName = player.PlayerName != null ? player.PlayerName.ToString() : "Unknown";
                string entityId = player.EntityId.ToString();

                // Format last seen time
                DateTime lastSeen = new DateTime(player.LastLogin);
                string lastSeenStr;

                if (onlineIds.Contains(playerId))
                {
                    lastSeenStr = "[ONLINE]";
                }
                else
                {
                    TimeSpan timeSince = DateTime.Now - lastSeen;
                    if (timeSince.TotalDays >= 1)
                    {
                        lastSeenStr = $"{(int)timeSince.TotalDays}d ago ({lastSeen:yyyy-MM-dd HH:mm})";
                    }
                    else if (timeSince.TotalHours >= 1)
                    {
                        lastSeenStr = $"{(int)timeSince.TotalHours}h ago ({lastSeen:yyyy-MM-dd HH:mm})";
                    }
                    else
                    {
                        lastSeenStr = $"{(int)timeSince.TotalMinutes}m ago ({lastSeen:yyyy-MM-dd HH:mm})";
                    }
                }

                Log.Out($"[CHRANIBotTNG] {playerName,-20} | {playerId,-30} | {entityId,-8} | {lastSeenStr}");
            }

            Log.Out($"[CHRANIBotTNG] {new string('-', 80)}");
            Log.Out($"[CHRANIBotTNG] Total: {allPlayers.Count} registered players, {onlineIds.Count} currently online");
        }
        catch (Exception e)
        {
            Log.Out($"[CHRANIBotTNG] ERROR executing playerlist: {e.Message}");
            Log.Out($"[CHRANIBotTNG] Stack trace: {e.StackTrace}");
        }
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
                // Check if user is admin
                if (!AdminManager.IsAdmin(_cInfo))
                {
                    Log.Out($"[CHRANIBotTNG] Non-admin {_cInfo?.playerName} tried to mute - denied");
                }
                else
                {
                    string targetId = parts[2];
                    CHRANIBotTNG.MutedPlayers.Add(targetId);
                    MuteStorage.SaveMutedPlayers(CHRANIBotTNG.MutedPlayers);
                    Log.Out($"[CHRANIBotTNG] Admin {_cInfo?.playerName} muted: {targetId}");
                }
            }
            else if (parts.Length >= 3 && parts[1].ToLower() == "unmute")
            {
                // Check if user is admin
                if (!AdminManager.IsAdmin(_cInfo))
                {
                    Log.Out($"[CHRANIBotTNG] Non-admin {_cInfo?.playerName} tried to unmute - denied");
                }
                else
                {
                    string targetId = parts[2];
                    if (CHRANIBotTNG.MutedPlayers.Remove(targetId))
                    {
                        MuteStorage.SaveMutedPlayers(CHRANIBotTNG.MutedPlayers);
                        Log.Out($"[CHRANIBotTNG] Admin {_cInfo?.playerName} unmuted: {targetId}");
                    }
                    else
                    {
                        Log.Out($"[CHRANIBotTNG] Player was not muted: {targetId}");
                    }
                }
            }
            else if (parts.Length >= 2 && parts[1].ToLower() == "mutelist")
            {
                // Show muted players list
                if (!AdminManager.IsAdmin(_cInfo))
                {
                    Log.Out($"[CHRANIBotTNG] Non-admin {_cInfo?.playerName} tried to view mutelist - denied");
                }
                else
                {
                    Log.Out($"[CHRANIBotTNG] Muted players ({CHRANIBotTNG.MutedPlayers.Count}):");
                    foreach (var muted in CHRANIBotTNG.MutedPlayers)
                    {
                        Log.Out($"[CHRANIBotTNG]     {muted}");
                    }
                }
            }
            else if (parts.Length >= 2 && parts[1].ToLower() == "reload")
            {
                // Reload serveradmin.xml
                if (!AdminManager.IsAdmin(_cInfo))
                {
                    Log.Out($"[CHRANIBotTNG] Non-admin {_cInfo?.playerName} tried to reload - denied");
                }
                else
                {
                    AdminManager.Reload();
                    Log.Out($"[CHRANIBotTNG] Admin {_cInfo?.playerName} reloaded serveradmin.xml");
                }
            }
            else if (parts.Length >= 2 && parts[1].ToLower() == "playerlist")
            {
                // List all registered players
                if (!AdminManager.IsAdmin(_cInfo))
                {
                    Log.Out($"[CHRANIBotTNG] Non-admin {_cInfo?.playerName} tried to view playerlist - denied");
                }
                else
                {
                    PlayerListCommand.Execute(_cInfo);
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
