using MegaCrit.Sts2.Core.Models;
using System.Reflection;
using HarmonyLib;

namespace PermaProg.PermaProgCode.Patches;

[HarmonyPatch]
public static class SetStartingHpPatch
{
    private static void SetHp(ref int __result)
    {
        if (PP.BalancingEnabled)
            __result = (int)(__result * 0.8);

        __result += (int)PP.MaxHealthValue;
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
            .Select(t => t.GetProperty("StartingHp")?.GetGetMethod(true))
            .Where(m => m != null)
            .ToArray();
    }

    public static void Postfix(ref int __result)
    {
        SetHp(ref __result);
    }
}