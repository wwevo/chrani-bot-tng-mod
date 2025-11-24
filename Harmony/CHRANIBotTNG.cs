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
        Console.WriteLine("[CHRANIBotTNG] Loading");

        modPath = _modInstance.Path;

        // Load persisted mute list
        MuteStorage.Initialize(modPath);
        MutedPlayers = MuteStorage.LoadMutedPlayers();

        // Load admin list from serveradmin.xml
        AdminManager.Initialize();

        var harmony = new Harmony("com.chranibottng.mod");
        harmony.PatchAll(Assembly.GetExecutingAssembly());

        Console.WriteLine($"[CHRANIBotTNG] Loaded - {MutedPlayers.Count} muted players, {AdminManager.GetAdminCount()} admins");
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
        Console.WriteLine($"[MuteStorage] Initialized: {muteFilePath}");
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
                    Console.WriteLine($"[MuteStorage] Loaded {players.Count} muted players");
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
                Console.WriteLine($"[MuteStorage] Saved {players.Count} muted players");
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

// ==================== ADMIN MANAGER (serveradmin.xml) ====================
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
            // Try common locations
            // GetSaveGameDir() returns the world folder (e.g., Saves/RWG/WorldName)
            // serveradmin.xml is in the Saves folder, so we need to go up 2 levels
            string[] possiblePaths = new[]
            {
                Path.Combine(GameIO.GetSaveGameDir(), "..", "..", "serveradmin.xml"), // Saves/serveradmin.xml (standard location)
                Path.Combine(GameIO.GetSaveGameDir(), "..", "serveradmin.xml"),       // Saves/RWG/serveradmin.xml
                Path.Combine(GameIO.GetSaveGameDir(), "serveradmin.xml"),             // Saves/RWG/WorldName/serveradmin.xml
                "serveradmin.xml"                                                      // Working directory
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

        // Log all attempted paths
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
            XDocument doc = XDocument.Load(serverAdminPath);

            adminSteamIDs = doc.Descendants("user")
                .Where(u =>
                {
                    var permStr = u.Attribute("permission_level")?.Value;
                    return int.TryParse(permStr, out int perm) && perm < 1000;
                })
                .Select(u => u.Attribute("userid")?.Value)
                .Where(id => !string.IsNullOrEmpty(id))
                .ToHashSet();

            Console.WriteLine($"[AdminManager] Loaded {adminSteamIDs.Count} admins (permission < 1000)");
            foreach (var id in adminSteamIDs)
            {
                Console.WriteLine($"  Admin: {id}");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"[AdminManager] Error loading serveradmin.xml: {e.Message}");
        }
    }

    public static bool IsAdmin(ClientInfo _cInfo)
    {
        if (_cInfo == null || _cInfo.InternalId == null) return false;
        return adminSteamIDs.Contains(_cInfo.InternalId.ReadablePlatformUserIdentifier);
    }

    public static int GetAdminCount()
    {
        return adminSteamIDs.Count;
    }

    public static void Reload()
    {
        if (serverAdminPath != null)
        {
            Console.WriteLine("[AdminManager] Reloading serveradmin.xml");
            LoadAdmins();
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
                    Console.WriteLine($"[CHRANIBotTNG] Non-admin {_cInfo?.playerName} tried to mute - denied");
                }
                else
                {
                    string targetId = parts[2];
                    CHRANIBotTNG.MutedPlayers.Add(targetId);
                    MuteStorage.SaveMutedPlayers(CHRANIBotTNG.MutedPlayers);
                    Console.WriteLine($"[CHRANIBotTNG] Admin {_cInfo?.playerName} muted: {targetId}");
                }
            }
            else if (parts.Length >= 3 && parts[1].ToLower() == "unmute")
            {
                // Check if user is admin
                if (!AdminManager.IsAdmin(_cInfo))
                {
                    Console.WriteLine($"[CHRANIBotTNG] Non-admin {_cInfo?.playerName} tried to unmute - denied");
                }
                else
                {
                    string targetId = parts[2];
                    if (CHRANIBotTNG.MutedPlayers.Remove(targetId))
                    {
                        MuteStorage.SaveMutedPlayers(CHRANIBotTNG.MutedPlayers);
                        Console.WriteLine($"[CHRANIBotTNG] Admin {_cInfo?.playerName} unmuted: {targetId}");
                    }
                    else
                    {
                        Console.WriteLine($"[CHRANIBotTNG] Player was not muted: {targetId}");
                    }
                }
            }
            else if (parts.Length >= 2 && parts[1].ToLower() == "mutelist")
            {
                // Show muted players list
                if (!AdminManager.IsAdmin(_cInfo))
                {
                    Console.WriteLine($"[CHRANIBotTNG] Non-admin {_cInfo?.playerName} tried to view mutelist - denied");
                }
                else
                {
                    Console.WriteLine($"[CHRANIBotTNG] Muted players ({CHRANIBotTNG.MutedPlayers.Count}):");
                    foreach (var muted in CHRANIBotTNG.MutedPlayers)
                    {
                        Console.WriteLine($"  - {muted}");
                    }
                }
            }
            else if (parts.Length >= 2 && parts[1].ToLower() == "reload")
            {
                // Reload serveradmin.xml
                if (!AdminManager.IsAdmin(_cInfo))
                {
                    Console.WriteLine($"[CHRANIBotTNG] Non-admin {_cInfo?.playerName} tried to reload - denied");
                }
                else
                {
                    AdminManager.Reload();
                    Console.WriteLine($"[CHRANIBotTNG] Admin {_cInfo?.playerName} reloaded serveradmin.xml");
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