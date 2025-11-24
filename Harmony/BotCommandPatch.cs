using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

public class BotCommandMod : IModApi
{
    private static Harmony harmony;

    public void InitMod(Mod _modInstance)
    {
        Console.WriteLine("[BotCommandMod] Loading");

        // Initialize managers
        string modPath = _modInstance.Path;
        MuteManager.Initialize(modPath);
        AdminPermissionManager.Initialize();

        // Apply Harmony patches
        harmony = new Harmony("wwevo.botcommand");
        harmony.PatchAll(Assembly.GetExecutingAssembly());

        Console.WriteLine("[BotCommandMod] Loaded successfully");
        Console.WriteLine($"[BotCommandMod] Muted players: {MuteManager.GetMutedCount()}");
        Console.WriteLine($"[BotCommandMod] Admin count: {AdminPermissionManager.GetAdminCount()}");
    }
}

// ==================== MUTE MANAGER ====================
public static class MuteManager
{
    private static string dataFilePath;
    private static MuteData muteData = new MuteData();
    private static readonly object fileLock = new object();

    public class MutedPlayer
    {
        public string SteamID { get; set; }
        public string PlayerName { get; set; }
        public long MutedUntilTimestamp { get; set; } // Unix timestamp, 0 = permanent
        public string Reason { get; set; }
        public string MutedBy { get; set; }
        public long MutedAtTimestamp { get; set; }
    }

    public class MuteData
    {
        public List<MutedPlayer> MutedPlayers { get; set; } = new List<MutedPlayer>();
    }

    public static void Initialize(string modPath)
    {
        string dataDir = Path.Combine(modPath, "Data");
        if (!Directory.Exists(dataDir))
        {
            Directory.CreateDirectory(dataDir);
        }

        dataFilePath = Path.Combine(dataDir, "muted_players.json");
        LoadData();
        Console.WriteLine($"[MuteManager] Initialized with file: {dataFilePath}");
    }

    private static void LoadData()
    {
        lock (fileLock)
        {
            try
            {
                if (File.Exists(dataFilePath))
                {
                    string json = File.ReadAllText(dataFilePath);
                    muteData = ParseJson(json);

                    // Clean up expired mutes
                    long currentTime = GetUnixTimestamp();
                    int removed = muteData.MutedPlayers.RemoveAll(p =>
                        p.MutedUntilTimestamp > 0 && p.MutedUntilTimestamp < currentTime);

                    if (removed > 0)
                    {
                        SaveData();
                        Console.WriteLine($"[MuteManager] Removed {removed} expired mutes");
                    }

                    Console.WriteLine($"[MuteManager] Loaded {muteData.MutedPlayers.Count} muted players");
                }
                else
                {
                    Console.WriteLine("[MuteManager] No existing data file, starting fresh");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[MuteManager] Error loading data: {e.Message}");
                muteData = new MuteData();
            }
        }
    }

    private static void SaveData()
    {
        lock (fileLock)
        {
            try
            {
                string json = ToJson(muteData);
                File.WriteAllText(dataFilePath, json);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[MuteManager] Error saving data: {e.Message}");
            }
        }
    }

    public static void MutePlayer(string steamID, string playerName, long durationSeconds, string reason, string mutedBy)
    {
        lock (fileLock)
        {
            // Remove existing mute if present
            muteData.MutedPlayers.RemoveAll(p => p.SteamID == steamID);

            long currentTime = GetUnixTimestamp();
            long mutedUntil = durationSeconds > 0 ? currentTime + durationSeconds : 0; // 0 = permanent

            muteData.MutedPlayers.Add(new MutedPlayer
            {
                SteamID = steamID,
                PlayerName = playerName,
                MutedUntilTimestamp = mutedUntil,
                Reason = reason ?? "No reason specified",
                MutedBy = mutedBy,
                MutedAtTimestamp = currentTime
            });

            SaveData();
            Console.WriteLine($"[MuteManager] Muted player {playerName} ({steamID}) until {(mutedUntil > 0 ? DateTimeOffset.FromUnixTimeSeconds(mutedUntil).ToString() : "permanent")}");
        }
    }

    public static void UnmutePlayer(string steamID)
    {
        lock (fileLock)
        {
            int removed = muteData.MutedPlayers.RemoveAll(p => p.SteamID == steamID);
            if (removed > 0)
            {
                SaveData();
                Console.WriteLine($"[MuteManager] Unmuted player with SteamID {steamID}");
            }
        }
    }

    public static bool IsPlayerMuted(string steamID)
    {
        long currentTime = GetUnixTimestamp();
        var mute = muteData.MutedPlayers.FirstOrDefault(p => p.SteamID == steamID);

        if (mute == null) return false;

        // Check if temporary mute has expired
        if (mute.MutedUntilTimestamp > 0 && mute.MutedUntilTimestamp < currentTime)
        {
            UnmutePlayer(steamID);
            return false;
        }

        return true;
    }

    public static MutedPlayer GetMuteInfo(string steamID)
    {
        return muteData.MutedPlayers.FirstOrDefault(p => p.SteamID == steamID);
    }

    public static List<MutedPlayer> GetAllMutedPlayers()
    {
        return muteData.MutedPlayers.ToList();
    }

    public static int GetMutedCount()
    {
        return muteData.MutedPlayers.Count;
    }

    private static long GetUnixTimestamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    // Simple JSON serialization (manual to avoid dependencies)
    private static string ToJson(MuteData data)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"MutedPlayers\": [");

        for (int i = 0; i < data.MutedPlayers.Count; i++)
        {
            var player = data.MutedPlayers[i];
            sb.AppendLine("    {");
            sb.AppendLine($"      \"SteamID\": \"{EscapeJson(player.SteamID)}\",");
            sb.AppendLine($"      \"PlayerName\": \"{EscapeJson(player.PlayerName)}\",");
            sb.AppendLine($"      \"MutedUntilTimestamp\": {player.MutedUntilTimestamp},");
            sb.AppendLine($"      \"Reason\": \"{EscapeJson(player.Reason)}\",");
            sb.AppendLine($"      \"MutedBy\": \"{EscapeJson(player.MutedBy)}\",");
            sb.AppendLine($"      \"MutedAtTimestamp\": {player.MutedAtTimestamp}");
            sb.Append("    }");
            if (i < data.MutedPlayers.Count - 1) sb.Append(",");
            sb.AppendLine();
        }

        sb.AppendLine("  ]");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static MuteData ParseJson(string json)
    {
        MuteData data = new MuteData();

        try
        {
            // Simple manual JSON parsing
            string[] lines = json.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            MutedPlayer currentPlayer = null;

            foreach (string line in lines)
            {
                string trimmed = line.Trim();

                if (trimmed.StartsWith("{") && currentPlayer == null && !trimmed.Contains("MutedPlayers"))
                {
                    currentPlayer = new MutedPlayer();
                }
                else if (trimmed.StartsWith("}") && currentPlayer != null)
                {
                    data.MutedPlayers.Add(currentPlayer);
                    currentPlayer = null;
                }
                else if (currentPlayer != null)
                {
                    if (trimmed.Contains("\"SteamID\""))
                        currentPlayer.SteamID = ExtractStringValue(trimmed);
                    else if (trimmed.Contains("\"PlayerName\""))
                        currentPlayer.PlayerName = ExtractStringValue(trimmed);
                    else if (trimmed.Contains("\"MutedUntilTimestamp\""))
                        currentPlayer.MutedUntilTimestamp = ExtractLongValue(trimmed);
                    else if (trimmed.Contains("\"Reason\""))
                        currentPlayer.Reason = ExtractStringValue(trimmed);
                    else if (trimmed.Contains("\"MutedBy\""))
                        currentPlayer.MutedBy = ExtractStringValue(trimmed);
                    else if (trimmed.Contains("\"MutedAtTimestamp\""))
                        currentPlayer.MutedAtTimestamp = ExtractLongValue(trimmed);
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"[MuteManager] Error parsing JSON: {e.Message}");
        }

        return data;
    }

    private static string ExtractStringValue(string line)
    {
        int firstQuote = line.IndexOf('"', line.IndexOf(':'));
        int lastQuote = line.LastIndexOf('"');
        if (firstQuote >= 0 && lastQuote > firstQuote)
        {
            return line.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
        }
        return "";
    }

    private static long ExtractLongValue(string line)
    {
        int colonIndex = line.IndexOf(':');
        if (colonIndex >= 0)
        {
            string valueStr = line.Substring(colonIndex + 1).Trim().TrimEnd(',');
            if (long.TryParse(valueStr, out long value))
            {
                return value;
            }
        }
        return 0;
    }

    private static string EscapeJson(string str)
    {
        if (string.IsNullOrEmpty(str)) return "";
        return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }
}

// ==================== ADMIN PERMISSION MANAGER ====================
public static class AdminPermissionManager
{
    private static List<AdminUser> admins = new List<AdminUser>();
    private static string serverAdminPath;

    public class AdminUser
    {
        public string Platform { get; set; }
        public string SteamID { get; set; }
        public string Name { get; set; }
        public int PermissionLevel { get; set; }
    }

    public static void Initialize()
    {
        // Try to find serveradmin.xml
        serverAdminPath = FindServerAdminXml();

        if (serverAdminPath != null)
        {
            LoadAdmins();
        }
        else
        {
            Console.WriteLine("[AdminPermissionManager] serveradmin.xml not found, admin checking disabled");
        }
    }

    private static string FindServerAdminXml()
    {
        // Common paths for serveradmin.xml
        string[] possiblePaths = new[]
        {
            Path.Combine(GameIO.GetSaveGameDir(), "..", "serveradmin.xml"),
            Path.Combine(GameIO.GetSaveGameDir(), "serveradmin.xml"),
            "serveradmin.xml"
        };

        foreach (string path in possiblePaths)
        {
            try
            {
                string fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                {
                    Console.WriteLine($"[AdminPermissionManager] Found serveradmin.xml at: {fullPath}");
                    return fullPath;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[AdminPermissionManager] Error checking path {path}: {e.Message}");
            }
        }

        return null;
    }

    private static void LoadAdmins()
    {
        try
        {
            if (!File.Exists(serverAdminPath))
            {
                Console.WriteLine("[AdminPermissionManager] serveradmin.xml does not exist");
                return;
            }

            XDocument doc = XDocument.Load(serverAdminPath);

            admins = doc.Descendants("user")
                .Where(u =>
                {
                    var permStr = u.Attribute("permission_level")?.Value;
                    return int.TryParse(permStr, out int perm) && perm < 1000;
                })
                .Select(u => new AdminUser
                {
                    Platform = u.Attribute("platform")?.Value ?? "Steam",
                    SteamID = u.Attribute("userid")?.Value,
                    Name = u.Attribute("name")?.Value,
                    PermissionLevel = int.TryParse(u.Attribute("permission_level")?.Value, out int perm) ? perm : 1000
                })
                .ToList();

            Console.WriteLine($"[AdminPermissionManager] Loaded {admins.Count} admins with permission level < 1000:");
            foreach (var admin in admins)
            {
                Console.WriteLine($"  - {admin.Name} ({admin.SteamID}) [Level {admin.PermissionLevel}]");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"[AdminPermissionManager] Error loading serveradmin.xml: {e.Message}");
        }
    }

    public static bool IsAdmin(string steamID)
    {
        return admins.Any(a => a.SteamID == steamID);
    }

    public static AdminUser GetAdmin(string steamID)
    {
        return admins.FirstOrDefault(a => a.SteamID == steamID);
    }

    public static int GetPermissionLevel(string steamID)
    {
        var admin = GetAdmin(steamID);
        return admin?.PermissionLevel ?? 1000; // Default permission level for non-admins
    }

    public static List<AdminUser> GetAllAdmins()
    {
        return admins.ToList();
    }

    public static int GetAdminCount()
    {
        return admins.Count;
    }

    public static void Reload()
    {
        Console.WriteLine("[AdminPermissionManager] Reloading serveradmin.xml");
        LoadAdmins();
    }
}

// ==================== HARMONY PATCH FOR CHAT COMMANDS ====================
[HarmonyPatch(typeof(ChatCommandManager))]
[HarmonyPatch("ProcessCommand")]
public class ChatCommandPatch
{
    static bool Prefix(string _command, ClientInfo _cInfo)
    {
        try
        {
            if (_cInfo == null) return true;

            string steamID = _cInfo.InternalId.ReadablePlatformUserIdentifier;
            string playerName = _cInfo.playerName;

            // Check if player is muted
            if (MuteManager.IsPlayerMuted(steamID))
            {
                var muteInfo = MuteManager.GetMuteInfo(steamID);
                if (muteInfo != null)
                {
                    string message;
                    if (muteInfo.MutedUntilTimestamp == 0)
                    {
                        message = $"You are permanently muted. Reason: {muteInfo.Reason}";
                    }
                    else
                    {
                        long remainingSeconds = muteInfo.MutedUntilTimestamp - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        message = $"You are muted for {remainingSeconds / 60} more minutes. Reason: {muteInfo.Reason}";
                    }

                    _cInfo.SendPackage(NetPackageManager.GetPackage<NetPackageChat>().Setup(
                        EChatType.Whisper, -1, message, "", false, null));
                }

                return false; // Block the command
            }

            // Handle bot commands
            if (_command.StartsWith("/bot"))
            {
                HandleBotCommand(_command, _cInfo, steamID, playerName);
                return false; // Prevent default processing
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"[ChatCommandPatch] Error in Prefix: {e}");
        }

        return true; // Allow default processing
    }

    private static void HandleBotCommand(string command, ClientInfo cInfo, string steamID, string playerName)
    {
        string[] parts = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
        {
            SendMessage(cInfo, "Bot commands: /bot mute <player> [duration] [reason] | /bot unmute <player> | /bot mutelist | /bot admins | /bot reload");
            return;
        }

        string subCommand = parts[1].ToLower();

        switch (subCommand)
        {
            case "mute":
                if (!AdminPermissionManager.IsAdmin(steamID))
                {
                    SendMessage(cInfo, "You don't have permission to use this command.");
                    return;
                }

                if (parts.Length < 3)
                {
                    SendMessage(cInfo, "Usage: /bot mute <player> [durationMinutes] [reason]");
                    return;
                }

                string targetPlayer = parts[2];
                long durationSeconds = 3600; // Default 1 hour
                string reason = "No reason specified";

                if (parts.Length >= 4 && int.TryParse(parts[3], out int minutes))
                {
                    durationSeconds = minutes * 60;
                }

                if (parts.Length >= 5)
                {
                    reason = string.Join(" ", parts.Skip(4));
                }

                // Find target player
                ClientInfo targetCInfo = FindPlayer(targetPlayer);
                if (targetCInfo != null)
                {
                    string targetSteamID = targetCInfo.InternalId.ReadablePlatformUserIdentifier;
                    MuteManager.MutePlayer(targetSteamID, targetCInfo.playerName, durationSeconds, reason, playerName);
                    SendMessage(cInfo, $"Muted player {targetCInfo.playerName} for {durationSeconds / 60} minutes. Reason: {reason}");
                }
                else
                {
                    SendMessage(cInfo, $"Player '{targetPlayer}' not found.");
                }
                break;

            case "unmute":
                if (!AdminPermissionManager.IsAdmin(steamID))
                {
                    SendMessage(cInfo, "You don't have permission to use this command.");
                    return;
                }

                if (parts.Length < 3)
                {
                    SendMessage(cInfo, "Usage: /bot unmute <player>");
                    return;
                }

                string unmuteTarget = parts[2];
                ClientInfo unmuteCInfo = FindPlayer(unmuteTarget);

                if (unmuteCInfo != null)
                {
                    string targetSteamID = unmuteCInfo.InternalId.ReadablePlatformUserIdentifier;
                    MuteManager.UnmutePlayer(targetSteamID);
                    SendMessage(cInfo, $"Unmuted player {unmuteCInfo.playerName}");
                }
                else
                {
                    SendMessage(cInfo, $"Player '{unmuteTarget}' not found.");
                }
                break;

            case "mutelist":
                if (!AdminPermissionManager.IsAdmin(steamID))
                {
                    SendMessage(cInfo, "You don't have permission to use this command.");
                    return;
                }

                var mutedPlayers = MuteManager.GetAllMutedPlayers();
                if (mutedPlayers.Count == 0)
                {
                    SendMessage(cInfo, "No players are currently muted.");
                }
                else
                {
                    SendMessage(cInfo, $"Muted players ({mutedPlayers.Count}):");
                    foreach (var muted in mutedPlayers)
                    {
                        string until = muted.MutedUntilTimestamp == 0 ? "permanent" :
                            DateTimeOffset.FromUnixTimeSeconds(muted.MutedUntilTimestamp).ToString("yyyy-MM-dd HH:mm");
                        SendMessage(cInfo, $"  {muted.PlayerName} until {until} - {muted.Reason}");
                    }
                }
                break;

            case "admins":
                var admins = AdminPermissionManager.GetAllAdmins();
                if (admins.Count == 0)
                {
                    SendMessage(cInfo, "No admins loaded (serveradmin.xml not found or empty).");
                }
                else
                {
                    SendMessage(cInfo, $"Admins ({admins.Count}):");
                    foreach (var admin in admins)
                    {
                        SendMessage(cInfo, $"  {admin.Name} [Level {admin.PermissionLevel}]");
                    }
                }
                break;

            case "reload":
                if (!AdminPermissionManager.IsAdmin(steamID))
                {
                    SendMessage(cInfo, "You don't have permission to use this command.");
                    return;
                }

                AdminPermissionManager.Reload();
                SendMessage(cInfo, "Reloaded admin permissions from serveradmin.xml");
                break;

            default:
                SendMessage(cInfo, $"Unknown bot command: {subCommand}");
                break;
        }
    }

    private static ClientInfo FindPlayer(string nameOrId)
    {
        // Try exact name match first
        foreach (var client in ConnectionManager.Instance.Clients.List)
        {
            if (client.playerName.Equals(nameOrId, StringComparison.OrdinalIgnoreCase))
            {
                return client;
            }
        }

        // Try partial name match
        foreach (var client in ConnectionManager.Instance.Clients.List)
        {
            if (client.playerName.IndexOf(nameOrId, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return client;
            }
        }

        // Try Steam ID match
        foreach (var client in ConnectionManager.Instance.Clients.List)
        {
            if (client.InternalId.ReadablePlatformUserIdentifier == nameOrId)
            {
                return client;
            }
        }

        return null;
    }

    private static void SendMessage(ClientInfo cInfo, string message)
    {
        cInfo.SendPackage(NetPackageManager.GetPackage<NetPackageChat>().Setup(
            EChatType.Whisper, -1, message, "", false, null));
    }
}
