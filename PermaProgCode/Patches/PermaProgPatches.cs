using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Achievements;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Odds;
using MegaCrit.Sts2.Core.Runs;
using BaseLib.Config;
using HarmonyLib;
using Godot;

namespace PermaProg.PermaProgCode.Patches;

[HarmonyPatch]
public static class PermaProgPatches
{
    [HarmonyPatch(typeof(GoldReward), MethodType.Constructor, [typeof(int), typeof(int), typeof(Player), typeof(bool)])]
    [HarmonyPrefix]
    public static void IncreaseGoldRewardDuringRun(ref int min, ref int max, Player player)
    {
        var balancingMultiplier = PP.BalancingEnabled ? 0.8 : 1.0;
        min = (int)Math.Round(min * balancingMultiplier * (1.0 + PP.GoldGainValue / 100.0));
        max = (int)Math.Round(max * balancingMultiplier * (1.0 + PP.GoldGainValue / 100.0));
    }

    [HarmonyPatch(typeof(CardRarityOdds), "GetBaseOdds")]
    [HarmonyPostfix]
    public static void IncreaseCardRarityOdds(ref float __result, CardRarityOddsType type, CardRarity rarity)
    {
        if (PP.CardRarityValue <= 0.1) return;
        MF.Log.Info($"Boosting card rarity odds by {(int)PP.CardRarityValue}%");
        __result *= 1.0f + (float)PP.CardRarityValue / 100.0f;
    }

    [HarmonyPatch(typeof(PlayerCmd), "GainGold")]
    [HarmonyPrefix]
    public static void GainCurrencyDuringRun(decimal amount, Player player, bool wasStolenBack)
    {
        var currencyGained = (double)amount * (1.0 + PP.CurrencyGainValue / 100.0);
        MF.Log.Info($"Currency to gain: {(int)currencyGained} from {amount} gold " +
                    $"with multiplier {1.0 + PP.CurrencyGainValue / 100.0}.");
        PP.CurrencyToGain = (int)currencyGained;
    }

    [HarmonyPatch(typeof(SaveManager), "SaveRun")]
    [HarmonyPostfix]
    public static void IncrementTotalCurrencyGained(AbstractRoom? preFinishedRoom, bool saveProgress)
    {
        if (PP.CurrencyToGain <= 0) return;
        MF.Log.Info($"Add currency reward ({PP.CurrencyToGain}) to total currency gained");
        PP.TotalCurrencyGainedDuringRun += PP.CurrencyToGain;
        MF.Log.Info($"Total currency gained during run: {PP.TotalCurrencyGainedDuringRun}.");
        PP.CurrencyToGain = 0;
        ModConfig.SaveDebounced<PP>();
    }

    [HarmonyPatch(typeof(AchievementsHelper), "AfterRunEnded")]
    [HarmonyPrefix]
    public static void SaveDataAtEndOfRun(RunState state, Player player, bool isVictory)
    {
        if (state.CurrentActIndex >= 2)
        {
            var interest = (double)PP.CurrencyAvailable;
            interest *= PP.CurrencyInterestValue / 100.0;
            MF.Log.Info($"Gain {(int)interest} in interest");
            PP.CurrencyAvailable += (int)interest;
        }

        if (PP.CurrencyToGain > 0)
        {
            MF.Log.Info($"Add last currency reward ({PP.CurrencyToGain}) to total currency gained");
            PP.TotalCurrencyGainedDuringRun += PP.CurrencyToGain;
            PP.CurrencyToGain = 0;
        }

        MF.Log.Info($"Run ended. Adding {PP.TotalCurrencyGainedDuringRun} to available currency");
        PP.CurrencyGainedLastRunText = PP.TotalCurrencyGainedDuringRun.ToString();
        PP.CurrencyAvailable += PP.TotalCurrencyGainedDuringRun;
        ModConfig.SaveDebounced<PP>();
    }

    [HarmonyPatch(typeof(NGameOverScreen), "AddBadge")]
    [HarmonyPrefix]
    public static void UpdateBadgeInfo(string locEntryKey, string? locAmountKey, ref int amount, string? iconPath)
    {
        if (locEntryKey != "BADGE.goldGained") return;
        MF.Log.Info($"Exchange end-of-run gold ({amount}) to currency gained ({PP.TotalCurrencyGainedDuringRun})");
        amount = PP.TotalCurrencyGainedDuringRun;
    }

    [HarmonyPatch(typeof(NBadge), "Create")]
    [HarmonyPrefix]
    public static void CreateBadge(ref string label, Texture2D? icon)
    {
        if (!label.Contains("Gold")) return;
        MF.Log.Info("Exchange end-of-run 'Gold gained' reward badge to 'Currency Gained'");
        label = label.Replace("Gold", "Currency");
    }
}