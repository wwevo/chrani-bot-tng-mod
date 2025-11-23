using HarmonyLib;
using System.Reflection;
using System;
using System.Linq;

public class BotCommandMod : IModApi
{
    public void InitMod(Mod _modInstance)
    {
        Console.WriteLine("[BotCommandMod] Loading");

        // List all GameManager methods containing "Chat"
        var gmType = typeof(GameManager);
        Console.WriteLine("[BotCommandMod] GameManager methods with 'Chat':");
        foreach (var method in gmType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
        {
            if (method.Name.Contains("Chat"))
            {
                var parameters = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name));
                Console.WriteLine($"  {method.Name}({parameters})");
            }
        }

        Console.WriteLine("[BotCommandMod] Loaded");
    }
}

