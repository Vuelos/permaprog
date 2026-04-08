using BaseLib.Config;
using BaseLib.Config.UI;
using Godot;
using Godot.Collections;
using HarmonyLib;
using MegaCrit.Sts2.Core.Achievements;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Runs;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace PermaProg.PermaProgCode;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node {
   public const string ModId = "PermaProg"; //Used for resource filepath

   public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
      new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

   public static double CurrencyGained { get; set; }

   public static UpgDataContainer Upgrades { get; } = new();

   public static void Initialize() {
      ModConfigRegistry.Register(ModId, new MyModConfig());
      Harmony harmony = new(ModId);
      harmony.PatchAll();
   }
}

[HarmonyPatch(typeof(Player), "PopulateStartingInventory")]
public static class ApplyDataAtStartOfRun {
   [HarmonyPostfix]
   // ReSharper disable once InconsistentNaming
   public static void Postfix(Player __instance) {
      __instance.Gold += (int)MyModConfig.CurrentValueStartGold;
      __instance.Creature.SetMaxHpInternal(__instance.Creature.MaxHp + (int)MyModConfig.CurrentValueMaxHealth);
   }
}

/// There is 100% a variable available for this... I haven't found it yet though.
/// (CurrentMapPointHistoryEntry.GoldGained only gave me latest gold received, not total)
[HarmonyPatch(typeof(PlayerCmd), "GainGold")]
public static class IncrementCurrencyGained {
   [HarmonyPrefix]
   public static void Prefix(decimal amount, Player player, bool wasStolenBack = false) {
      MainFile.CurrencyGained += (double)amount * (1 + MyModConfig.CurrentValueCurrencyGain / 100);
   }
}

[HarmonyPatch(typeof(AchievementsHelper), "AfterRunEnded")]
public static class SaveDataAtEndOfRun {
   [HarmonyPrefix]
   public static void Prefix(RunState state, Player player, bool isVictory) {
      MyModConfig.CurrencyAvailable += (int)MainFile.CurrencyGained;
      MainFile.CurrencyGained = 0.0;
      ModConfig.SaveDebounced<MyModConfig>();
   }
}

internal class MyModConfig : SimpleModConfig {
   private static Control? _optionContainer;
   public static int CurrencyAvailable { get; set; }
   public static string CurrencyText { get; set; } = "0";

   //UI GENERATION//////////////////////////////////////////////////////////////////////////////////////////////////////
   public override void SetupConfigUI(Control optionContainer) {
      _optionContainer = optionContainer;
      AddRestoreDefaultsButton(_optionContainer);

      CreateCurrencyHeader();
      _optionContainer.AddChild(CreateButton("Add currency (debug)", "+500", Currency500));
      _optionContainer.AddChild(CreateDividerControl());
      _optionContainer.AddChild(CreateSectionHeader("Tier 1 upgrades"));

      CreateSlider(MainFile.Upgrades.StartGold);
      _optionContainer.AddChild(CreateButton(nameof(UpgradeButtonStartGold), "Cost", UpgradeButtonStartGold));
      _optionContainer.AddChild(CreateDividerControl());

      CreateSlider(MainFile.Upgrades.CurrencyGain);
      _optionContainer.AddChild(CreateButton(nameof(UpgradeButtonCurrencyGain), "Cost", UpgradeButtonCurrencyGain));
      _optionContainer.AddChild(CreateDividerControl());
      
      // First UpdateUi is necessary for logic (need up-to-date values). The rest refresh after new elements are added.
      UpdateUi();
      Tier2Upgrades(optionContainer);
      UpdateUi(); 
      Tier3Upgrades(optionContainer);
      UpdateUi();
   }

   private void Tier2Upgrades(Control optionContainer) {
      if (MainFile.Upgrades.TotalCurrentLevels < 5) {
         optionContainer.AddChild(CreateSectionHeader("..some beings... ..are yet to... ..be revealed..."));
         optionContainer.AddChild(CreateSectionHeader("???"));
      }
      else {
         optionContainer.AddChild(CreateSectionHeader("Tier 2 upgrades"));
         CreateSlider(MainFile.Upgrades.MaxHealth);
         optionContainer.AddChild(CreateButton(nameof(UpgradeButtonMaxHealth), "Cost", UpgradeButtonMaxHealth));
         optionContainer.AddChild(CreateDividerControl());
      }
   }

   private void Tier3Upgrades(Control optionContainer) {
      switch (MainFile.Upgrades.TotalCurrentLevels) {
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
   //END OF UI GENERATION///////////////////////////////////////////////////////////////////////////////////////////////

   //SLIDERS////////////////////////////////////////////////////////////////////////////////////////////////////////////
   public static int CurrentLevelStartGold { get; set; }
   [SliderRange(0.0, 1000.0)] public static double CurrentValueStartGold { get; set; }

   public static int CurrentLevelCurrencyGain { get; set; }
   [SliderRange(0.0, 1000.0)] public static double CurrentValueCurrencyGain { get; set; }

   public static int CurrentLevelMaxHealth { get; set; }
   [SliderRange(0.0, 1000.0)] public static double CurrentValueMaxHealth { get; set; }
   //END OF SLIDERS/////////////////////////////////////////////////////////////////////////////////////////////////////

   //BUTTONS////////////////////////////////////////////////////////////////////////////////////////////////////////////
   public static void UpgradeButtonStartGold() {
      if (UpgradeButtonPressed(MainFile.Upgrades.StartGold)) CurrentLevelStartGold++;
      UpdateUi();
   }

   public static void UpgradeButtonCurrencyGain() {
      if (UpgradeButtonPressed(MainFile.Upgrades.CurrencyGain)) CurrentLevelCurrencyGain++;
      UpdateUi();
   }

   public static void UpgradeButtonMaxHealth() {
      if (UpgradeButtonPressed(MainFile.Upgrades.MaxHealth)) CurrentLevelMaxHealth++;
      UpdateUi();
   }

   private static void Currency500() {
      CurrencyAvailable += 500;
      UpdateUi();
   }
   //END OF BUTTONS/////////////////////////////////////////////////////////////////////////////////////////////////////

   //HELPER FUNCTIONS///////////////////////////////////////////////////////////////////////////////////////////////////
   private static bool UpgradeButtonPressed(Upgradeable upg) {
      if (upg.CurrentLevel > upg.MaxLevel - 1) return false;
      if (upg.UpgCosts[upg.CurrentLevel] > CurrencyAvailable) return false;

      CurrencyAvailable -= upg.UpgCosts[upg.CurrentLevel];
      return true;
   }

   private static void UpdateUi() {
      UpdateCurrentValues();
      UpdateCurrencyHeader();
      UpdateSliders();
      UpdateButtons();
   }

   private static void UpdateCurrentValues() {
      Array<int> currentLevels = [CurrentLevelStartGold, CurrentLevelCurrencyGain, CurrentLevelMaxHealth];
      var totalCurrentLevels = 0;
      for (var i = 0; i < currentLevels.Count; i++) {
         MainFile.Upgrades.All[i].CurrentLevel = currentLevels[i];
         totalCurrentLevels += MainFile.Upgrades.All[i].CurrentLevel;
      }

      MainFile.Upgrades.TotalCurrentLevels = totalCurrentLevels;
   }

   private static void UpdateCurrencyHeader() {
      var headerRow = _optionContainer?.GetNode<NConfigOptionRow>("CurrencyText");
      if (headerRow?.SettingControl is NConfigLineEdit header) header.Text = CurrencyAvailable.ToString();
      CurrencyText = CurrencyAvailable.ToString();
   }

   private static void UpdateSliders() {
      foreach (var upg in MainFile.Upgrades.All) {
         var sliderRow = _optionContainer?.GetNode<NConfigOptionRow>(upg.SliderName);
         if (sliderRow?.SettingControl is not NConfigSlider slider) return;

         var maxSliderVal = upg.Vals[upg.CurrentLevel];
         if (maxSliderVal <= 0) {
            slider.Visible = false;
         }
         else {
            slider.SetRange(0, maxSliderVal);
            slider.Visible = true;
         }
      }
   }

   private static void UpdateButtons() {
      foreach (var upg in MainFile.Upgrades.All) {
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

   private void CreateCurrencyHeader() {
      var propertyInfo = ConfigProperties.Find(x => x.Name == "CurrencyText");
      if (propertyInfo == null) return;

      var headerRow = CreateLineEditOption(propertyInfo);
      if (headerRow.SettingControl is NConfigLineEdit header) {
         header.AddThemeFontSizeOverride("font_size", 50);
         header.Editable = false;
      }

      _optionContainer?.AddChild(headerRow);
   }

   private void CreateSlider(Upgradeable upg) {
      var tmpPropertyInfo = ConfigProperties.Find(x => x.Name == upg.SliderName);
      if (tmpPropertyInfo != null) _optionContainer?.AddChild(CreateSliderOption(tmpPropertyInfo));
   }
   //END OF HELPER FUNCTIONS////////////////////////////////////////////////////////////////////////////////////////////
}

//UPGRADE DATA//////////////////////////////////////////////////////////////////////////////////////////////////////////
public class Upgradeable {
   public string SliderName = "";
   public string ButtonName = "";
   public int MaxLevel;
   public Array<int> Vals = [];
   public Array<int> UpgCosts = [];

   public int CurrentLevel;
}

public class UpgDataContainer {
   public int TotalCurrentLevels;

   public readonly List<Upgradeable> All;
   public readonly Upgradeable StartGold = new();
   public readonly Upgradeable CurrencyGain = new();
   public readonly Upgradeable MaxHealth = new();

   public UpgDataContainer() {
      {
         StartGold.MaxLevel = 5;
         StartGold.Vals = [0, 10, 20, 30, 40, 50];
         StartGold.UpgCosts = [0, 100, 300, 500, 700];
      }

      {
         CurrencyGain.MaxLevel = 5;
         CurrencyGain.Vals = [0, 10, 20, 30, 40, 50];
         CurrencyGain.UpgCosts = [100, 300, 500, 700, 900];
      }

      {
         MaxHealth.MaxLevel = 5;
         MaxHealth.Vals = [0, 1, 2, 3, 4, 5];
         MaxHealth.UpgCosts = [100, 300, 500, 700, 900];
      }

      All = [StartGold, CurrencyGain, MaxHealth];
      SetNames();
   }

   private void SetNames() {
      StartGold.SliderName = nameof(MyModConfig.CurrentValueStartGold);
      StartGold.ButtonName = nameof(MyModConfig.UpgradeButtonStartGold);

      CurrencyGain.SliderName = nameof(MyModConfig.CurrentValueCurrencyGain);
      CurrencyGain.ButtonName = nameof(MyModConfig.UpgradeButtonCurrencyGain);

      MaxHealth.SliderName = nameof(MyModConfig.CurrentValueMaxHealth);
      MaxHealth.ButtonName = nameof(MyModConfig.UpgradeButtonMaxHealth);
   }
}
//END OF UPGRADE DATA///////////////////////////////////////////////////////////////////////////////////////////////////