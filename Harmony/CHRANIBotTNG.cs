using HarmonyLib;
using System.Reflection;
using System;
using System.Linq;

public class CHRANIBotTNG : IModApi
{
    public void InitMod(Mod _modInstance)
    {
        Console.WriteLine("[CHRANIBotTNG] Loading");

        // List all GameManager methods containing "Chat"
        var gmType = typeof(GameManager);
        Console.WriteLine("[CHRANIBotTNG] GameManager methods with 'Chat':");
        foreach (var method in gmType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
        {
            if (method.Name.Contains("Chat"))
            {
                var parameters = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name));
                Console.WriteLine($"  {method.Name}({parameters})");
            }
        }

        Console.WriteLine("[CHRANIBotTNG] Loaded");
    }
}