using MegaCrit.Sts2.Core.Models;
using System.Reflection;
using HarmonyLib;

namespace PermaProg.PermaProgCode.Patches;

[HarmonyPatch]
public static class SetStartingGold
{
    private static void SetGold(ref int __result)
    {
        if (PP.BalancingEnabled)
            __result = 0;

        __result += (int)PP.StartGoldValue;
    }

    public static MethodInfo?[] TargetMethods()
    {
        var baseType = typeof(CharacterModel);

        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch { return []; }
            })
            .Where(t => baseType.IsAssignableFrom(t) && !t.IsAbstract)
            .Select(t => t.GetProperty("StartingGold")?.GetGetMethod(true))
            .Where(m => m != null)
            .ToArray();
    }

    public static void Postfix(ref int __result)
    {
        SetGold(ref __result);
    }
}