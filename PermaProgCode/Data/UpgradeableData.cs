using PermaProg.PermaProgCode.Model;

namespace PermaProg.PermaProgCode.Data;

public class UpgradeableData
{
    public int TotalCurrentLevels;

    public readonly Dictionary<UpgradeableModel, string> All = new();
    public readonly UpgradeableModel StartGold = new();
    public readonly UpgradeableModel CurrencyGain = new();
    public readonly UpgradeableModel MaxHealth = new();
    public readonly UpgradeableModel CardUpgrades = new();
    public readonly UpgradeableModel CurrencyInterest = new();
    public readonly UpgradeableModel GoldGain = new();
    public readonly UpgradeableModel BlockGain = new();

    public UpgradeableData()
    {
        All.Add(StartGold, nameof(StartGold));
        All.Add(CurrencyGain, nameof(CurrencyGain));
        All.Add(MaxHealth, nameof(MaxHealth));
        All.Add(CardUpgrades, nameof(CardUpgrades));
        All.Add(CurrencyInterest, nameof(CurrencyInterest));
        All.Add(GoldGain, nameof(GoldGain));
        All.Add(BlockGain, nameof(BlockGain));

        foreach (var upg in All)
        {
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

        MF.Log.Info("Running sanity checks");
        foreach (var upg in All.Keys.Where(upg =>
                     upg.Vals.Count != upg.MaxLevel + 1 || upg.UpgCosts.Count != upg.MaxLevel))
        {
            MF.Log.Info($"This one is invalid -> {upg.CurrentLevelName}");
            MF.Log.Info($"MaxLevel: {upg.MaxLevel} Vals: {upg.Vals.Count} UpgCosts: {upg.UpgCosts.Count}");
            throw new InvalidOperationException();
        }

        MF.Log.Info("Sanity checks OK");
    }
}