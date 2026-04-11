using System.Reflection;
using BaseLib.Config;
using BaseLib.Config.UI;
using Godot;
using Godot.Collections;
using HarmonyLib;
using MegaCrit.Sts2.Core.Achievements;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace PermaProg.PermaProgCode;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node {
  public const string ModId = "PermaProg"; //Used for resource filepath

  public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
    new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

  public static void Initialize() {
    ModConfigRegistry.Register(ModId, new PermaProg());
    Harmony harmony = new(ModId);
    harmony.PatchAll();
  }
}

[HarmonyPatch(typeof(Player), "PopulateStartingInventory")]
public static class ApplyDataAtStartOfRun {
  // ReSharper disable once InconsistentNaming
  public static void Postfix(Player __instance) {
    if (PermaProg.BalancingEnabled) {
      __instance.Gold -= 99;
      if (__instance.Gold < 0) __instance.Gold = 0;
      __instance.Creature.SetMaxHpInternal(__instance.Creature.MaxHp - 20);
    }

    __instance.Gold += (int)PermaProg.StartGoldValue;
    __instance.Creature.SetMaxHpInternal(__instance.Creature.MaxHp + (int)PermaProg.MaxHealthValue);
    UpgradeCards(__instance);
  }

  private static void UpgradeCards(Player instance) {
    var cards = instance.Deck.Cards;
    var cardsToUpgrade = RandomlySelectedCards(cards, (int)PermaProg.CardUpgradesValue, cards.Count);
    foreach (var card in cardsToUpgrade) {
      if (!card.IsUpgradable) continue;
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
}

[HarmonyPatch(typeof(GoldReward))]
[HarmonyPatch(MethodType.Constructor)]
[HarmonyPatch([typeof(int), typeof(int), typeof(Player), typeof(bool)])]
public static class IncreaseGoldRewardDuringRun {
  public static void Prefix(ref int min, ref int max, Player player) {
    var balancingMultiplier = PermaProg.BalancingEnabled ? 0.8 : 1.0;
    min = (int)Math.Round(min * balancingMultiplier * (1 + PermaProg.GoldGainValue / 100));
    max = (int)Math.Round(max * balancingMultiplier * (1 + PermaProg.GoldGainValue / 100));
  }
}

/// There is 100% a variable available for this... I haven't found it yet though
[HarmonyPatch(typeof(PlayerCmd), "GainGold")]
public static class IncreaseCurrencyGained {
  public static void Prefix(decimal amount, Player player, bool wasStolenBack = false) {
    PermaProg.CurrencyGained += (double)amount * (1 + PermaProg.CurrencyGainValue / 100);
  }
}

[HarmonyPatch(typeof(AchievementsHelper), "AfterRunEnded")]
public static class SaveDataAtEndOfRun {
  public static void Prefix(RunState state, Player player, bool isVictory) {
    PermaProg.CurrencyAvailable = (int)(PermaProg.CurrencyAvailable * (1 + PermaProg.CurrencyInterestValue / 100));
    PermaProg.CurrencyAvailable += (int)PermaProg.CurrencyGained;
    PermaProg.CurrencyGained = 0.0;
    ModConfig.SaveDebounced<PermaProg>();
  }
}

internal class PermaProg : SimpleModConfig {
  private static Control? _optionContainer;
  [ConfigIgnore] public static double CurrencyGained { get; set; }
  [ConfigIgnore] public static UpgDataContainer Upgrades { get; } = new();
  [ConfigHideInUI] public static int CurrencyAvailable { get; set; }
  public static string CurrencyText { get; set; } = "0";
  public static bool BalancingEnabled { get; set; } = true;

  //UI GENERATION///////////////////////////////////////////////////////////////////////////////////////////////////////
  public override void SetupConfigUI(Control optionContainer) {
    _optionContainer = optionContainer;
    AddRestoreDefaultsButton(_optionContainer);

    _optionContainer.AddChild(CreateToggleOption(GetPropertyInfo(nameof(BalancingEnabled))));
    CreateCurrencyHeader();
    _optionContainer.AddChild(CreateButton("Add currency (debug)", "+5000", Currency5000));
    _optionContainer.AddChild(CreateDividerControl());

    _optionContainer.AddChild(CreateSectionHeader("Tier 1 upgrades"));
    CreateUpgradeableUi(Upgrades.StartGold, UpgradeButtonStartGold);
    CreateUpgradeableUi(Upgrades.CurrencyGain, UpgradeButtonCurrencyGain);
    CreateUpgradeableUi(Upgrades.MaxHealth, UpgradeButtonMaxHealth);

    UpdateCurrentValues();
    Tier2Upgrades(optionContainer);
    Tier3Upgrades(optionContainer);
    UpdateUi();
  }

  private void Tier2Upgrades(Control optionContainer) {
    if (Upgrades.TotalCurrentLevels < 5) {
      optionContainer.AddChild(CreateSectionHeader("..some beings... ..are yet to... ..be revealed..."));
      optionContainer.AddChild(CreateSectionHeader("???"));

      /* These are temporarily(?) necessary as the restore defaults button triggers an error log */
      /* when: 1) tier 2 enabled 2) restore defaults 3) leave and re-enter settings menu. */
      Upgrades.CurrencyInterest.Unlocked = false;
      Upgrades.GoldGain.Unlocked = false;
      Upgrades.CardUpgrades.Unlocked = false;
    }
    else {
      optionContainer.AddChild(CreateSectionHeader("Tier 2 upgrades"));
      CreateUpgradeableUi(Upgrades.CurrencyInterest, UpgradeButtonCurrencyInterest);
      CreateUpgradeableUi(Upgrades.GoldGain, UpgradeButtonGoldGain);
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
        optionContainer.AddChild(CreateSectionHeader("..some beings... ..are yet to... ..be created..."));
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

  private void Currency5000() {
    CurrencyAvailable += 5000;
    UpdateUi();
  }
  //END OF BUTTONS//////////////////////////////////////////////////////////////////////////////////////////////////////

  //HELPER FUNCTIONS////////////////////////////////////////////////////////////////////////////////////////////////////
  private void UpdateUi() {
    UpdateCurrentValues();
    UpdateCurrencyHeader();
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

  private static void UpdateCurrencyHeader() {
    var headerRow = _optionContainer?.GetNode<NConfigOptionRow>("CurrencyText");
    if (headerRow?.SettingControl is NConfigLineEdit header) header.Text = CurrencyAvailable.ToString();
    CurrencyText = CurrencyAvailable.ToString();
  }

  private static void UpdateSliders() {
    foreach (var upg in Upgrades.All.Keys) {
      if (!upg.Unlocked) continue;
      var sliderRow = _optionContainer?.GetNode<NConfigOptionRow>(upg.SliderName);
      if (sliderRow?.SettingControl is not NConfigSlider slider) return;

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

  private static void UpdateButtons() {
    foreach (var upg in Upgrades.All.Keys) {
      if (!upg.Unlocked) continue;
      var buttonRow = _optionContainer?.GetNode<NConfigOptionRow>(upg.ButtonName);
      if (buttonRow?.SettingControl is not NConfigButton button) return;

      if (upg.CurrentLevel >= upg.MaxLevel) {
        (button.GetChild(1) as Label)!.Text = "Maxed out!";
      }
      else if (upg.UpgCosts[upg.CurrentLevel] <= 0) {
        (button.GetChild(1) as Label)!.Text = "Free!";
      }
      else {
        (button.GetChild(1) as Label)!.Text = upg.UpgCosts[upg.CurrentLevel].ToString();
      }
    }
  }

  private static bool IsLevelUpSuccessful(Upgradeable upg) {
    if (upg.CurrentLevel >= upg.MaxLevel) return false;
    if (upg.UpgCosts[upg.CurrentLevel] > CurrencyAvailable) return false;

    CurrencyAvailable -= upg.UpgCosts[upg.CurrentLevel];
    upg.CurrentLevel++;
    return true;
  }

  private void CreateCurrencyHeader() {
    var propertyInfo = GetPropertyInfo(nameof(CurrencyText));
    var headerRow = CreateLineEditOption(propertyInfo);
    if (headerRow.SettingControl is NConfigLineEdit header) {
      header.AddThemeFontSizeOverride("font_size", 50);
      header.Editable = false;
    }

    _optionContainer?.AddChild(headerRow);
  }

  private void CreateUpgradeableUi(Upgradeable upg, Action onPressed) {
    _optionContainer?.AddChild(CreateSliderOption(GetPropertyInfo(upg.SliderName)));
    _optionContainer?.AddChild(CreateButton(upg.ButtonName, "Default text", onPressed));
    _optionContainer?.AddChild(CreateDividerControl());
    upg.Unlocked = true;
  }

  private PropertyInfo GetPropertyInfo(string name) {
    var propertyInfo = ConfigProperties.Find(x => x.Name == name);
    return propertyInfo ?? throw new InvalidOperationException();
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

  public UpgDataContainer() {
    All.Add(StartGold, nameof(StartGold));
    All.Add(CurrencyGain, nameof(CurrencyGain));
    All.Add(MaxHealth, nameof(MaxHealth));
    All.Add(CardUpgrades, nameof(CardUpgrades));
    All.Add(CurrencyInterest, nameof(CurrencyInterest));
    All.Add(GoldGain, nameof(GoldGain));

    foreach (var upg in All) {
      upg.Key.SliderName = upg.Value + "Value";
      upg.Key.ButtonName = "UpgradeButton" + upg.Value;
      upg.Key.CurrentLevelName = upg.Value + "Level";
    }

    {
      StartGold.MaxLevel = 20;
      StartGold.Vals =
        [0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120, 130, 140, 150, 160, 170, 180, 190, 200];
      StartGold.UpgCosts = [
        0, 100, 200, 300, 400, 500, 600, 700, 800, 900, 1000, 1100, 1200, 1300, 1400, 1500, 1600, 1700, 1800, 1900
      ];
    }

    {
      CurrencyGain.MaxLevel = 20;
      CurrencyGain.Vals =
        [0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120, 130, 140, 150, 160, 170, 180, 190, 200];
      CurrencyGain.UpgCosts = [
        100, 200, 300, 400, 500, 600, 700, 800, 900, 1000, 1100, 1200, 1300, 1400, 1500, 1600, 1700, 1800, 1900, 2000
      ];
    }

    {
      MaxHealth.MaxLevel = 20;
      MaxHealth.Vals = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20];
      MaxHealth.UpgCosts = [
        100, 200, 300, 400, 500, 600, 700, 800, 900, 1000, 1100, 1200, 1300, 1400, 1500, 1600, 1700, 1800, 1900, 2000
      ];
    }

    {
      CardUpgrades.MaxLevel = 10;
      CardUpgrades.Vals = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
      CardUpgrades.UpgCosts = [1000, 1200, 1400, 1600, 1800, 2000, 2500, 3000, 3500, 4000];
    }

    {
      CurrencyInterest.MaxLevel = 20;
      CurrencyInterest.Vals = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20];
      CurrencyInterest.UpgCosts = [
        100, 200, 300, 400, 500, 600, 700, 800, 900, 1000, 1100, 1200, 1300, 1400, 1500, 1600, 1700, 1800, 1900, 2000
      ];
    }

    {
      GoldGain.MaxLevel = 19;
      GoldGain.Vals = [0, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70, 75, 80, 90, 95, 100];
      GoldGain.UpgCosts = [
        100, 200, 300, 400, 500, 600, 700, 800, 900, 1000, 1100, 1200, 1300, 1400, 1500, 1600, 1700, 1800, 1900
      ];
    }

    // Sanity check
    foreach (var upg in All.Keys.Where(upg =>
               upg.Vals.Count != upg.MaxLevel + 1 || upg.UpgCosts.Count != upg.MaxLevel)) {
      GD.Print("This one :( -> ", upg.CurrentLevelName);
      GD.Print("MaxLevel, Vals, UpgCosts: " + upg.MaxLevel + " ", +upg.Vals.Count + " ", +upg.UpgCosts.Count);
      throw new InvalidOperationException();
    }
  }
}
//END OF UPGRADE DATA///////////////////////////////////////////////////////////////////////////////////////////////////