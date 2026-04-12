using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Entities.Relics;
using PermaProg.PermaProgCode.Extensions;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using BaseLib.Extensions;
using BaseLib.Abstracts;
using BaseLib.Utils;
using Godot;

namespace PermaProg.PermaProgCode.Relics;

[Pool(typeof(SharedRelicPool))]
// ReSharper disable once ClassNeverInstantiated.Global
public sealed class PpRelic : CustomRelicModel {
  private bool ShouldTrigger { get; set; }

  public override RelicRarity Rarity => RelicRarity.Starter;

  protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(1M, ValueProp.Unpowered)];

  public override Task BeforeTurnEndVeryEarly(PlayerChoiceContext choiceContext, CombatSide side) {
    if (side != Owner.Creature.Side)
      return Task.CompletedTask;
    ShouldTrigger = true;
    return Task.CompletedTask;
  }

  public override async Task BeforeTurnEnd(PlayerChoiceContext choiceContext, CombatSide side) {
    if (!ShouldTrigger)
      return;
    ShouldTrigger = false;
    if (PermaProg.BlockGainValue > 0) {
      Flash();
      var blockAmount = new BlockVar((decimal)PermaProg.BlockGainValue, ValueProp.Unpowered);
      await CreatureCmd.GainBlock(Owner.Creature, blockAmount, null);
    }
  }

  public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
    CombatState combatState) {
    ShouldTrigger = false;
    return Task.CompletedTask;
  }

  //PermaProg/images/relics
  public override string PackedIconPath {
    get {
      var path = $"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".RelicImagePath();
      return ResourceLoader.Exists(path) ? path : "relic.png".RelicImagePath();
    }
  }

  protected override string PackedIconOutlinePath {
    get {
      var path = $"{Id.Entry.RemovePrefix().ToLowerInvariant()}_outline.png".RelicImagePath();
      return ResourceLoader.Exists(path) ? path : "relic_outline.png".RelicImagePath();
    }
  }

  protected override string BigIconPath {
    get {
      var path = $"{Id.Entry.RemovePrefix().ToLowerInvariant()}.png".BigRelicImagePath();
      return ResourceLoader.Exists(path) ? path : "relic.png".BigRelicImagePath();
    }
  }
}