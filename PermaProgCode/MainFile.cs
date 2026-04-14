using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Achievements;
using PermaProg.PermaProgCode.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Odds;
using MegaCrit.Sts2.Core.Runs;
using BaseLib.Config.UI;
using Godot.Collections;
using System.Reflection;
using BaseLib.Config;
using HarmonyLib;
using Godot;

namespace PermaProg.PermaProgCode;

// MF: MainFile
// ReSharper disable once ClassNeverInstantiated.Global
[ModInitializer(nameof(Initialize))]
public partial class MF : Node {
  public const string ModId = "PermaProg"; //Used for resource filepath
  public const string ResPath = $"res://{ModId}";

  public static MegaCrit.Sts2.Core.Logging.Logger Log { get; } =
    new(ModId, LogType.Generic);

  public static void Initialize() {
    ModConfigRegistry.Register(ModId, new PP());
    Harmony harmony = new(ModId);
    harmony.PatchAll();
  }
}

[HarmonyPatch]
public static class PermaProgPatches {
  [HarmonyPatch(typeof(Player), "PopulateStartingInventory")]
  [HarmonyPrefix]
  public static void StartNewRun(Player __instance) {
    MF.Log.Info("Starting new run");

    PP.TotalCurrencyGainedDuringRun = 0;
    ModConfig.SaveDebounced<PP>();
  }

  [HarmonyPatch(typeof(Player), "PopulateStartingDeck")]
  [HarmonyPostfix]
  public static void UpgradeCards(Player __instance) {
    var cards = __instance.Deck.Cards;
    var cardsToUpgrade = RandomlySelectedCards(cards, (int)PP.CardUpgradesValue, cards.Count);
    var cardModels = cardsToUpgrade.ToList();

    MF.Log.Info($"Upgrading {cardModels.Count} cards");
    foreach (var card in cardModels.Where(card => card.IsUpgradable)) {
      card.UpgradeInternal();
      card.FinalizeUpgradeInternal();
    }
  }

  // Ty Matthew Watson on StackOverflow
  public static IEnumerable<T> RandomlySelectedCards<T>(IEnumerable<T> sequence, int count, int sequenceLength) {
    var rng = new Random();
    var available = sequenceLength;
    var remaining = count;

    using var iterator = sequence.GetEnumerator();
    for (var current = 0; current < sequenceLength; ++current) {
      iterator.MoveNext();
      if (rng.NextDouble() < remaining / (double)available) {
        yield return iterator.Current;
        --remaining;
      }

      --available;
    }
  }

  [HarmonyPatch(typeof(Player), "PopulateStartingRelics")]
  [HarmonyPostfix]
  public static void AddPpRelic(Player __instance) {
    MF.Log.Info("Adding Peapod relic");

    var ppRelic = ModelDb.Relic<PpRelic>().ToMutable();
    ppRelic.FloorAddedToDeck = 1;
    __instance.AddRelicInternal(ppRelic, silent: true);
  }

  [HarmonyPatch(typeof(GoldReward), MethodType.Constructor, [typeof(int), typeof(int), typeof(Player), typeof(bool)])]
  [HarmonyPrefix]
  public static void IncreaseGoldRewardDuringRun(ref int min, ref int max, Player player) {
    var balancingMultiplier = PP.BalancingEnabled ? 0.8 : 1.0;
    min = (int)Math.Round(min * balancingMultiplier * (1.0 + PP.GoldGainValue / 100.0));
    max = (int)Math.Round(max * balancingMultiplier * (1.0 + PP.GoldGainValue / 100.0));
  }

  [HarmonyPatch(typeof(CardRarityOdds), "GetBaseOdds")]
  [HarmonyPostfix]
  public static void IncreaseCardRarityOdds(ref float __result, CardRarityOddsType type, CardRarity rarity) {
    if (PP.CardRarityValue <= 0.1) return;
    MF.Log.Info($"Boosting card rarity odds by {(int)PP.CardRarityValue}%");
    __result *= 1.0f + (float)PP.CardRarityValue / 100.0f;
  }

  [HarmonyPatch(typeof(PlayerCmd), "GainGold")]
  [HarmonyPrefix]
  public static void GainCurrencyDuringRun(decimal amount, Player player, bool wasStolenBack) {
    var currencyGained = (double)amount * (1.0 + PP.CurrencyGainValue / 100.0);
    MF.Log.Info(
      $"Currency to gain: {(int)currencyGained} from {amount} gold with multiplier {1.0 + PP.CurrencyGainValue / 100.0}.");
    PP.CurrencyToGain = (int)currencyGained;
  }

  [HarmonyPatch(typeof(SaveManager), "SaveRun")]
  [HarmonyPostfix]
  public static void IncrementTotalCurrencyGained(AbstractRoom? preFinishedRoom, bool saveProgress) {
    if (PP.CurrencyToGain <= 0) return;
    MF.Log.Info($"Add currency reward ({PP.CurrencyToGain}) to total currency gained");
    PP.TotalCurrencyGainedDuringRun += PP.CurrencyToGain;
    MF.Log.Info($"Total currency gained during run: {PP.TotalCurrencyGainedDuringRun}.");
    PP.CurrencyToGain = 0;
    ModConfig.SaveDebounced<PP>();
  }

  [HarmonyPatch(typeof(AchievementsHelper), "AfterRunEnded")]
  [HarmonyPrefix]
  public static void SaveDataAtEndOfRun(RunState state, Player player, bool isVictory) {
    if (state.CurrentActIndex >= 2) {
      var interest = (int)(PP.CurrencyAvailable * (1.0 + PP.CurrencyInterestValue / 100.0) - PP.CurrencyAvailable);
      MF.Log.Info($"Gain {interest} in interest");
      PP.CurrencyAvailable += interest;
    }

    MF.Log.Info($"Run ended. Adding {PP.TotalCurrencyGainedDuringRun} to available currency");
    PP.CurrencyGainedLastRunText = PP.TotalCurrencyGainedDuringRun.ToString();
    PP.CurrencyAvailable += PP.TotalCurrencyGainedDuringRun;
    ModConfig.SaveDebounced<PP>();
  }

  [HarmonyPatch(typeof(NGameOverScreen), "AddBadge")]
  [HarmonyPrefix]
  public static void UpdateBadgeInfo(string locEntryKey, string? locAmountKey, ref int amount, string? iconPath) {
    if (locEntryKey != "BADGE.goldGained") return;
    MF.Log.Info($"Exchange end-of-run gold ({amount}) to currency gained ({PP.TotalCurrencyGainedDuringRun})");
    amount = PP.TotalCurrencyGainedDuringRun;
  }

  [HarmonyPatch(typeof(NBadge), "Create")]
  [HarmonyPrefix]
  public static void CreateBadge(ref string label, Texture2D? icon) {
    if (!label.Contains("Gold")) return;
    MF.Log.Info("Exchange end-of-run 'Gold gained' reward badge to 'Currency Gained'");
    label = label.Replace("Gold", "Currency");
  }
}

// PP: PermaProg
internal class PP : SimpleModConfig {
  private static Control? _optionContainer;
  [ConfigIgnore] public static UpgDataContainer Upgrades { get; } = new();
  [ConfigIgnore] public static int CurrencyToGain { get; set; }
  [ConfigHideInUI] public static int TotalCurrencyGainedDuringRun { get; set; }
  [ConfigHideInUI] public static int CurrencyAvailable { get; set; }
  public static string CurrencyText { get; set; } = "0";
  public static string CurrencyGainedLastRunText { get; set; } = "0";
  public static bool BalancingEnabled { get; set; } = true;

  //UI GENERATION///////////////////////////////////////////////////////////////////////////////////////////////////////
  public override void SetupConfigUI(Control optionContainer) {
    MF.Log.Info("Shop menu entered");
    _optionContainer = optionContainer;

#if DEBUG
    AddRestoreDefaultsButton(_optionContainer);
    _optionContainer.AddChild(CreateButton("Add gold (debug)", "+5000", AddGold5000));
    _optionContainer.AddChild(CreateDividerControl());
#endif

    _optionContainer.AddChild(CreateToggleOption(GetPropertyInfo(nameof(BalancingEnabled))));
    CreateLineEdit(nameof(CurrencyGainedLastRunText), 20);
    CreateLineEdit(nameof(CurrencyText), 50);
    _optionContainer.AddChild(CreateDividerControl());

    _optionContainer.AddChild(CreateSectionHeader("Tier 1 upgrades"));
    CreateUpgradeableUi(Upgrades.StartGold, UpgradeButtonStartGold);
    CreateUpgradeableUi(Upgrades.CurrencyGain, UpgradeButtonCurrencyGain);
    CreateUpgradeableUi(Upgrades.MaxHealth, UpgradeButtonMaxHealth);

    UpdateCurrentValues();
    Tier2Upgrades(optionContainer);
    Tier3Upgrades(optionContainer);
    Tier4Upgrades(optionContainer);
    UpdateUi();
  }

  private void Tier2Upgrades(Control optionContainer) {
    if (Upgrades.TotalCurrentLevels < 5) {
      optionContainer.AddChild(CreateSectionHeader("..some beings... ..are yet to... ..be revealed..."));
      optionContainer.AddChild(CreateSectionHeader("???"));
#if DEBUG
      Upgrades.CurrencyInterest.Unlocked = false;
      Upgrades.GoldGain.Unlocked = false;
      Upgrades.CardUpgrades.Unlocked = false;
      Upgrades.BlockGain.Unlocked = false;
#endif
    }
    else {
      optionContainer.AddChild(CreateSectionHeader("Tier 2 upgrades"));
      CreateUpgradeableUi(Upgrades.CurrencyInterest, UpgradeButtonCurrencyInterest, true);
      CreateUpgradeableUi(Upgrades.GoldGain, UpgradeButtonGoldGain, true);
      CreateUpgradeableUi(Upgrades.CardUpgrades, UpgradeButtonCardUpgrades);
    }
  }

  private void Tier3Upgrades(Control optionContainer) {
    switch (Upgrades.TotalCurrentLevels) {
      case < 5:
        break;
      case < 10:
        optionContainer.AddChild(CreateSectionHeader("..you have... ..done well... ..so far..."));
        optionContainer.AddChild(CreateSectionHeader("???"));
        break;
      default:
        optionContainer.AddChild(CreateSectionHeader("Tier 3 upgrades"));
        CreateUpgradeableUi(Upgrades.BlockGain, UpgradeButtonBlockGain);
        CreateUpgradeableUi(Upgrades.CardRarity, UpgradeButtonCardRarity, true);
        break;
    }
  }

  private void Tier4Upgrades(Control optionContainer) {
    switch (Upgrades.TotalCurrentLevels) {
      case < 10:
        break;
      case < 20:
        optionContainer.AddChild(CreateSectionHeader("..the journey... ..shall continue... ..with effort..."));
        optionContainer.AddChild(CreateSectionHeader("???"));
        break;
      default:
        optionContainer.AddChild(CreateSectionHeader("Tier 4 upgrades"));
        optionContainer.AddChild(CreateSectionHeader("..some beings... ..are yet to... ..be created..."));
        optionContainer.AddChild(CreateSectionHeader("(end of beta content)"));
        break;
    }
  }
  //END OF UI GENERATION////////////////////////////////////////////////////////////////////////////////////////////////

  //SLIDERS/////////////////////////////////////////////////////////////////////////////////////////////////////////////
  public static int StartGoldLevel { get; set; }
  [SliderRange(0.0, 1000.0)] public static double StartGoldValue { get; set; }

  public static int CurrencyGainLevel { get; set; }
  [SliderRange(0.0, 1000.0)] public static double CurrencyGainValue { get; set; }

  public static int MaxHealthLevel { get; set; }
  [SliderRange(0.0, 1000.0)] public static double MaxHealthValue { get; set; }

  public static int CardUpgradesLevel { get; set; }
  [SliderRange(0.0, 1000.0)] public static double CardUpgradesValue { get; set; }

  public static int CurrencyInterestLevel { get; set; }
  [SliderRange(0.0, 1000.0)] public static double CurrencyInterestValue { get; set; }

  public static int GoldGainLevel { get; set; }
  [SliderRange(0.0, 1000.0)] public static double GoldGainValue { get; set; }

  public static int BlockGainLevel { get; set; }
  [SliderRange(0.0, 1000.0)] public static double BlockGainValue { get; set; }

  public static int CardRarityLevel { get; set; }
  [SliderRange(0.0, 1000.0)] public static double CardRarityValue { get; set; }
  //END OF SLIDERS//////////////////////////////////////////////////////////////////////////////////////////////////////

  //BUTTONS/////////////////////////////////////////////////////////////////////////////////////////////////////////////
  public void UpgradeButtonStartGold() {
    if (IsLevelUpSuccessful(Upgrades.StartGold)) StartGoldLevel++;
    UpdateUi();
  }

  public void UpgradeButtonCurrencyGain() {
    if (IsLevelUpSuccessful(Upgrades.CurrencyGain)) CurrencyGainLevel++;
    UpdateUi();
  }

  public void UpgradeButtonMaxHealth() {
    if (IsLevelUpSuccessful(Upgrades.MaxHealth)) MaxHealthLevel++;
    UpdateUi();
  }

  public void UpgradeButtonCardUpgrades() {
    if (IsLevelUpSuccessful(Upgrades.CardUpgrades)) CardUpgradesLevel++;
    UpdateUi();
  }

  public void UpgradeButtonCurrencyInterest() {
    if (IsLevelUpSuccessful(Upgrades.CurrencyInterest)) CurrencyInterestLevel++;
    UpdateUi();
  }

  public void UpgradeButtonGoldGain() {
    if (IsLevelUpSuccessful(Upgrades.GoldGain)) GoldGainLevel++;
    UpdateUi();
  }

  public void UpgradeButtonBlockGain() {
    if (IsLevelUpSuccessful(Upgrades.BlockGain)) BlockGainLevel++;
    UpdateUi();
  }

  public void UpgradeButtonCardRarity() {
    if (IsLevelUpSuccessful(Upgrades.CardRarity)) CardRarityLevel++;
    UpdateUi();
  }

#if DEBUG
  public void AddGold5000() {
    CurrencyAvailable += 5000;
    UpdateUi();
  }
#endif
  //END OF BUTTONS//////////////////////////////////////////////////////////////////////////////////////////////////////

  //HELPER FUNCTIONS////////////////////////////////////////////////////////////////////////////////////////////////////
  private void UpdateUi() {
    UpdateCurrentValues();
    UpdateLineEdits();
    UpdateSliders();
    UpdateButtons();
  }

  private void UpdateCurrentValues() {
    var totalCurrentLevels = 0;
    foreach (var upg in Upgrades.All.Keys) {
      if (!upg.Unlocked) continue;
      var propertyInfo = GetPropertyInfo(upg.CurrentLevelName);
      upg.CurrentLevel = (int)(propertyInfo.GetValue(Upgrades) ?? throw new InvalidOperationException());
      totalCurrentLevels += upg.CurrentLevel;
    }

    Upgrades.TotalCurrentLevels = totalCurrentLevels;
  }

  private static void UpdateLineEdits() {
    CurrencyText = CurrencyAvailable.ToString();
    var headerRow = _optionContainer?.GetNode<NConfigOptionRow>("CurrencyText");
    if (headerRow?.SettingControl is NConfigLineEdit header) header.Text = CurrencyText;

    var headerRow2 = _optionContainer?.GetNode<NConfigOptionRow>("CurrencyGainedLastRunText");
    if (headerRow2?.SettingControl is NConfigLineEdit header2) header2.Text = CurrencyGainedLastRunText;
  }

  private void UpdateSliders() {
    foreach (var upg in Upgrades.All.Keys) {
      if (!upg.Unlocked) continue;
      var sliderRow = _optionContainer?.GetNode<NConfigOptionRow>(upg.SliderName);
      if (sliderRow?.SettingControl is not NConfigSlider slider) return;

      IsArraySafe(upg, upg.Vals);
      var maxSliderValue = upg.Vals[upg.CurrentLevel];
      if (maxSliderValue <= 0) {
        slider.Visible = false;
      }
      else {
        slider.SetRange(0, maxSliderValue);
        slider.Visible = true;
      }
    }
  }

  private void UpdateButtons() {
    foreach (var upg in Upgrades.All.Keys) {
      if (!upg.Unlocked) continue;
      var buttonRow = _optionContainer?.GetNode<NConfigOptionRow>(upg.ButtonName);
      if (buttonRow?.SettingControl is not NConfigButton button) return;

      if (upg.CurrentLevel >= upg.MaxLevel) {
        (button.GetChild(1) as Label)!.Text = "Maxed out!";
        continue;
      }

      IsArraySafe(upg, upg.UpgCosts);
      (button.GetChild(1) as Label)!.Text =
        upg.UpgCosts[upg.CurrentLevel] <= 0 ? "Free!" : upg.UpgCosts[upg.CurrentLevel].ToString();
    }
  }

  private bool IsLevelUpSuccessful(Upgradeable upg) {
    if (upg.CurrentLevel >= upg.MaxLevel) return false;
    if (!IsArraySafe(upg, upg.UpgCosts)) return false;
    if (upg.UpgCosts[upg.CurrentLevel] > CurrencyAvailable) return false;

    CurrencyAvailable -= upg.UpgCosts[upg.CurrentLevel];
    upg.CurrentLevel++;
    return true;
  }

  private void CreateLineEdit(string name, int fontSize, bool isEditable = false) {
    var propertyInfo = GetPropertyInfo(name);
    var headerRow = CreateLineEditOption(propertyInfo);
    if (headerRow.SettingControl is NConfigLineEdit header) {
      header.AddThemeFontSizeOverride("font_size", fontSize);
      header.Editable = isEditable;
    }

    _optionContainer?.AddChild(headerRow);
  }

  private void CreateUpgradeableUi(Upgradeable upg, Action onPressed, bool addHoverTip = false) {
    var slider = CreateSliderOption(GetPropertyInfo(upg.SliderName));
    if (addHoverTip) slider.AddHoverTip();
    _optionContainer?.AddChild(slider);
    _optionContainer?.AddChild(CreateButton(upg.ButtonName, "Default text", onPressed));
    _optionContainer?.AddChild(CreateDividerControl());
    upg.Unlocked = true;
  }

  private PropertyInfo GetPropertyInfo(string name) {
    var propertyInfo = ConfigProperties.Find(x => x.Name == name);
    return propertyInfo ?? throw new InvalidOperationException();
  }

  private bool IsArraySafe(Upgradeable upg, Array<int> upgArray) {
    if (upg.CurrentLevel <= upgArray.Count - 1 || upg.CurrentLevel == 0) return true;

    MF.Log.Error($"{upg.CurrentLevelName}: Current level ({upg.CurrentLevel}) is higher than values " +
                 $"available ({upgArray.Count - 1}). Lowering value to max level available. Bugs may occur");
    upg.CurrentLevel = upgArray.Count - 1;
    GetPropertyInfo(upg.CurrentLevelName).SetValue(Upgrades, upg.CurrentLevel);
    return false;
  }

  //END OF HELPER FUNCTIONS/////////////////////////////////////////////////////////////////////////////////////////////
}

//UPGRADE DATA//////////////////////////////////////////////////////////////////////////////////////////////////////////
public class Upgradeable {
  public string SliderName = "";
  public string ButtonName = "";
  public string CurrentLevelName = "";

  public int MaxLevel;
  public Array<int> Vals = [];
  public Array<int> UpgCosts = [];

  public int CurrentLevel;
  public bool Unlocked;
}

public class UpgDataContainer {
  public int TotalCurrentLevels;

  public readonly System.Collections.Generic.Dictionary<Upgradeable, string> All = new();
  public readonly Upgradeable StartGold = new();
  public readonly Upgradeable CurrencyGain = new();
  public readonly Upgradeable MaxHealth = new();
  public readonly Upgradeable CardUpgrades = new();
  public readonly Upgradeable CurrencyInterest = new();
  public readonly Upgradeable GoldGain = new();
  public readonly Upgradeable BlockGain = new();
  public readonly Upgradeable CardRarity = new();

  public UpgDataContainer() {
    All.Add(StartGold, nameof(StartGold));
    All.Add(CurrencyGain, nameof(CurrencyGain));
    All.Add(MaxHealth, nameof(MaxHealth));
    All.Add(CardUpgrades, nameof(CardUpgrades));
    All.Add(CurrencyInterest, nameof(CurrencyInterest));
    All.Add(GoldGain, nameof(GoldGain));
    All.Add(BlockGain, nameof(BlockGain));
    All.Add(CardRarity, nameof(CardRarity));

    foreach (var upg in All) {
      upg.Key.SliderName = upg.Value + "Value";
      upg.Key.ButtonName = "UpgradeButton" + upg.Value;
      upg.Key.CurrentLevelName = upg.Value + "Level";
    }

    {
      StartGold.MaxLevel = 10;
      StartGold.Vals = [0, 20, 40, 60, 80, 100, 120, 140, 160, 180, 200];
      StartGold.UpgCosts = [0, 200, 400, 600, 800, 1000, 1200, 1400, 1600, 1800];
    }

    {
      CurrencyGain.MaxLevel = 10;
      CurrencyGain.Vals = [0, 30, 60, 90, 120, 150, 180, 210, 240, 270, 300];
      CurrencyGain.UpgCosts = [100, 300, 600, 900, 1200, 1500, 1800, 2100, 2400, 2700];
    }

    {
      MaxHealth.MaxLevel = 10;
      MaxHealth.Vals = [0, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20];
      MaxHealth.UpgCosts = [150, 500, 850, 1200, 1550, 1900, 2250, 2600, 2950, 3300];
    }

    {
      CardUpgrades.MaxLevel = 10;
      CardUpgrades.Vals = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
      CardUpgrades.UpgCosts = [1000, 1400, 1800, 2200, 2700, 3200, 3700, 4200, 4700, 5000];
    }

    {
      CurrencyInterest.MaxLevel = 5;
      CurrencyInterest.Vals = [0, 4, 8, 12, 16, 20];
      CurrencyInterest.UpgCosts = [500, 1000, 1500, 2000, 2500];
    }

    {
      GoldGain.MaxLevel = 10;
      GoldGain.Vals = [0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100];
      GoldGain.UpgCosts = [100, 300, 600, 900, 1200, 1500, 1800, 2100, 2400, 2700];
    }

    {
      BlockGain.MaxLevel = 3;
      BlockGain.Vals = [0, 1, 2, 3];
      BlockGain.UpgCosts = [5000, 15000, 45000];
    }

    {
      CardRarity.MaxLevel = 5;
      CardRarity.Vals = [0, 20, 40, 60, 80, 100];
      CardRarity.UpgCosts = [5000, 10000, 15000, 20000, 25000];
    }

    MF.Log.Info("Running sanity checks");
    foreach (var upg in All.Keys.Where(upg =>
               upg.Vals.Count != upg.MaxLevel + 1 || upg.UpgCosts.Count != upg.MaxLevel)) {
      MF.Log.Info($"This one is invalid -> {upg.CurrentLevelName}");
      MF.Log.Info($"MaxLevel: {upg.MaxLevel} Vals: {upg.Vals.Count} UpgCosts: {upg.UpgCosts.Count}");
      throw new InvalidOperationException();
    }

    MF.Log.Info("Sanity checks OK");
  }
}
//END OF UPGRADE DATA///////////////////////////////////////////////////////////////////////////////////////////////////

/* These will be placed here until I figure out a more efficient way to write this code */
/* Should use the 'all characters list' which might give support for custom characters */
[HarmonyPatch]
public static class SetStartingHp {
  private static void SetHp(ref int __result) {
    if (PP.BalancingEnabled) __result = (int)(__result * 0.8);
    __result += (int)PP.MaxHealthValue;
  }

  [HarmonyPatch(typeof(Ironclad), "StartingHp", MethodType.Getter)]
  [HarmonyPostfix]
  public static void Fun1(ref int __result) {
    SetHp(ref __result);
  }

  [HarmonyPatch(typeof(Silent), "StartingHp", MethodType.Getter)]
  [HarmonyPostfix]
  public static void Fun2(ref int __result) {
    SetHp(ref __result);
  }

  [HarmonyPatch(typeof(Regent), "StartingHp", MethodType.Getter)]
  [HarmonyPostfix]
  public static void Fun3(ref int __result) {
    SetHp(ref __result);
  }

  [HarmonyPatch(typeof(Necrobinder), "StartingHp", MethodType.Getter)]
  [HarmonyPostfix]
  public static void Fun4(ref int __result) {
    SetHp(ref __result);
  }

  [HarmonyPatch(typeof(Defect), "StartingHp", MethodType.Getter)]
  [HarmonyPostfix]
  public static void Fun5(ref int __result) {
    SetHp(ref __result);
  }
}

[HarmonyPatch]
public static class SetStartingGold {
  private static void SetGold(ref int __result) {
    if (PP.BalancingEnabled) __result = 0;
    __result += (int)PP.StartGoldValue;
  }

  [HarmonyPatch(typeof(Ironclad), "StartingGold", MethodType.Getter)]
  [HarmonyPostfix]
  public static void Fun1(ref int __result) {
    SetGold(ref __result);
  }

  [HarmonyPatch(typeof(Silent), "StartingGold", MethodType.Getter)]
  [HarmonyPostfix]
  public static void Fun2(ref int __result) {
    SetGold(ref __result);
  }

  [HarmonyPatch(typeof(Regent), "StartingGold", MethodType.Getter)]
  [HarmonyPostfix]
  public static void Fun3(ref int __result) {
    SetGold(ref __result);
  }

  [HarmonyPatch(typeof(Necrobinder), "StartingGold", MethodType.Getter)]
  [HarmonyPostfix]
  public static void Fun4(ref int __result) {
    SetGold(ref __result);
  }

  [HarmonyPatch(typeof(Defect), "StartingGold", MethodType.Getter)]
  [HarmonyPostfix]
  public static void Fun5(ref int __result) {
    SetGold(ref __result);
  }
}