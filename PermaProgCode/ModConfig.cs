using PermaProg.PermaProgCode.Model;
using PermaProg.PermaProgCode.Data;
using System.Reflection;
using Godot.Collections;
using BaseLib.Config.UI;
using BaseLib.Config;
using Godot;

namespace PermaProg.PermaProgCode;

internal class PP : SimpleModConfig
{
    private static Control? _optionContainer;
    [ConfigIgnore] public static UpgradeableData Upgrades { get; } = new();
    [ConfigIgnore] public static int CurrencyToGain { get; set; }
    [ConfigHideInUI] public static int TotalCurrencyGainedDuringRun { get; set; }
    [ConfigHideInUI] public static int CurrencyAvailable { get; set; }
    public static string CurrencyText { get; set; } = "0";
    public static string CurrencyGainedLastRunText { get; set; } = "0";
    public static bool BalancingEnabled { get; set; } = true;

    public override void SetupConfigUI(Control optionContainer)
    {
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

    private void Tier2Upgrades(Control optionContainer)
    {
        if (Upgrades.TotalCurrentLevels < 5)
        {
            optionContainer.AddChild(CreateSectionHeader("..some beings... ..are yet to... ..be revealed..."));
            optionContainer.AddChild(CreateSectionHeader("???"));
#if DEBUG
            Upgrades.CurrencyInterest.Unlocked = false;
            Upgrades.GoldGain.Unlocked = false;
            Upgrades.CardUpgrades.Unlocked = false;
            Upgrades.BlockGain.Unlocked = false;
#endif
        }
        else
        {
            optionContainer.AddChild(CreateSectionHeader("Tier 2 upgrades"));
            CreateUpgradeableUi(Upgrades.CurrencyInterest, UpgradeButtonCurrencyInterest, true);
            CreateUpgradeableUi(Upgrades.GoldGain, UpgradeButtonGoldGain, true);
            CreateUpgradeableUi(Upgrades.CardUpgrades, UpgradeButtonCardUpgrades);
        }
    }

    private void Tier3Upgrades(Control optionContainer)
    {
        switch (Upgrades.TotalCurrentLevels)
        {
            case < 5:
                break;
            case < 10:
                optionContainer.AddChild(CreateSectionHeader("..you have... ..done well... ..so far..."));
                optionContainer.AddChild(CreateSectionHeader("???"));
                break;
            default:
                optionContainer.AddChild(CreateSectionHeader("Tier 3 upgrades"));
                CreateUpgradeableUi(Upgrades.BlockGain, UpgradeButtonBlockGain);
                break;
        }
    }

    private void Tier4Upgrades(Control optionContainer)
    {
        switch (Upgrades.TotalCurrentLevels)
        {
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

    public void UpgradeButtonStartGold()
    {
        if (IsLevelUpSuccessful(Upgrades.StartGold)) StartGoldLevel++;
        UpdateUi();
    }

    public void UpgradeButtonCurrencyGain()
    {
        if (IsLevelUpSuccessful(Upgrades.CurrencyGain)) CurrencyGainLevel++;
        UpdateUi();
    }

    public void UpgradeButtonMaxHealth()
    {
        if (IsLevelUpSuccessful(Upgrades.MaxHealth)) MaxHealthLevel++;
        UpdateUi();
    }

    public void UpgradeButtonCardUpgrades()
    {
        if (IsLevelUpSuccessful(Upgrades.CardUpgrades)) CardUpgradesLevel++;
        UpdateUi();
    }

    public void UpgradeButtonCurrencyInterest()
    {
        if (IsLevelUpSuccessful(Upgrades.CurrencyInterest)) CurrencyInterestLevel++;
        UpdateUi();
    }

    public void UpgradeButtonGoldGain()
    {
        if (IsLevelUpSuccessful(Upgrades.GoldGain)) GoldGainLevel++;
        UpdateUi();
    }

    public void UpgradeButtonBlockGain()
    {
        if (IsLevelUpSuccessful(Upgrades.BlockGain)) BlockGainLevel++;
        UpdateUi();
    }

#if DEBUG
    public void AddGold5000()
    {
        CurrencyAvailable += 5000;
        UpdateUi();
    }
#endif
    private void UpdateUi()
    {
        UpdateCurrentValues();
        UpdateLineEdits();
        UpdateSliders();
        UpdateButtons();
    }

    private void UpdateCurrentValues()
    {
        var totalCurrentLevels = 0;
        foreach (var upg in Upgrades.All.Keys)
        {
            if (!upg.Unlocked) continue;
            var propertyInfo = GetPropertyInfo(upg.CurrentLevelName);
            upg.CurrentLevel = (int)(propertyInfo.GetValue(Upgrades) ?? throw new InvalidOperationException());
            totalCurrentLevels += upg.CurrentLevel;
        }

        Upgrades.TotalCurrentLevels = totalCurrentLevels;
    }

    private static void UpdateLineEdits()
    {
        CurrencyText = CurrencyAvailable.ToString();
        var headerRow = _optionContainer?.GetNode<NConfigOptionRow>("CurrencyText");
        if (headerRow?.SettingControl is NConfigLineEdit header) header.Text = CurrencyText;

        var headerRow2 = _optionContainer?.GetNode<NConfigOptionRow>("CurrencyGainedLastRunText");
        if (headerRow2?.SettingControl is NConfigLineEdit header2) header2.Text = CurrencyGainedLastRunText;
    }

    private void UpdateSliders()
    {
        foreach (var upg in Upgrades.All.Keys)
        {
            if (!upg.Unlocked) continue;
            var sliderRow = _optionContainer?.GetNode<NConfigOptionRow>(upg.SliderName);
            if (sliderRow?.SettingControl is not NConfigSlider slider) return;

            IsArraySafe(upg, upg.Vals);
            var maxSliderValue = upg.Vals[upg.CurrentLevel];
            if (maxSliderValue <= 0)
            {
                slider.Visible = false;
            }
            else
            {
                slider.SetRange(0, maxSliderValue);
                slider.Visible = true;
            }
        }
    }

    private void UpdateButtons()
    {
        foreach (var upg in Upgrades.All.Keys)
        {
            if (!upg.Unlocked) continue;
            var buttonRow = _optionContainer?.GetNode<NConfigOptionRow>(upg.ButtonName);
            if (buttonRow?.SettingControl is not NConfigButton button) return;

            if (upg.CurrentLevel >= upg.MaxLevel)
            {
                (button.GetChild(1) as Label)!.Text = "Maxed out!";
                continue;
            }

            IsArraySafe(upg, upg.UpgCosts);
            (button.GetChild(1) as Label)!.Text =
                upg.UpgCosts[upg.CurrentLevel] <= 0 ? "Free!" : upg.UpgCosts[upg.CurrentLevel].ToString();
        }
    }

    private bool IsLevelUpSuccessful(UpgradeableModel upg)
    {
        if (upg.CurrentLevel >= upg.MaxLevel) return false;
        if (!IsArraySafe(upg, upg.UpgCosts)) return false;
        if (upg.UpgCosts[upg.CurrentLevel] > CurrencyAvailable) return false;

        CurrencyAvailable -= upg.UpgCosts[upg.CurrentLevel];
        upg.CurrentLevel++;
        return true;
    }

    private void CreateLineEdit(string name, int fontSize, bool isEditable = false)
    {
        var propertyInfo = GetPropertyInfo(name);
        var headerRow = CreateLineEditOption(propertyInfo);
        if (headerRow.SettingControl is NConfigLineEdit header)
        {
            header.AddThemeFontSizeOverride("font_size", fontSize);
            header.Editable = isEditable;
        }

        _optionContainer?.AddChild(headerRow);
    }

    private void CreateUpgradeableUi(UpgradeableModel upg, Action onPressed, bool addHoverTip = false)
    {
        var slider = CreateSliderOption(GetPropertyInfo(upg.SliderName));
        if (addHoverTip) slider.AddHoverTip();
        _optionContainer?.AddChild(slider);
        _optionContainer?.AddChild(CreateButton(upg.ButtonName, "Default text", onPressed));
        _optionContainer?.AddChild(CreateDividerControl());
        upg.Unlocked = true;
    }

    private PropertyInfo GetPropertyInfo(string name)
    {
        var propertyInfo = ConfigProperties.Find(x => x.Name == name);
        return propertyInfo ?? throw new InvalidOperationException();
    }

    private bool IsArraySafe(UpgradeableModel upg, Array<int> upgArray)
    {
        if (upg.CurrentLevel <= upgArray.Count - 1 || upg.CurrentLevel == 0) return true;

        MF.Log.Error($"{upg.CurrentLevelName}: Current level ({upg.CurrentLevel}) is higher than values " +
                     $"available ({upgArray.Count - 1}). Lowering value to max level available. Bugs may occur");
        upg.CurrentLevel = upgArray.Count - 1;
        GetPropertyInfo(upg.CurrentLevelName).SetValue(Upgrades, upg.CurrentLevel);
        return false;
    }
}