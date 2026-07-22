using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.UI;
using BannerlordTwitch.Util;
using BLTAdoptAHero;
using BLTAdoptAHero.Annotations;
using BLTAdoptAHero.Actions.Upgrades;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;
using BannerlordTwitch.Rewards;
using System.ComponentModel;
using System.Collections.ObjectModel;

namespace BLTAdoptAHero.Actions
{
    [LocDisplayName("{=BLT_UpgradeCmd}Upgrade"),
     LocDescription("{=BLT_UpgradeCmdDesc}Purchase upgrades for fiefs, clans, or kingdoms"),
     UsedImplicitly]
    public class UpgradeAction : HeroCommandHandlerBase
    {
        [CategoryOrder("General", 0),
         CategoryOrder("Permissions", 1)]
        public class Settings : IDocumentable
        {
            [LocDisplayName("{=BLT_UpgradeEnabled}Enabled"),
             LocCategory("General", "{=GeneralCat}General"),
             LocDescription("{=BLT_UpgradeEnabledDesc}Enable the upgrade system"),
             PropertyOrder(1), UsedImplicitly]
            public bool Enabled { get; set; } = true;

            [LocDisplayName("{=BLT_AllowList}Allow List Command"),
             LocCategory("General", "{=GeneralCat}General"),
             LocDescription("{=BLT_AllowListDesc}Allow players to list all available upgrades"),
             PropertyOrder(2), UsedImplicitly]
            public bool AllowListCommand { get; set; } = true;

            [LocDisplayName("{=BLT_AccumulateWhenFull}Accumulate Troops When Full"),
             LocCategory("General", "{=GeneralCat}General"),
             LocDescription("{=BLT_AccumulateWhenFullDesc}When enabled, troop spawn upgrades will reserve troops if all war parties/garrisons are full, releasing them all once space becomes available. If this is off, troops will simply be lost if all parties/garrisons are full."),
             PropertyOrder(3), UsedImplicitly, DefaultValue(true)]
            public bool AccumulateWhenFull { get; set; } = true;

            // Permissions
            [LocDisplayName("{=BLT_KingdomLeaderFiefs}Kingdom Leaders Can Upgrade Fiefs"),
             LocCategory("Permissions", "{=BLT_Permissions}Permissions"),
             LocDescription("{=BLT_KingdomLeaderFiefsDesc}Allow kingdom rulers to purchase fief upgrades for settlements in their kingdom"),
             PropertyOrder(1), UsedImplicitly]
            public bool AllowKingdomLeadersForFiefs { get; set; } = false;

            [LocDisplayName("{=BLT_AnyClanMember}Any Clan Member Can Upgrade Clan"),
             LocCategory("Permissions", "{=BLT_Permissions}Permissions"),
             LocDescription("{=BLT_AnyClanMemberDesc}Allow any clan member to purchase clan upgrades (not just the leader)"),
             PropertyOrder(2), UsedImplicitly]
            public bool AllowAnyClanMemberForClanUpgrades { get; set; } = false;

            [LocDisplayName("{=BLT_IndependentLord}Independent Clans Count as Lords"),
             LocCategory("Permissions", "{=BLT_Permissions}Permissions"),
             LocDescription("{=BLT_IndependentLordDesc}If enabled, clans that own fiefs but belong to no kingdom benefit from Lord Only upgrades. Default: true (preserves existing behaviour)."),
             PropertyOrder(3), UsedImplicitly, DefaultValue(true)]
            public bool IndependentClansCountAsLords { get; set; } = true;

            [LocDisplayName("{=BLT_IndependentMerc}Independent Clans Count as Mercenaries"),
             LocCategory("Permissions", "{=BLT_Permissions}Permissions"),
             LocDescription("{=BLT_IndependentMercDesc}If enabled, clans that own fiefs but belong to no kingdom also benefit from Mercenary Only upgrades."),
             PropertyOrder(4), UsedImplicitly, DefaultValue(false)]
            public bool IndependentClansCountAsMercs { get; set; } = false;


            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                string YesNo(bool value) => value ? "{=BLT_UpgradeYes}Yes".Translate() : "{=BLT_UpgradeNo}No".Translate();
                generator.P("{=BLT_UpgradeDocsEnabled}<strong>Enabled:</strong> {Value}".Translate(("Value", YesNo(Enabled))));
                generator.P("{=BLT_UpgradeDocsAllowList}<strong>Allow List Command:</strong> {Value}".Translate(("Value", YesNo(AllowListCommand))));
                generator.P("{=BLT_UpgradeDocsKingdomFiefs}<strong>Kingdom Leaders Can Upgrade Fiefs:</strong> {Value}".Translate(("Value", YesNo(AllowKingdomLeadersForFiefs))));
                generator.P("{=BLT_UpgradeDocsAnyClanMember}<strong>Any Clan Member Can Upgrade Clan:</strong> {Value}".Translate(("Value", YesNo(AllowAnyClanMemberForClanUpgrades))));
                generator.P("{=BLT_UpgradeDocsReserveTroops}<strong>Reserve Troops When Full:</strong> {Value}".Translate(("Value", YesNo(AccumulateWhenFull))));
                generator.P("{=BLT_UpgradeDocsIndependentLords}<strong>Independent Clans Count as Lords:</strong> {Value}".Translate(("Value", YesNo(IndependentClansCountAsLords))));
                generator.P("{=BLT_UpgradeDocsIndependentMercs}<strong>Independent Clans Count as Mercenaries:</strong> {Value}".Translate(("Value", YesNo(IndependentClansCountAsMercs))));
            }
        }

        public class UpgradeSystemDocumentation : IDocumentable
        {
            private readonly GlobalCommonConfig config;

            internal UpgradeSystemDocumentation(GlobalCommonConfig config = null) => this.config = config;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                generator.H1("{=BLT_UpgradeDocsSystem}Upgrade System".Translate());
                generator.P("{=BLT_UpgradeDocsOverview}This section contains all available upgrades organized by type and restrictions.".Translate());

                var source = config ?? GlobalCommonConfig.Get();
                if (source == null)
                {
                    generator.P("{=BLT_UpgradeConfigUnavailable}Configuration not available".Translate());
                    return;
                }

                GenerateUpgradeCounts(generator, source);
                GenerateUpgradesTables(generator, source);
            }

            private void GenerateUpgradeCounts(IDocumentationGenerator generator, GlobalCommonConfig config)
            {
                generator.H2("{=BLT_UpgradeDocsCounts}Upgrade Counts".Translate());
                generator.P("{=BLT_UpgradeDocsFiefCount}<strong>Fief Upgrades:</strong> {Count}".Translate(("Count", config.FiefUpgrades?.Count ?? 0)));
                generator.P("{=BLT_UpgradeDocsClanCount}<strong>Clan Upgrades:</strong> {Count}".Translate(("Count", config.ClanUpgrades?.Count ?? 0)));
                generator.P("{=BLT_UpgradeDocsKingdomCount}<strong>Kingdom Upgrades:</strong> {Count}".Translate(("Count", config.KingdomUpgrades?.Count ?? 0)));
            }

            private void GenerateUpgradesTables(IDocumentationGenerator generator, GlobalCommonConfig config)
            {
                if (config.FiefUpgrades != null && config.FiefUpgrades.Count > 0)
                {
                    var standard = config.FiefUpgrades.Where(u => !u.CoastalOnly).ToList();
                    var coastal = config.FiefUpgrades.Where(u => u.CoastalOnly).ToList();
                    generator.H2("{=BLT_UpgradeDocsFiefUpgrades}Fief Upgrades".Translate());
                    if (standard.Count > 0) { generator.H3("{=BLT_UpgradeDocsStandardFiefs}Standard Fief Upgrades".Translate()); GenerateFiefUpgradeTable(generator, standard); }
                    if (coastal.Count > 0) { generator.H3("{=BLT_UpgradeDocsCoastalFiefs}Coastal Only Fief Upgrades".Translate()); GenerateFiefUpgradeTable(generator, coastal); }
                }

                if (config.ClanUpgrades != null && config.ClanUpgrades.Count > 0)
                {
                    var std = config.ClanUpgrades.Where(u => (u.MercOnly && u.LordOnly && !u.ApplyToVassals) || (!u.MercOnly && !u.LordOnly && !u.ApplyToVassals)).ToList();
                    var lord = config.ClanUpgrades.Where(u => u.LordOnly && !u.MercOnly && !u.ApplyToVassals).ToList();
                    var merc = config.ClanUpgrades.Where(u => u.MercOnly && !u.LordOnly && !u.ApplyToVassals).ToList();
                    var vassal = config.ClanUpgrades.Where(u => u.ApplyToVassals).ToList();
                    generator.H2("{=BLT_UpgradeDocsClanUpgrades}Clan Upgrades".Translate());
                    if (std.Count > 0) { generator.H3("{=BLT_UpgradeDocsStandardClans}Standard Clan Upgrades".Translate()); GenerateClanUpgradeTable(generator, std); }
                    if (lord.Count > 0) { generator.H3("{=BLT_UpgradeDocsLordClans}Lord Only Clan Upgrades".Translate()); GenerateClanUpgradeTable(generator, lord); }
                    if (merc.Count > 0) { generator.H3("{=BLT_UpgradeDocsMercenaryClans}Mercenary Only Clan Upgrades".Translate()); GenerateClanUpgradeTable(generator, merc); }
                    if (vassal.Count > 0) { generator.H3("{=BLT_UpgradeDocsVassalClans}Vassal Only Clan Upgrades".Translate()); GenerateClanUpgradeTable(generator, vassal); }
                }

                if (config.KingdomUpgrades != null && config.KingdomUpgrades.Count > 0)
                {
                    generator.H2("{=BLT_UpgradeDocsKingdomUpgrades}Kingdom Upgrades".Translate());
                    GenerateKingdomUpgradeTable(generator, config.KingdomUpgrades.ToList());
                }
            }

            private void GenerateFiefUpgradeTable(IDocumentationGenerator generator, List<FiefUpgrade> upgrades)
            {
                generator.Table("upgrade-table", () =>
                {
                    generator.TR(() => { generator.TH("{=BLT_UpgradeDocsId}ID".Translate()); generator.TH("{=BLT_UpgradeDocsName}Name".Translate()); generator.TH("{=BLT_UpgradeDocsCost}Cost".Translate()); generator.TH("{=BLT_UpgradeDocsTier}Tier".Translate()); generator.TH("{=BLT_UpgradeDocsRequired}Required".Translate()); generator.TH("{=BLT_UpgradeDocsDescription}Description".Translate()); });
                    foreach (var u in upgrades)
                    {
                        generator.TR(() =>
                        {
                            generator.TD(u.ID);
                            generator.TD(u.Name);
                            generator.TD($"{u.GoldCost}{Naming.Gold}");
                            generator.TD(u.TierLevel > 0 ? u.TierLevel.ToString() : "-");
                            generator.TD(!string.IsNullOrEmpty(u.RequiredUpgradeID) ? u.RequiredUpgradeID : "-");
                            generator.TD(() =>
                            {
                                generator.P(u.Description);
                                if (ShouldShowFullDescription(u.ID))
                                    generator.Details(() => { generator.Summary("{=BLT_UpgradeDocsViewDetails}View Details".Translate()); var fx = GetUpgradeEffects(u); if (!string.IsNullOrEmpty(fx)) generator.P(fx); });
                            });
                        });
                    }
                });
            }

            private void GenerateClanUpgradeTable(IDocumentationGenerator generator, List<ClanUpgrade> upgrades)
            {
                generator.Table("upgrade-table", () =>
                {
                    generator.TR(() => { generator.TH("{=BLT_UpgradeDocsId}ID".Translate()); generator.TH("{=BLT_UpgradeDocsName}Name".Translate()); generator.TH("{=BLT_UpgradeDocsCost}Cost".Translate()); generator.TH("{=BLT_UpgradeDocsTier}Tier".Translate()); generator.TH("{=BLT_UpgradeDocsRequired}Required".Translate()); generator.TH("{=BLT_UpgradeDocsDescription}Description".Translate()); });
                    foreach (var u in upgrades)
                    {
                        generator.TR(() =>
                        {
                            generator.TD(u.ID);
                            generator.TD(u.Name);
                            generator.TD($"{u.GoldCost}{Naming.Gold}");
                            generator.TD(u.TierLevel > 0 ? u.TierLevel.ToString() : "-");
                            generator.TD(!string.IsNullOrEmpty(u.RequiredUpgradeID) ? u.RequiredUpgradeID : "-");
                            generator.TD(() =>
                            {
                                generator.P(u.Description);
                                if (ShouldShowFullDescription(u.ID))
                                    generator.Details(() => { generator.Summary("{=BLT_UpgradeDocsViewDetails}View Details".Translate()); var fx = GetUpgradeEffects(u); if (!string.IsNullOrEmpty(fx)) generator.P(fx); });
                            });
                        });
                    }
                });
            }

            private void GenerateKingdomUpgradeTable(IDocumentationGenerator generator, List<KingdomUpgrade> upgrades)
            {
                generator.Table("upgrade-table", () =>
                {
                    generator.TR(() => { generator.TH("{=BLT_UpgradeDocsId}ID".Translate()); generator.TH("{=BLT_UpgradeDocsName}Name".Translate()); generator.TH("{=BLT_UpgradeDocsCost}Cost".Translate()); generator.TH("{=BLT_UpgradeDocsTier}Tier".Translate()); generator.TH("{=BLT_UpgradeDocsRequired}Required".Translate()); generator.TH("{=BLT_UpgradeDocsDescription}Description".Translate()); });
                    foreach (var u in upgrades)
                    {
                        generator.TR(() =>
                        {
                            generator.TD(u.ID);
                            generator.TD(u.Name);
                            generator.TD(u.GetCostString());
                            generator.TD(u.TierLevel > 0 ? u.TierLevel.ToString() : "-");
                            generator.TD(!string.IsNullOrEmpty(u.RequiredUpgradeID) ? u.RequiredUpgradeID : "-");
                            generator.TD(() =>
                            {
                                generator.P(u.Description);
                                if (ShouldShowFullDescription(u.ID))
                                    generator.Details(() => { generator.Summary("{=BLT_UpgradeDocsViewDetails}View Details".Translate()); var fx = GetUpgradeEffects(u); if (!string.IsNullOrEmpty(fx)) generator.P(fx); });
                            });
                        });
                    }
                });
            }

            private string GetUpgradeEffects(FiefUpgrade u)
            {
                var sb = new StringBuilder();
                sb.AppendLine("{=BLT_UpgradeDocsEffects}<strong>Effects:</strong><br>".Translate());
                if (u.CanBeRemoved) sb.AppendLine("{=BLT_UpgradeDocsCanRemove}Can Be Removed<br>".Translate());
                AppendSettlementEffects(sb, u.LoyaltyDailyFlat, u.LoyaltyDailyPercent, u.ProsperityDailyFlat,
                    u.ProsperityDailyPercent, u.SecurityDailyFlat, u.SecurityDailyPercent, u.MilitiaDailyFlat,
                    u.MilitiaDailyPercent, u.FoodDailyFlat, u.FoodDailyPercent, u.TaxIncomeFlat,
                    u.TaxIncomePercent, u.GarrisonCapacityBonus, u.HearthDaily);
                return sb.Length > 0 ? sb.ToString() : "{=BLT_UpgradeDocsNoEffects}No effects configured".Translate();
            }

            private string GetUpgradeEffects(ClanUpgrade u)
            {
                var sb = new StringBuilder();
                if (u.RenownDaily != 0 || u.PartySizeBonus != 0 || u.PartySpeedBonus != 0 || u.PartyAmountBonus != 0 || u.MaxVassalsBonus != 0 || u.RetinueSizeBonus != 0 || u.ArmySpeedBonus != 0 || u.MercIncomeFlat != 0 || u.MercIncomePercent != 0)
                {
                    sb.AppendLine("{=BLT_UpgradeDocsClanEffects}<strong>Clan Effects:</strong><br>".Translate());
                    if (u.RenownDaily != 0) sb.AppendLine("{=BLT_UpgradeDocsRenownDaily}Renown: {Value}/day<br>".Translate(("Value", Signed(u.RenownDaily))));
                    if (u.InfluenceDaily != 0) sb.AppendLine("{=BLT_UpgradeDocsInfluenceDaily}Influence: {Value}/day<br>".Translate(("Value", Signed(u.InfluenceDaily))));
                    if (u.PartySizeBonus != 0) sb.AppendLine("{=BLT_UpgradeDocsPartySize}Party Size: {Value}<br>".Translate(("Value", Signed(u.PartySizeBonus))));
                    if (u.PartySpeedBonus != 0) sb.AppendLine("{=BLT_UpgradeDocsPartySpeed}Party Speed: {Value}<br>".Translate(("Value", Signed(u.PartySpeedBonus))));
                    if (u.PartyAmountBonus != 0) sb.AppendLine("{=BLT_UpgradeDocsPartyLimit}Party Limit: {Value}<br>".Translate(("Value", Signed(u.PartyAmountBonus))));
                    if (u.MaxVassalsBonus != 0) sb.AppendLine("{=BLT_UpgradeDocsVassalLimit}Vassal Limit: {Value}<br>".Translate(("Value", Signed(u.MaxVassalsBonus))));
                    if (u.RetinueSizeBonus != 0) sb.AppendLine("{=BLT_UpgradeDocsRetinueSize}Retinue Size: {Value}<br>".Translate(("Value", Signed(u.RetinueSizeBonus))));
                    if (u.ArmySpeedBonus != 0) sb.AppendLine("{=BLT_UpgradeDocsArmySpeed}Army Speed: {Value} (once/clan: {OncePerClan})<br>"
                        .Translate(("Value", Signed(u.ArmySpeedBonus)), ("OncePerClan", FormatYesNo(u.ArmySpeedOncePerClan))));
                    if (u.MercIncomeFlat != 0) sb.AppendLine("{=BLT_UpgradeDocsMercIncomeFlat}Merc Income (Flat): {Value}/day<br>".Translate(("Value", Signed(u.MercIncomeFlat))));
                    if (u.MercIncomePercent != 0) sb.AppendLine("{=BLT_UpgradeDocsMercIncomePercent}Merc Income (%): {Value}%/day<br>".Translate(("Value", Signed(u.MercIncomePercent))));
                }
                if (u.LoyaltyDailyFlat != 0 || u.LoyaltyDailyPercent != 0 || u.ProsperityDailyFlat != 0 || u.ProsperityDailyPercent != 0 ||
                    u.SecurityDailyFlat != 0 || u.SecurityDailyPercent != 0 || u.MilitiaDailyFlat != 0 || u.MilitiaDailyPercent != 0 ||
                    u.FoodDailyFlat != 0 || u.FoodDailyPercent != 0 || u.TaxIncomeFlat != 0 || u.TaxIncomePercent != 0 ||
                    u.GarrisonCapacityBonus != 0 || u.HearthDaily != 0)
                {
                    sb.AppendLine("{=BLT_UpgradeDocsSettlementEffects}<br><strong>Settlement Effects:</strong><br>".Translate());
                    AppendSettlementEffects(sb, u.LoyaltyDailyFlat, u.LoyaltyDailyPercent, u.ProsperityDailyFlat,
                        u.ProsperityDailyPercent, u.SecurityDailyFlat, u.SecurityDailyPercent, u.MilitiaDailyFlat,
                        u.MilitiaDailyPercent, u.FoodDailyFlat, u.FoodDailyPercent, u.TaxIncomeFlat,
                        u.TaxIncomePercent, u.GarrisonCapacityBonus, u.HearthDaily);
                }
                if (u.DailyTroopSpawnAmount > 0 || u.TroopTierBonus > 0)
                {
                    sb.AppendLine("{=BLT_UpgradeDocsTroopSpawning}<br><strong>Troop Spawning:</strong><br>".Translate());
                    if (u.DailyTroopSpawnAmount > 0)
                    {
                        sb.AppendLine("{=BLT_UpgradeDocsDailySpawn}Daily Spawn: {Amount} troops/day<br>".Translate(("Amount", u.DailyTroopSpawnAmount)));
                        sb.AppendLine("{=BLT_UpgradeDocsTroopTree}Troop Tree: {Tree}<br>".Translate(("Tree", u.TroopTree)));
                        sb.AppendLine("{=BLT_UpgradeDocsBaseTier}Base Tier: {Tier}<br>".Translate(("Tier", u.TroopTier)));
                    }
                    if (u.TroopTierBonus > 0 && !string.IsNullOrEmpty(u.BuffsTroopTierOf))
                        sb.AppendLine("{=BLT_UpgradeDocsTierBonus}Tier Bonus: +{Bonus} to {Target}<br>".Translate(("Bonus", u.TroopTierBonus), ("Target", u.BuffsTroopTierOf)));
                }
                return sb.Length > 0 ? sb.ToString() : "{=BLT_UpgradeDocsNoEffects}No effects configured".Translate();
            }

            private string GetUpgradeEffects(KingdomUpgrade u)
            {
                var sb = new StringBuilder();
                if (u.InfluenceDaily != 0 || u.MaxClansBonus != 0 || u.MaxMercClansBonus != 0)
                {
                    sb.AppendLine("{=BLT_UpgradeDocsKingdomEffects}<strong>Kingdom Effects:</strong><br>".Translate());
                    if (u.InfluenceDaily != 0) sb.AppendLine("{=BLT_UpgradeDocsRulerInfluence}Influence: {Value}/day (ruler only)<br>".Translate(("Value", Signed(u.InfluenceDaily))));
                    if (u.MaxClansBonus != 0) sb.AppendLine("{=BLT_UpgradeDocsMaxClans}Max Clans: {Value}<br>".Translate(("Value", Signed(u.MaxClansBonus))));
                    if (u.MaxMercClansBonus != 0) sb.AppendLine("{=BLT_UpgradeDocsMaxMercClans}Max Merc Clans: {Value}<br>".Translate(("Value", Signed(u.MaxMercClansBonus))));
                }
                if (u.RenownDaily != 0 || u.PartySizeBonus != 0 || u.PartySpeedBonus != 0 || u.InfluenceDaily != 0 || u.RetinueSizeBonus != 0 || u.ArmySpeedBonus != 0)
                {
                    sb.AppendLine("{=BLT_UpgradeDocsAllClanEffects}<br><strong>Clan Effects (All Kingdom Clans):</strong><br>".Translate());
                    if (u.RenownDaily != 0) sb.AppendLine("{=BLT_UpgradeDocsRenownDaily}Renown: {Value}/day<br>".Translate(("Value", Signed(u.RenownDaily))));
                    if (u.PartySizeBonus != 0) sb.AppendLine("{=BLT_UpgradeDocsPartySize}Party Size: {Value}<br>".Translate(("Value", Signed(u.PartySizeBonus))));
                    if (u.PartySpeedBonus != 0) sb.AppendLine("{=BLT_UpgradeDocsPartySpeed}Party Speed: {Value}<br>".Translate(("Value", Signed(u.PartySpeedBonus))));
                    if (u.InfluenceDaily != 0) sb.AppendLine("{=BLT_UpgradeDocsAllClanInfluence}Influence: {Value}/day (all clans)<br>".Translate(("Value", Signed(u.InfluenceDaily))));
                    if (u.RetinueSizeBonus != 0) sb.AppendLine("{=BLT_UpgradeDocsRetinuePerClan}Retinue Size: {Value} per clan<br>".Translate(("Value", Signed(u.RetinueSizeBonus))));
                    if (u.ArmySpeedBonus != 0) sb.AppendLine("{=BLT_UpgradeDocsArmySpeedPerClan}Army Speed: {Value} per clan in army (once/clan: {OncePerClan})<br>"
                        .Translate(("Value", Signed(u.ArmySpeedBonus)), ("OncePerClan", FormatYesNo(u.ArmySpeedOncePerClan))));
                }
                if (u.LoyaltyDailyFlat != 0 || u.LoyaltyDailyPercent != 0 || u.ProsperityDailyFlat != 0 || u.ProsperityDailyPercent != 0 ||
                    u.SecurityDailyFlat != 0 || u.SecurityDailyPercent != 0 || u.MilitiaDailyFlat != 0 || u.MilitiaDailyPercent != 0 ||
                    u.FoodDailyFlat != 0 || u.FoodDailyPercent != 0 || u.TaxIncomeFlat != 0 || u.TaxIncomePercent != 0 ||
                    u.GarrisonCapacityBonus != 0 || u.HearthDaily != 0)
                {
                    sb.AppendLine("{=BLT_UpgradeDocsAllSettlementEffects}<br><strong>Settlement Effects (All Kingdom Settlements):</strong><br>".Translate());
                    AppendSettlementEffects(sb, u.LoyaltyDailyFlat, u.LoyaltyDailyPercent, u.ProsperityDailyFlat,
                        u.ProsperityDailyPercent, u.SecurityDailyFlat, u.SecurityDailyPercent, u.MilitiaDailyFlat,
                        u.MilitiaDailyPercent, u.FoodDailyFlat, u.FoodDailyPercent, u.TaxIncomeFlat,
                        u.TaxIncomePercent, u.GarrisonCapacityBonus, u.HearthDaily);
                }
                if (u.DailyTroopSpawnAmount > 0 || u.TroopTierBonus > 0)
                {
                    sb.AppendLine("{=BLT_UpgradeDocsAllTroopSpawning}<br><strong>Troop Spawning (All Kingdom Clans):</strong><br>".Translate());
                    if (u.DailyTroopSpawnAmount > 0)
                    {
                        sb.AppendLine("{=BLT_UpgradeDocsDailySpawnPerClan}Daily Spawn: {Amount} troops/day per clan<br>".Translate(("Amount", u.DailyTroopSpawnAmount)));
                        sb.AppendLine("{=BLT_UpgradeDocsTroopTree}Troop Tree: {Tree}<br>".Translate(("Tree", u.TroopTree)));
                        sb.AppendLine("{=BLT_UpgradeDocsBaseTier}Base Tier: {Tier}<br>".Translate(("Tier", u.TroopTier)));
                    }
                    if (u.TroopTierBonus > 0 && !string.IsNullOrEmpty(u.BuffsTroopTierOf))
                        sb.AppendLine("{=BLT_UpgradeDocsTierBonus}Tier Bonus: +{Bonus} to {Target}<br>".Translate(("Bonus", u.TroopTierBonus), ("Target", u.BuffsTroopTierOf)));
                }
                return sb.Length > 0 ? sb.ToString() : "{=BLT_UpgradeDocsNoEffects}No effects configured".Translate();
            }

            private static void AppendSettlementEffects(StringBuilder sb, float loyaltyFlat, float loyaltyPercent,
                float prosperityFlat, float prosperityPercent, float securityFlat, float securityPercent,
                float militiaFlat, float militiaPercent, float foodFlat, float foodPercent, int taxFlat,
                float taxPercent, int garrisonCapacity, float hearth)
            {
                if (loyaltyFlat != 0) sb.AppendLine("{=BLT_UpgradeDocsLoyaltyDaily}Loyalty: {Value}/day<br>".Translate(("Value", Signed(loyaltyFlat))));
                if (loyaltyPercent != 0) sb.AppendLine("{=BLT_UpgradeDocsLoyaltyPercent}Loyalty: {Value}%/day<br>".Translate(("Value", Signed(loyaltyPercent))));
                if (prosperityFlat != 0) sb.AppendLine("{=BLT_UpgradeDocsProsperityDaily}Prosperity: {Value}/day<br>".Translate(("Value", Signed(prosperityFlat))));
                if (prosperityPercent != 0) sb.AppendLine("{=BLT_UpgradeDocsProsperityPercent}Prosperity: {Value}%/day<br>".Translate(("Value", Signed(prosperityPercent))));
                if (securityFlat != 0) sb.AppendLine("{=BLT_UpgradeDocsSecurityDaily}Security: {Value}/day<br>".Translate(("Value", Signed(securityFlat))));
                if (securityPercent != 0) sb.AppendLine("{=BLT_UpgradeDocsSecurityPercent}Security: {Value}%/day<br>".Translate(("Value", Signed(securityPercent))));
                if (militiaFlat != 0) sb.AppendLine("{=BLT_UpgradeDocsMilitiaDaily}Militia: {Value}/day<br>".Translate(("Value", Signed(militiaFlat))));
                if (militiaPercent != 0) sb.AppendLine("{=BLT_UpgradeDocsMilitiaPercent}Militia: {Value}%/day<br>".Translate(("Value", Signed(militiaPercent))));
                if (foodFlat != 0) sb.AppendLine("{=BLT_UpgradeDocsFoodDaily}Food: {Value}/day<br>".Translate(("Value", Signed(foodFlat))));
                if (foodPercent != 0) sb.AppendLine("{=BLT_UpgradeDocsFoodPercent}Food: {Value}%/day<br>".Translate(("Value", Signed(foodPercent))));
                if (taxFlat != 0) sb.AppendLine("{=BLT_UpgradeDocsTaxDaily}Tax Income: {Value}{Gold}/day<br>".Translate(("Value", Signed(taxFlat)), ("Gold", Naming.Gold)));
                if (taxPercent != 0) sb.AppendLine("{=BLT_UpgradeDocsTaxPercent}Tax Income: {Value}%<br>".Translate(("Value", Signed(taxPercent))));
                if (garrisonCapacity != 0) sb.AppendLine("{=BLT_UpgradeDocsGarrisonCapacity}Garrison Capacity: {Value}<br>".Translate(("Value", Signed(garrisonCapacity))));
                if (hearth != 0) sb.AppendLine("{=BLT_UpgradeDocsHearth}Hearth: {Value}<br>".Translate(("Value", Signed(hearth))));
            }

            private static string Signed(float v) => v > 0 ? $"+{v}" : v.ToString();
            private static string Signed(int v) => v > 0 ? $"+{v}" : v.ToString();
            private static string FormatYesNo(bool value)
                => value ? "{=BLT_UpgradeYes}Yes".Translate() : "{=BLT_UpgradeNo}No".Translate();

            private bool ShouldShowFullDescription(string upgradeId)
            {
                var m = System.Text.RegularExpressions.Regex.Match(upgradeId, @"^(.+?)(\d+)$");
                return m.Success ? int.Parse(m.Groups[2].Value) == 1 : true;
            }
        }

        // ── Shared string-comparison shorthand ─────────────────────────────────
        private static readonly StringComparison OIC = StringComparison.OrdinalIgnoreCase;

        public override Type HandlerConfigType => typeof(Settings);

        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config, Action<string> onSuccess, Action<string> onFailure)
        {
            if (config is not Settings settings) { onFailure("{=BLT_UpgradeInvalidConfig}Invalid configuration".Translate()); return; }
            if (adoptedHero == null) { onFailure(AdoptAHero.NoHeroMessage); return; }
            if (!settings.Enabled) { onFailure("{=BLT_UpgradeDisabled}The upgrade system is disabled".Translate()); return; }
            if (Mission.Current != null) { onFailure("{=BLT_NoMission}Cannot use this command during a mission".Translate()); return; }
            if (context.Args.IsEmpty())
            {
                onFailure("{=BLT_UpgradeUsage}Usage: [auto|bulk] all | fief <settlement_name|all> [upgrade_id] | clan <upgrade_id|all> | kingdom <upgrade_id|all> | info <fief|clan|kingdom> <name> | list [fief|clan|kingdom] | remove <fief|clan|kingdom> <name> <upgrade_id>".Translate());
                return;
            }

            // Push the accumulation setting to the behavior so daily ticks respect it immediately.
            if (UpgradeBehavior.Current != null)
            {
                UpgradeBehavior.Current.AccumulateWhenFull = settings.AccumulateWhenFull;
                UpgradeBehavior.Current.IndependentClansCountAsLords = settings.IndependentClansCountAsLords; // ← add
                UpgradeBehavior.Current.IndependentClansCountAsMercs = settings.IndependentClansCountAsMercs; // ← add
            }

            var globalConfig = GlobalCommonConfig.Get();
            if (globalConfig == null) { onFailure("{=BLT_UpgradeConfigUnavailable}Configuration not available".Translate()); return; }

            // ── Parse special flags (position-independent) ──────────────────────
            var rawArgs = context.Args.Split(' ');

            bool autoBuy = rawArgs.Any(a => MatchesCommand(a, "{=BLT_UpgradeArgAuto}auto".Translate(), "auto")
                                            || MatchesCommand(a, "{=BLT_UpgradeArgBulk}bulk".Translate(), "bulk"));

            var rawWithoutAuto = rawArgs
                .Where(a => !MatchesCommand(a, "{=BLT_UpgradeArgAuto}auto".Translate(), "auto")
                            && !MatchesCommand(a, "{=BLT_UpgradeArgBulk}bulk".Translate(), "bulk"))
                .ToArray();

            if (rawWithoutAuto.Length == 0)
            {
                onFailure("{=BLT_UpgradeNoCommandAfterFlags}No command specified after flags".Translate());
                return;
            }

            var command = GetCommand(rawWithoutAuto[0]);

            bool applyAll = command == "fief" && rawWithoutAuto.Length == 2
                                                   && MatchesCommand(rawWithoutAuto[1], "{=BLT_UpgradeArgAll}all".Translate(), "all");
            bool applyAllK = command == "fief" && rawWithoutAuto.Any(a =>
                MatchesCommand(a, "{=BLT_UpgradeArgAllKingdom}allk".Translate(), "allk"));

            if (applyAll && applyAllK)
            {
                onFailure("{=BLT_UpgradeConflictingScopes}'all' and 'allk' cannot be used together".Translate());
                return;
            }

            var cleanArgs = rawWithoutAuto
                .Where(a => command != "fief"
                            || (!MatchesCommand(a, "{=BLT_UpgradeArgAll}all".Translate(), "all")
                                && !MatchesCommand(a, "{=BLT_UpgradeArgAllKingdom}allk".Translate(), "allk")))
                .ToArray();

            if (cleanArgs.Length == 0)
            {
                onFailure("{=BLT_UpgradeNoCommandAfterFlags}No command specified after flags".Translate());
                return;
            }
            
            // ── list ────────────────────────────────────────────────────────────
            if (command == "list")
            {
                if (!settings.AllowListCommand) { onFailure("{=BLT_UpgradeListDisabled}The list command is disabled".Translate()); return; }
                string type = cleanArgs.Length > 1 ? GetCommand(cleanArgs[1]) : "all";
                HandleListCommand(type, globalConfig, onSuccess, onFailure);
                return;
            }

            // ── info ────────────────────────────────────────────────────────────
            if (command == "info")
            {
                if (cleanArgs.Length < 2) { onFailure("{=BLT_UpgradeInfoUsage}Usage: info <fief|clan|kingdom> <name>".Translate()); return; }
                string type = GetCommand(cleanArgs[1]);
                string name = string.Join(" ", cleanArgs.Skip(2));
                HandleInfoCommand(type, name, adoptedHero, globalConfig, onSuccess, onFailure);
                return;
            }

            // ── remove ──────────────────────────────────────────────────────────
            if (command == "remove")
            {
                if (cleanArgs.Length < 3) { onFailure("{=BLT_UpgradeRemoveUsage}Usage: remove <fief|clan|kingdom> <settlement_name/upgrade_id> [upgrade_id]".Translate()); return; }
                string type = GetCommand(cleanArgs[1]);
                if (type == "fief")
                {
                    if (cleanArgs.Length < 4) { onFailure("{=BLT_UpgradeRemoveFiefUsage}Usage: remove fief <settlement_name> <upgrade_id>".Translate()); return; }
                    string tName = string.Join(" ", cleanArgs.Skip(2).Take(cleanArgs.Length - 3));
                    string uId = cleanArgs.Last();
                    HandleRemoveCommand(type, tName, uId, adoptedHero, settings, globalConfig, onSuccess, onFailure);
                }
                else
                {
                    HandleRemoveCommand(type, null, cleanArgs[2], adoptedHero, settings, globalConfig, onSuccess, onFailure);
                }
                return;
            }

            // ── purchase (fief / clan / kingdom) ────────────────────────────────
            if (command == "all")
            {
                if (cleanArgs.Length != 1) { onFailure("{=BLT_UpgradeAllUsage}Usage: [auto|bulk] all".Translate()); return; }
                PurchaseAllAvailableUpgrades(adoptedHero, settings, globalConfig, autoBuy, onSuccess, onFailure);
                return;
            }

            if (command == "fief" && applyAll)
            {
                PurchaseAllFiefUpgrades(adoptedHero, settings, globalConfig, autoBuy, onSuccess, onFailure);
                return;
            }

            bool needsSettlementName = command == "fief" && !applyAll && !applyAllK;

            if (needsSettlementName && cleanArgs.Length < 3)
            {
                onFailure("{=BLT_UpgradeFiefUsage}Usage: [auto|bulk] fief <settlement_name> <upgrade_id>".Translate());
                return;
            }
            else if (!needsSettlementName && (command == "fief" || command == "clan" || command == "kingdom") && cleanArgs.Length < 2)
            {
                onFailure("{=BLT_UpgradeTypeUsage}Usage: [auto|bulk] {Command} [all|allk] <upgrade_id>"
                    .Translate(("Command", GetCommandDisplayName(command))));
                return;
            }
            else if (command != "fief" && command != "clan" && command != "kingdom")
            {
                onFailure("{=BLT_UpgradeUnknownCommand}Unknown command '{Command}'. Use all, fief, clan, or kingdom"
                    .Translate(("Command", command)));
                return;
            }

            string upgradeId;
            string targetName = null;

            if (command == "fief" && !applyAll && !applyAllK)
            {
                upgradeId = cleanArgs.Last();
                targetName = string.Join(" ", cleanArgs.Skip(1).Take(cleanArgs.Length - 2));
            }
            else
            {
                upgradeId = cleanArgs.Last();
            }

            HandlePurchaseCommand(command, targetName, upgradeId, adoptedHero, settings, globalConfig, autoBuy, applyAll, applyAllK, onSuccess, onFailure);
        }

        private static string GetCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return "";

            command = command.Trim();

            if (MatchesCommand(command, "{=BLT_UpgradeArgAuto}auto".Translate(), "auto")) return "auto";
            if (MatchesCommand(command, "{=BLT_UpgradeArgBulk}bulk".Translate(), "bulk")) return "bulk";
            if (MatchesCommand(command, "{=BLT_UpgradeArgAll}all".Translate(), "all")) return "all";
            if (MatchesCommand(command, "{=BLT_UpgradeArgAllKingdom}allk".Translate(), "allk")) return "allk";
            if (MatchesCommand(command, "{=BLT_UpgradeSubList}list".Translate(), "list")) return "list";
            if (MatchesCommand(command, "{=BLT_UpgradeSubInfo}info".Translate(), "info")) return "info";
            if (MatchesCommand(command, "{=BLT_UpgradeSubRemove}remove".Translate(), "remove")) return "remove";
            if (MatchesCommand(command, "{=BLT_UpgradeTypeFief}fief".Translate(), "fief")) return "fief";
            if (MatchesCommand(command, "{=BLT_UpgradeTypeClan}clan".Translate(), "clan")) return "clan";
            if (MatchesCommand(command, "{=BLT_UpgradeTypeKingdom}kingdom".Translate(), "kingdom")) return "kingdom";

            return command.ToLowerInvariant();
        }

        private static bool MatchesCommand(string command, string translatedCommand, string defaultCommand)
            => command.Equals(defaultCommand, StringComparison.OrdinalIgnoreCase)
               || command.Equals(translatedCommand, StringComparison.OrdinalIgnoreCase);

        private static string GetCommandDisplayName(string command) => command switch
        {
            "fief" => "{=BLT_UpgradeTypeFief}fief".Translate(),
            "clan" => "{=BLT_UpgradeTypeClan}clan".Translate(),
            "kingdom" => "{=BLT_UpgradeTypeKingdom}kingdom".Translate(),
            _ => command
        };

        // ════════════════════════════════════════════════════════════════════════
        // Auto-buy prerequisite chain builders
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns an ordered list of upgrade IDs that must be purchased to satisfy all
        /// prerequisites of <paramref name="targetId"/>, including transitively, excluding
        /// any already in <paramref name="owned"/>. The list is in dependency order
        /// (deepest prerequisite first, target last).
        /// </summary>
        private List<string> BuildFiefPurchaseChain(string targetId, HashSet<string> owned, GlobalCommonConfig config)
        {
            var result = new List<string>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Recurse(string id)
            {
                if (visited.Contains(id)) return;
                visited.Add(id);
                if (owned.Contains(id)) return;
                var up = config.FiefUpgrades?.FirstOrDefault(u => string.Equals(u.ID, id, OIC));
                if (up == null) return;
                foreach (var req in up.RequiredUpgradeIDs) Recurse(req);
                result.Add(id);
            }
            Recurse(targetId);
            return result;
        }

        private List<string> BuildClanPurchaseChain(string targetId, HashSet<string> owned, GlobalCommonConfig config)
        {
            var result = new List<string>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Recurse(string id)
            {
                if (visited.Contains(id)) return;
                visited.Add(id);
                if (owned.Contains(id)) return;
                var up = config.ClanUpgrades?.FirstOrDefault(u => string.Equals(u.ID, id, OIC));
                if (up == null) return;
                foreach (var req in up.RequiredUpgradeIDs) Recurse(req);
                result.Add(id);
            }
            Recurse(targetId);
            return result;
        }

        private List<string> BuildKingdomPurchaseChain(string targetId, HashSet<string> owned, GlobalCommonConfig config)
        {
            var result = new List<string>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Recurse(string id)
            {
                if (visited.Contains(id)) return;
                visited.Add(id);
                if (owned.Contains(id)) return;
                var up = config.KingdomUpgrades?.FirstOrDefault(u => string.Equals(u.ID, id, OIC));
                if (up == null) return;
                foreach (var req in up.RequiredUpgradeIDs) Recurse(req);
                result.Add(id);
            }
            Recurse(targetId);
            return result;
        }

        // ════════════════════════════════════════════════════════════════════════
        // List / Info helpers
        // ════════════════════════════════════════════════════════════════════════

        private void HandleListCommand(string type, GlobalCommonConfig gc, Action<string> ok, Action<string> fail)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{=BLT_UpgradeAvailableHeader}=== Available Upgrades ===".Translate());

            if (type == "all" || type == "fief")
            {
                sb.AppendLine("{=BLT_UpgradeFiefListHeader}\n[Fief Upgrades]".Translate());
                if (gc.FiefUpgrades?.Count > 0)
                    foreach (var u in gc.FiefUpgrades)
                    {
                        string tag = u.CapitalOnly
                            ? "{=BLT_UpgradeCapitalOnlyTag} [CAPITAL ONLY — use: capital list]".Translate()
                            : "";
                        sb.AppendLine($"  {u.ID}: {u.Name} - {u.GetCostString()}{tag}");
                        sb.AppendLine($"    {u.Description}");
                    }
                else { sb.Append("  "); sb.AppendLine("{=BLT_UpgradeNoFiefConfigured}No fief upgrades configured".Translate()); }
            }
            if (type == "all" || type == "clan")
            {
                sb.AppendLine("{=BLT_UpgradeClanListHeader}\n[Clan Upgrades]".Translate());
                if (gc.ClanUpgrades?.Count > 0)
                    foreach (var u in gc.ClanUpgrades) { sb.AppendLine($"  {u.ID}: {u.Name} - {u.GetCostString()}"); sb.AppendLine($"    {u.Description}"); }
                else { sb.Append("  "); sb.AppendLine("{=BLT_UpgradeNoClanConfigured}No clan upgrades configured".Translate()); }
            }
            if (type == "all" || type == "kingdom")
            {
                sb.AppendLine("{=BLT_UpgradeKingdomListHeader}\n[Kingdom Upgrades]".Translate());
                if (gc.KingdomUpgrades?.Count > 0)
                    foreach (var u in gc.KingdomUpgrades) { sb.AppendLine($"  {u.ID}: {u.Name} - {u.GetCostString()}"); sb.AppendLine($"    {u.Description}"); }
                else { sb.Append("  "); sb.AppendLine("{=BLT_UpgradeNoKingdomConfigured}No kingdom upgrades configured".Translate()); }
            }

            if (type != "all" && type != "fief" && type != "clan" && type != "kingdom")
            { fail("{=BLT_UpgradeInvalidListType}Invalid type '{Type}'. Use 'all', 'fief', 'clan', or 'kingdom'"
                .Translate(("Type", type))); return; }

            ok(sb.ToString());
        }

        private void HandleInfoCommand(string type, string name, Hero hero, GlobalCommonConfig gc, Action<string> ok, Action<string> fail)
        {
            switch (type)
            {
                case "fief": ShowFiefInfo(name, hero, gc, ok, fail); break;
                case "clan": ShowClanInfo(name, hero, gc, ok, fail); break;
                case "kingdom": ShowKingdomInfo(name, hero, gc, ok, fail); break;
                default: fail("{=BLT_UpgradeInvalidType}Invalid type. Use 'fief', 'clan', or 'kingdom'".Translate()); break;
            }
        }

        private void ShowFiefInfo(string name, Hero hero, GlobalCommonConfig gc, Action<string> ok, Action<string> fail)
        {
            if (string.IsNullOrEmpty(name)) { fail("{=BLT_UpgradeInfoFiefUsage}Usage: info <fief> <name>".Translate()); return; }
            var settlement = FindSettlement(name);
            if (settlement == null) { fail("{=BLT_UpgradeSettlementNotFound}Settlement '{Name}' not found".Translate(("Name", name))); return; }
            if (settlement.Town == null || settlement.IsVillage) { fail("{=BLT_UpgradeTownsCastlesOnly}Only towns and castles can have upgrades".Translate()); return; }

            var ids = UpgradeBehavior.Current?.GetFiefUpgrades(settlement) ?? new List<string>();
            var sb = new StringBuilder();
            sb.AppendLine("{=BLT_UpgradeEntityHeader}=== {Name} Upgrades ===".Translate(("Name", settlement.Name)));
            if (ids.Count == 0) { sb.AppendLine("{=BLT_UpgradeNonePurchased}No upgrades purchased yet".Translate()); }
            else
            {
                sb.AppendLine("{=BLT_UpgradePurchasedHeader}Purchased Upgrades:".Translate());
                foreach (var u in HighestTierOnly(ids.Select(id => gc.FiefUpgrades.FirstOrDefault(u => u.ID == id)).Where(u => u != null).Cast<object>()))
                    sb.AppendLine($"  • {((FiefUpgrade)u).Name}");
            }
            ok(sb.ToString());
        }

        private void ShowClanInfo(string name, Hero hero, GlobalCommonConfig gc, Action<string> ok, Action<string> fail)
        {
            if (string.IsNullOrEmpty(name)) { name = ""; }
            var clan = FindClan(name);
            if (clan == null) { clan = hero?.Clan; }
            if (clan == null) { fail("{=BLT_UpgradeClanNotFoundNoClan}Clan '{Name}' not found and you have no clan!".Translate(("Name", name))); return; }
            var ids = UpgradeBehavior.Current?.GetClanUpgrades(clan) ?? new List<string>();
            var sb = new StringBuilder();
            sb.AppendLine("{=BLT_UpgradeEntityHeader}=== {Name} Upgrades ===".Translate(("Name", clan.Name)));
            if (ids.Count == 0) { sb.AppendLine("{=BLT_UpgradeNonePurchased}No upgrades purchased yet".Translate()); }
            else
            {
                sb.AppendLine("{=BLT_UpgradePurchasedHeader}Purchased Upgrades:".Translate());
                foreach (var u in HighestTierOnly(ids.Select(id => gc.ClanUpgrades.FirstOrDefault(u => u.ID == id)).Where(u => u != null).Cast<object>()))
                    sb.AppendLine($"  • {((ClanUpgrade)u).Name}");
            }
            ok(sb.ToString());
        }

        private void ShowKingdomInfo(string name, Hero hero, GlobalCommonConfig gc, Action<string> ok, Action<string> fail)
        {
            if (string.IsNullOrEmpty(name)) { fail("{=BLT_UpgradeInfoKingdomUsage}Usage: info <kingdom> <name>".Translate()); return; }
            var kingdom = FindKingdom(name);
            if (kingdom == null) { kingdom = hero?.Clan?.Kingdom; }
            if (kingdom == null) { fail("{=BLT_UpgradeKingdomNotFound}Kingdom '{Name}' not found".Translate(("Name", name))); return; }
            var ids = UpgradeBehavior.Current?.GetKingdomUpgrades(kingdom) ?? new List<string>();
            var sb = new StringBuilder();
            sb.AppendLine("{=BLT_UpgradeEntityHeader}=== {Name} Upgrades ===".Translate(("Name", kingdom.Name)));
            if (ids.Count == 0) { sb.AppendLine("{=BLT_UpgradeNonePurchased}No upgrades purchased yet".Translate()); }
            else
            {
                sb.AppendLine("{=BLT_UpgradePurchasedHeader}Purchased Upgrades:".Translate());
                foreach (var u in HighestTierOnly(ids.Select(id => gc.KingdomUpgrades.FirstOrDefault(u => u.ID == id)).Where(u => u != null).Cast<object>()))
                    sb.AppendLine($"  • {((KingdomUpgrade)u).Name}");
            }
            ok(sb.ToString());
        }

        /// <summary>Groups upgrades by their base ID (strip trailing digits) and keeps only the highest-tier entry.</summary>
        private static IEnumerable<object> HighestTierOnly(IEnumerable<object> items)
        {
            // Each item must expose an "ID" property — use dynamic or a shared interface if available.
            // Using reflection-free approach: cast to dynamic to read ID.
            return items
                .GroupBy(u => { var id = GetId(u); var m = System.Text.RegularExpressions.Regex.Match(id, @"^(.+?)(\d+)$"); return m.Success ? m.Groups[1].Value : id; })
                .Select(g => g.OrderByDescending(u => { var m = System.Text.RegularExpressions.Regex.Match(GetId(u), @"(\d+)$"); return m.Success ? int.Parse(m.Groups[1].Value) : 0; }).First())
                .OrderBy(u => GetId(u));
        }

        private static string GetId(object u)
        {
            if (u is FiefUpgrade f) return f.ID;
            if (u is ClanUpgrade c) return c.ID;
            if (u is KingdomUpgrade k) return k.ID;
            return "";
        }

        // ════════════════════════════════════════════════════════════════════════
        // Purchase routing
        // ════════════════════════════════════════════════════════════════════════

        private void HandlePurchaseCommand(
            string type, string name, string upgradeId,
            Hero hero, Settings settings, GlobalCommonConfig gc,
            bool autoBuy, bool applyAll, bool applyAllK,
            Action<string> ok, Action<string> fail)
        {
            switch (type)
            {
                case "fief":
                    if (applyAll)
                        PurchaseAllFiefUpgrades(hero, settings, gc, autoBuy, ok, fail);
                    else if (applyAllK)
                        PurchaseFiefUpgradeMulti(upgradeId, hero, settings, gc, autoBuy, applyAllK, ok, fail);
                    else
                        PurchaseFiefUpgrade(name, upgradeId, hero, settings, gc, autoBuy, ok, fail);
                    break;
                case "clan":
                    if (MatchesCommand(upgradeId, "{=BLT_UpgradeArgAll}all".Translate(), "all"))
                    {
                        PurchaseAllClanUpgrades(hero, settings, gc, autoBuy, ok, fail);
                        return;
                    }
                    PurchaseClanUpgrade(upgradeId, hero, settings, gc, autoBuy, ok, fail);
                    break;
                case "kingdom":
                    if (MatchesCommand(upgradeId, "{=BLT_UpgradeArgAll}all".Translate(), "all"))
                    {
                        PurchaseAllKingdomUpgrades(hero, gc, ok, fail);
                        return;
                    }
                    
                    PurchaseKingdomUpgrade(upgradeId, hero, gc, autoBuy, ok, fail);
                    break;
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // Fief purchase — single settlement
        // ════════════════════════════════════════════════════════════════════════

        private void PurchaseFiefUpgrade(
            string name, string upgradeId,
            Hero hero, Settings settings, GlobalCommonConfig gc,
            bool autoBuy,
            Action<string> ok, Action<string> fail)
        {
            var settlement = FindSettlement(name);
            if (settlement == null) { fail("{=BLT_UpgradeSettlementNotFound}Settlement '{Name}' not found".Translate(("Name", name))); return; }
            if (settlement.Town == null) { fail("{=BLT_UpgradeTownsCastlesOnly}Only towns and castles can have upgrades".Translate()); return; }

            // Permission check
            bool isOwner = settlement.OwnerClan == hero.Clan;
            if (!isOwner && VassalBehavior.Current != null)
                foreach (Clan v in VassalBehavior.Current.GetVassalClans(hero.Clan))
                    if (v == settlement.OwnerClan) { isOwner = true; break; }

            bool isKingdomLeader = settings.AllowKingdomLeadersForFiefs
                && hero.Clan?.Kingdom != null
                && hero.Clan.Kingdom.Leader == hero
                && settlement.OwnerClan?.Kingdom == hero.Clan.Kingdom;

            if (!isOwner && !isKingdomLeader) { fail("{=BLT_UpgradeNoFiefPermission}You don't have permission to upgrade {Settlement}".Translate(("Settlement", settlement.Name))); return; }
            if (!hero.IsClanLeader && !isKingdomLeader) { fail("{=BLT_UpgradeFiefClanLeaderOnly}Only clan leaders can purchase fief upgrades".Translate()); return; }

            // Resolve the upgrade object for the final target first (validate it exists)
            var targetUpgrade = gc.FiefUpgrades?.FirstOrDefault(u => u.ID == upgradeId);
            if (targetUpgrade == null) { fail("{=BLT_UpgradeNotFound}Upgrade '{Id}' not found".Translate(("Id", upgradeId))); return; }
            if (targetUpgrade.CoastalOnly && !settlement.HasPort) { fail("{=BLT_UpgradeCoastalOnly}This is a Coastal Only upgrade, try again on a coastal settlement".Translate()); return; }

            // Build purchase chain (includes prerequisites when autoBuy is true)
            var owned = new HashSet<string>(UpgradeBehavior.Current?.GetFiefUpgrades(settlement) ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

            if (CapitalBehavior.Current != null)
                owned.UnionWith(CapitalBehavior.Current.GetCapitalUpgrades(hero.Clan));

            if (owned.Contains(upgradeId)) { fail("{=BLT_UpgradeAlreadyOwned}{Name} already has this upgrade".Translate(("Name", settlement.Name))); return; }

            List<string> chain;
            if (autoBuy)
            {
                chain = BuildFiefPurchaseChain(upgradeId, owned, gc);
            if (chain.Count == 0) { fail("{=BLT_UpgradeAlreadyOwned}{Name} already has this upgrade".Translate(("Name", settlement.Name))); return; }
            }
            else
            {
                // Original prerequisite check
                var reqIds = targetUpgrade.RequiredUpgradeIDs;
                if (reqIds.Count > 0 && !targetUpgrade.AreRequiredUpgradesMet(owned))
                {
                    var missing = reqIds.Where(id => !owned.Contains(id, StringComparer.OrdinalIgnoreCase));
                    fail("{=BLT_UpgradeRequiresFirst}Requires upgrade(s) first: {Upgrades}".Translate(("Upgrades", string.Join(", ", missing))));
                    return;
                }
                chain = new List<string> { upgradeId };
            }

            // Execute the chain
            var results = ExecuteFiefChain(settlement, chain, hero, gc, owned);
            ReportChainResults(results, upgradeId,
                "{=BLT_UpgradeForEntity}for {Name}".Translate(("Name", settlement.Name)), ok, fail);

            if (results.Any(r => r.Success))
                Log.ShowInformation("{=BLT_UpgradeLogPurchasedFief}{Hero} purchased upgrades for {Settlement}"
                    .Translate(("Hero", hero.Name), ("Settlement", settlement.Name)), hero.CharacterObject, Log.Sound.Notification1);
        }

        // ════════════════════════════════════════════════════════════════════════
        // Bulk purchase
        // ════════════════════════════════════════════════════════════════════════

        private void PurchaseAllAvailableUpgrades(
            Hero hero, Settings settings, GlobalCommonConfig gc, bool autoBuy,
            Action<string> ok, Action<string> fail)
        {
            var sections = new List<string>();
            int successes = 0;

            void Capture(string label, Action<Action<string>, Action<string>> action)
            {
                action(
                    message =>
                    {
                        successes++;
                        sections.Add($"{label}: {message}");
                    },
                    message => sections.Add($"{label}: {message}"));
            }

            Capture("{=BLT_UpgradeFiefsLabel}Fiefs".Translate(), (s, f) => PurchaseAllFiefUpgrades(hero, settings, gc, autoBuy, s, f));
            Capture("{=BLT_UpgradeClanLabel}Clan".Translate(), (s, f) => PurchaseAllClanUpgrades(hero, settings, gc, autoBuy, s, f));
            Capture("{=BLT_UpgradeKingdomLabel}Kingdom".Translate(), (s, f) => PurchaseAllKingdomUpgrades(hero, gc, s, f));

            var report = string.Join("\n", sections.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (successes > 0)
                ok(report);
            else
                fail(string.IsNullOrWhiteSpace(report) ? "{=BLT_UpgradeNoneCouldBePurchased}No upgrades could be purchased.".Translate() : report);
        }

        private void PurchaseAllFiefUpgrades(
            Hero hero, Settings settings, GlobalCommonConfig gc, bool autoBuy,
            Action<string> ok, Action<string> fail)
        {
            if (hero?.Clan == null) { fail("{=BLT_UpgradeNotInClan}You are not in a clan!".Translate()); return; }
            if (gc.FiefUpgrades == null || gc.FiefUpgrades.Count == 0) { fail("{=BLT_UpgradeNoFiefConfigured}No fief upgrades configured".Translate()); return; }

            var targetSet = new HashSet<Settlement>();

            if (hero.IsClanLeader)
            {
                var allowedClans = new HashSet<Clan> { hero.Clan };
                if (VassalBehavior.Current != null)
                    foreach (var v in VassalBehavior.Current.GetVassalClans(hero.Clan))
                        allowedClans.Add(v);

                foreach (var settlement in Settlement.All.Where(s => s.Town != null && allowedClans.Contains(s.OwnerClan)))
                    targetSet.Add(settlement);
            }

            bool isKingdomLeader = settings.AllowKingdomLeadersForFiefs
                && hero.Clan.Kingdom != null
                && hero.Clan.Kingdom.Leader == hero;

            if (isKingdomLeader)
                foreach (var settlement in Settlement.All.Where(s => s.Town != null && s.OwnerClan?.Kingdom == hero.Clan.Kingdom))
                    targetSet.Add(settlement);

            var targets = targetSet.ToList();
            if (targets.Count == 0) { fail("{=BLT_UpgradeNoValidSettlements}No valid settlements found".Translate()); return; }

            int settlementsUpdated = 0;
            int totalBought = 0;
            var failed = new List<string>();

            foreach (var settlement in targets)
            {
                var owned = new HashSet<string>(
                    UpgradeBehavior.Current?.GetFiefUpgrades(settlement) ?? new List<string>(),
                    StringComparer.OrdinalIgnoreCase);

                if (CapitalBehavior.Current != null)
                    owned.UnionWith(CapitalBehavior.Current.GetCapitalUpgrades(hero.Clan));

                bool isCapital = CapitalBehavior.Current?.IsCapital(settlement, hero.Clan) == true;
                int boughtHere = 0;

                foreach (var up in gc.FiefUpgrades.OrderBy(u => u.TierLevel))
                {
                    if (owned.Contains(up.ID)) continue;
                    if (up.CoastalOnly && !settlement.HasPort) continue;
                    if (up.CapitalOnly && !isCapital) continue;

                    var chain = BuildFiefPurchaseChain(up.ID, owned, gc);
                    if (chain.Count == 0) continue;

                    bool chainApplies = true;
                    foreach (var id in chain)
                    {
                        var chainUpgrade = gc.FiefUpgrades.FirstOrDefault(u => string.Equals(u.ID, id, OIC));
                        if (chainUpgrade == null) continue;
                        if (chainUpgrade.CoastalOnly && !settlement.HasPort) { chainApplies = false; break; }
                        if (chainUpgrade.CapitalOnly && !isCapital) { chainApplies = false; break; }
                    }
                    if (!chainApplies) continue;

                    var results = ExecuteFiefChain(settlement, chain, hero, gc, owned);
                    boughtHere += results.Count(r => r.Success);

                    var blocked = results.FirstOrDefault(r => !r.Success);
                    if (blocked != null)
                    {
                        failed.Add($"{settlement.Name}: {blocked.Message}");
                        break;
                    }
                }

                if (boughtHere > 0)
                {
                    settlementsUpdated++;
                    totalBought += boughtHere;
                }
            }

            if (totalBought > 0)
            {
                var message = "{=BLT_UpgradePurchasedFiefBulk}Purchased {Count} fief upgrade(s) across {Settlements} settlement(s)."
                    .Translate(("Count", totalBought), ("Settlements", settlementsUpdated));
                if (failed.Count > 0)
                    message += "{=BLT_UpgradeStopped} Stopped: {Reason}".Translate(("Reason", failed[0]));

                ok(message);
                Log.ShowInformation("{=BLT_UpgradeLogPurchasedAllFiefs}{Hero} purchased all available fief upgrades"
                    .Translate(("Hero", hero.Name)), hero.CharacterObject, Log.Sound.Notification1);
                return;
            }

            fail(failed.Count > 0 ? failed[0] : "{=BLT_UpgradeNoFiefCouldBePurchased}No fief upgrades could be purchased.".Translate());
        }

        private void PurchaseAllClanUpgrades(
            Hero hero,
            Settings settings,
            GlobalCommonConfig gc,
            bool autoBuy,
            Action<string> ok,
            Action<string> fail)
        {
            var clan = hero?.Clan;
            if (clan == null)
            {
                fail("{=BLT_UpgradeNotInClan}You are not in a clan!".Translate());
                return;
            }

            if (!settings.AllowAnyClanMemberForClanUpgrades && !hero.IsClanLeader)
            {
                fail("{=BLT_UpgradeClanLeaderOnly}Only clan leaders can purchase clan upgrades".Translate());
                return;
            }

            if (gc.ClanUpgrades == null || gc.ClanUpgrades.Count == 0)
            {
                fail("{=BLT_UpgradeNoClanConfigured}No clan upgrades configured".Translate());
                return;
            }

            var owned = new HashSet<string>(
                UpgradeBehavior.Current?.GetClanUpgrades(clan) ?? new(),
                StringComparer.OrdinalIgnoreCase);

            int bought = 0;

            foreach (var up in gc.ClanUpgrades.OrderBy(u => u.TierLevel))
            {
                if (owned.Contains(up.ID))
                    continue;

                var chain = BuildClanPurchaseChain(up.ID, owned, gc);

                var results = ExecuteClanChain(
                    clan,
                    chain,
                    hero,
                    gc,
                    owned);

                bought += results.Count(x => x.Success);

                if (results.Any(x => !x.Success))
                    break;
            }

            if (bought > 0)
                ok("{=BLT_UpgradePurchasedClanBulk}Purchased {Count} clan upgrades.".Translate(("Count", bought)));
            else
                fail("{=BLT_UpgradeNoneCouldBePurchased}No upgrades could be purchased.".Translate());
        }
        
        private void PurchaseAllKingdomUpgrades(
            Hero hero,
            GlobalCommonConfig gc,
            Action<string> ok,
            Action<string> fail)
        {
            if (hero.Clan == null)
            {
                fail("{=BLT_UpgradeNotInClan}You are not in a clan!".Translate());
                return;
            }

            var kingdom = hero.Clan.Kingdom;
            if (kingdom == null)
            {
                fail("{=BLT_UpgradeNotInKingdom}You are not in a kingdom!".Translate());
                return;
            }

            if (kingdom.Leader != hero)
            {
                fail("{=BLT_UpgradeKingdomRulerOnly}Only the kingdom ruler can purchase kingdom upgrades".Translate());
                return;
            }

            if (gc.KingdomUpgrades == null || gc.KingdomUpgrades.Count == 0)
            {
                fail("{=BLT_UpgradeNoKingdomConfigured}No kingdom upgrades configured".Translate());
                return;
            }

            var owned = new HashSet<string>(
                UpgradeBehavior.Current?.GetKingdomUpgrades(kingdom) ?? new List<string>(),
                StringComparer.OrdinalIgnoreCase);

            int bought = 0;
            var failed = new List<string>();

            foreach (var up in gc.KingdomUpgrades.OrderBy(u => u.TierLevel))
            {
                if (owned.Contains(up.ID))
                    continue;

                var chain = BuildKingdomPurchaseChain(up.ID, owned, gc);
                if (chain.Count == 0)
                    continue;

                var results = ExecuteKingdomChain(kingdom, chain, hero, gc, owned);

                bought += results.Count(r => r.Success);

                var blocked = results.FirstOrDefault(r => !r.Success);
                if (blocked != null)
                {
                    failed.Add($"{up.ID}: {blocked.Message}");
                    break;
                }
            }

            if (bought > 0)
            {
                var message = "{=BLT_UpgradePurchasedKingdomBulk}Purchased {Count} kingdom upgrade(s) for {Kingdom}."
                    .Translate(("Count", bought), ("Kingdom", kingdom.Name));
                if (failed.Count > 0)
                    message += "{=BLT_UpgradeStopped} Stopped: {Reason}".Translate(("Reason", failed[0]));

                ok(message);
                return;
            }

            fail(failed.Count > 0 ? failed[0] : "{=BLT_UpgradeNoKingdomCouldBePurchased}No kingdom upgrades could be purchased.".Translate());
        }
        
        private void PurchaseFiefUpgradeMulti(
            string upgradeId,
            Hero hero, Settings settings, GlobalCommonConfig gc,
            bool autoBuy, bool forKingdom,
            Action<string> ok, Action<string> fail)
        {
            IEnumerable<Settlement> targets;

            if (forKingdom)
            {
                if (hero.Clan == null) { fail("{=BLT_UpgradeNotInClan}You are not in a clan!".Translate()); return; }
                var kingdom = hero.Clan.Kingdom;
                if (kingdom == null) { fail("{=BLT_UpgradeNotInKingdom}You are not in a kingdom!".Translate()); return; }
                if (kingdom.Leader != hero && !settings.AllowKingdomLeadersForFiefs)
                { fail("{=BLT_UpgradeAllKingdomRulerOnly}Only the kingdom ruler can use 'allk'".Translate()); return; }
                targets = Settlement.All.Where(s => s.OwnerClan?.Kingdom == kingdom && s.Town != null);
            }
            else // applyAll — clan's own fiefs + vassal fiefs (same scope as single-settlement permission)
            {
                if (hero.Clan == null) { fail("{=BLT_UpgradeNotInClan}You are not in a clan!".Translate()); return; }
                if (!hero.IsClanLeader) { fail("{=BLT_UpgradeAllClanLeaderOnly}Only clan leaders can use 'all'".Translate()); return; }
                var allowedClans = new HashSet<Clan> { hero.Clan };
                if (VassalBehavior.Current != null)
                    foreach (var v in VassalBehavior.Current.GetVassalClans(hero.Clan))
                        allowedClans.Add(v);
                targets = Settlement.All.Where(s => allowedClans.Contains(s.OwnerClan) && s.Town != null);
            }

            var targetList = targets.ToList();
            if (targetList.Count == 0) { fail("{=BLT_UpgradeNoValidSettlements}No valid settlements found".Translate()); return; }

            // Validate the upgrade exists once up-front
            var targetUpgrade = gc.FiefUpgrades?.FirstOrDefault(u => u.ID == upgradeId);
            if (targetUpgrade == null) { fail("{=BLT_UpgradeNotFound}Upgrade '{Id}' not found".Translate(("Id", upgradeId))); return; }

            int settlementsSuccess = 0, settlementsSkipped = 0, settlementsFailed = 0;
            int totalBought = 0;
            var failMessages = new List<string>();

            foreach (var settlement in targetList)
            {
                // Skip non-coastal settlements for coastal-only upgrades silently
                if (targetUpgrade.CoastalOnly && !settlement.HasPort) { settlementsSkipped++; continue; }

                var owned = new HashSet<string>(UpgradeBehavior.Current?.GetFiefUpgrades(settlement) ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

            if (targetUpgrade.CapitalOnly) { fail("{=BLT_UpgradeCapitalOnlyDirect}'{Id}' is a capital-only upgrade; purchase it for your capital settlement directly".Translate(("Id", upgradeId))); return; }

                // Already fully upgraded — skip silently
                if (owned.Contains(upgradeId)) { settlementsSkipped++; continue; }

                List<string> chain;
                if (autoBuy)
                {
                    chain = BuildFiefPurchaseChain(upgradeId, owned, gc);
                    if (chain.Count == 0) { settlementsSkipped++; continue; }
                }
                else
                {
                    var reqIds = targetUpgrade.RequiredUpgradeIDs;
                    if (reqIds.Count > 0 && !targetUpgrade.AreRequiredUpgradesMet(owned))
                    {
                        var missing = reqIds.Where(id => !owned.Contains(id, StringComparer.OrdinalIgnoreCase));
                    failMessages.Add("{=BLT_UpgradeSettlementMissing}{Settlement}: missing {Upgrades}"
                        .Translate(("Settlement", settlement.Name), ("Upgrades", string.Join(", ", missing))));
                        settlementsFailed++;
                        continue;
                    }
                    chain = new List<string> { upgradeId };
                }

                var results = ExecuteFiefChain(settlement, chain, hero, gc, owned);
                int bought = results.Count(r => r.Success);
                if (bought > 0) { settlementsSuccess++; totalBought += bought; }
                else
                {
                    settlementsFailed++;
                    var firstFail = results.FirstOrDefault(r => !r.Success);
                    if (firstFail != null) failMessages.Add($"{settlement.Name}: {firstFail.Message}");
                }
            }

            if (settlementsSuccess > 0)
                Log.ShowInformation("{=BLT_UpgradeLogPurchasedMultiple}{Hero} purchased upgrades across multiple settlements"
                    .Translate(("Hero", hero.Name)), hero.CharacterObject, Log.Sound.Notification1);

            var sb = new StringBuilder();
            string scope = forKingdom ? "{=BLT_UpgradeTypeKingdom}kingdom".Translate() : "{=BLT_UpgradeTypeClan}clan".Translate();
            sb.AppendLine("{=BLT_UpgradeResultsHeader}=== Upgrade Results ({Scope}) ===".Translate(("Scope", scope)));
            sb.AppendLine("{=BLT_UpgradeSettlementsUpdated}Settlements updated : {Count}".Translate(("Count", settlementsSuccess)));
            sb.AppendLine("{=BLT_UpgradeSettlementsSkipped}Settlements skipped : {Count}  (already owned or ineligible)".Translate(("Count", settlementsSkipped)));
            sb.AppendLine("{=BLT_UpgradeSettlementsFailed}Settlements failed  : {Count}".Translate(("Count", settlementsFailed)));
            if (totalBought > 0) sb.AppendLine("{=BLT_UpgradeTotalBought}Total upgrades bought: {Count}".Translate(("Count", totalBought)));
            if (failMessages.Count > 0)
            {
                sb.AppendLine("{=BLT_UpgradeFailuresHeader}Failures:".Translate());
                foreach (var m in failMessages) sb.AppendLine($"  • {m}");
            }

            if (settlementsSuccess > 0 || settlementsSkipped > 0) ok(sb.ToString());
            else fail(sb.ToString());
        }

        // ════════════════════════════════════════════════════════════════════════
        // Fief chain execution helper
        // ════════════════════════════════════════════════════════════════════════

        private class PurchaseResult
        {
            public bool Success;
            public string UpgradeId;
            public string UpgradeName; // display name from config
            public string Message;     // failure reason when !Success
        }

        private List<PurchaseResult> ExecuteFiefChain(Settlement settlement, List<string> chain, Hero hero, GlobalCommonConfig gc, HashSet<string> alreadyOwned)
        {
            var results = new List<PurchaseResult>();
            foreach (var id in chain)
            {
                if (alreadyOwned.Contains(id)) continue;
                var up = gc.FiefUpgrades.FirstOrDefault(u => u.ID == id);
                if (up == null)
                { results.Add(new PurchaseResult { UpgradeId = id, UpgradeName = id, Success = false, Message = "{=BLT_UpgradeNotFoundInConfig}Upgrade '{Id}' not found in config".Translate(("Id", id)) }); return results; }

                int gold = BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(hero);
                if (gold < up.GoldCost)
                { results.Add(new PurchaseResult { UpgradeId = id, UpgradeName = up.Name, Success = false, Message = Naming.NotEnoughGold(up.GoldCost, gold) }); return results; }

                if (up.CapitalOnly)
                {
                    if (CapitalBehavior.Current?.IsCapital(settlement, hero.Clan) != true)
                    {
                        results.Add(new PurchaseResult
                        {
                            UpgradeId = id,
                            UpgradeName = up.Name,
                            Success = false,
                            Message = "{=BLT_UpgradeMustBeActiveCapital}'{Upgrade}' is a capital-only upgrade — {Settlement} must be your active capital"
                                .Translate(("Upgrade", up.Name), ("Settlement", settlement.Name))
                        });
                        return results;
                    }
                    BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, -up.GoldCost, true);
                    CapitalBehavior.Current.AddCapitalUpgrade(hero.Clan, id);
                }
                else
                {
                    BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, -up.GoldCost, true);
                    UpgradeBehavior.Current?.AddFiefUpgrade(settlement, id);
                }

                alreadyOwned.Add(id);
                results.Add(new PurchaseResult { UpgradeId = id, UpgradeName = up.Name, Success = true });
            }
            return results;
        }


        // ════════════════════════════════════════════════════════════════════════
        // Clan purchase
        // ════════════════════════════════════════════════════════════════════════

        private void PurchaseClanUpgrade(
            string upgradeId, Hero hero, Settings settings, GlobalCommonConfig gc,
            bool autoBuy,
            Action<string> ok, Action<string> fail)
        {
            var clan = hero?.Clan;
            if (clan == null) { fail("{=BLT_UpgradeNotInClan}You are not in a clan!".Translate()); return; }

            if (!settings.AllowAnyClanMemberForClanUpgrades && !hero.IsClanLeader)
            { fail("{=BLT_UpgradeClanLeaderOnly}Only clan leaders can purchase clan upgrades".Translate()); return; }

            var targetUpgrade = gc.ClanUpgrades?.FirstOrDefault(u => u.ID == upgradeId);
            if (targetUpgrade == null) { fail("{=BLT_UpgradeNotFound}Upgrade '{Id}' not found".Translate(("Id", upgradeId))); return; }

            var owned = new HashSet<string>(UpgradeBehavior.Current?.GetClanUpgrades(clan) ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            if (owned.Contains(upgradeId)) { fail("{=BLT_UpgradeAlreadyOwned}{Name} already has this upgrade".Translate(("Name", clan.Name))); return; }

            List<string> chain;
            if (autoBuy)
            {
                chain = BuildClanPurchaseChain(upgradeId, owned, gc);
            if (chain.Count == 0) { fail("{=BLT_UpgradeAlreadyOwned}{Name} already has this upgrade".Translate(("Name", clan.Name))); return; }
            }
            else
            {
                var reqIds = targetUpgrade.RequiredUpgradeIDs;
                if (reqIds.Count > 0 && !targetUpgrade.AreRequiredUpgradesMet(owned))
                {
                    var missing = reqIds.Where(id => !owned.Contains(id, StringComparer.OrdinalIgnoreCase));
                fail("{=BLT_UpgradeRequiresFirst}Requires upgrade(s) first: {Upgrades}".Translate(("Upgrades", string.Join(", ", missing))));
                    return;
                }
                chain = new List<string> { upgradeId };
            }

            var results = ExecuteClanChain(clan, chain, hero, gc, owned);
            ReportChainResults(results, upgradeId,
                "{=BLT_UpgradeForEntity}for {Name}".Translate(("Name", clan.Name)), ok, fail);

            if (results.Any(r => r.Success))
                Log.ShowInformation("{=BLT_UpgradeLogPurchasedClan}{Hero} purchased clan upgrade(s) for {Clan}"
                    .Translate(("Hero", hero.Name), ("Clan", clan.Name)), hero.CharacterObject, Log.Sound.Notification1);
        }

        private List<PurchaseResult> ExecuteClanChain(Clan clan, List<string> chain, Hero hero, GlobalCommonConfig gc, HashSet<string> owned)
        {
            var results = new List<PurchaseResult>();
            foreach (var id in chain)
            {
                if (owned.Contains(id)) continue;
                var up = gc.ClanUpgrades.FirstOrDefault(u => u.ID == id);
                if (up == null) { results.Add(new PurchaseResult { UpgradeId = id, Success = false, Message = "{=BLT_UpgradeNotFound}Upgrade '{Id}' not found".Translate(("Id", id)) }); return results; }
                int gold = BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(hero);
                if (gold < up.GoldCost) { results.Add(new PurchaseResult { UpgradeId = id, Success = false, Message = Naming.NotEnoughGold(up.GoldCost, gold) }); return results; }
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, -up.GoldCost, true);
                UpgradeBehavior.Current?.AddClanUpgrade(clan, id);
                owned.Add(id);
                results.Add(new PurchaseResult { UpgradeId = id, UpgradeName = up.Name, Success = true, Message = "{=BLT_UpgradePurchased}Purchased '{Upgrade}'".Translate(("Upgrade", up.Name)) });
            }
            return results;
        }

        // ════════════════════════════════════════════════════════════════════════
        // Kingdom purchase
        // ════════════════════════════════════════════════════════════════════════

        private void PurchaseKingdomUpgrade(
            string upgradeId, Hero hero, GlobalCommonConfig gc,
            bool autoBuy,
            Action<string> ok, Action<string> fail)
        {
            if (hero.Clan == null) { fail("{=BLT_UpgradeNotInClan}You are not in a clan!".Translate()); return; }
            var kingdom = hero.Clan.Kingdom;
            if (kingdom == null) { fail("{=BLT_UpgradeNotInKingdom}You are not in a kingdom!".Translate()); return; }
            if (kingdom.Leader != hero) { fail("{=BLT_UpgradeKingdomRulerOnly}Only the kingdom ruler can purchase kingdom upgrades".Translate()); return; }

            var targetUpgrade = gc.KingdomUpgrades?.FirstOrDefault(u => u.ID == upgradeId);
            if (targetUpgrade == null) { fail("{=BLT_UpgradeNotFound}Upgrade '{Id}' not found".Translate(("Id", upgradeId))); return; }

            var owned = new HashSet<string>(UpgradeBehavior.Current?.GetKingdomUpgrades(kingdom) ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            if (owned.Contains(upgradeId)) { fail("{=BLT_UpgradeAlreadyOwned}{Name} already has this upgrade".Translate(("Name", kingdom.Name))); return; }

            List<string> chain;
            if (autoBuy)
            {
                chain = BuildKingdomPurchaseChain(upgradeId, owned, gc);
            if (chain.Count == 0) { fail("{=BLT_UpgradeAlreadyOwned}{Name} already has this upgrade".Translate(("Name", kingdom.Name))); return; }
            }
            else
            {
                var reqIds = targetUpgrade.RequiredUpgradeIDs;
                if (reqIds.Count > 0 && !targetUpgrade.AreRequiredUpgradesMet(owned))
                {
                    var missing = reqIds.Where(id => !owned.Contains(id, StringComparer.OrdinalIgnoreCase));
                fail("{=BLT_UpgradeRequiresFirst}Requires upgrade(s) first: {Upgrades}".Translate(("Upgrades", string.Join(", ", missing))));
                    return;
                }
                chain = new List<string> { upgradeId };
            }

            var results = ExecuteKingdomChain(kingdom, chain, hero, gc, owned);
            ReportChainResults(results, upgradeId,
                "{=BLT_UpgradeForEntity}for {Name}".Translate(("Name", kingdom.Name)), ok, fail);

            if (results.Any(r => r.Success))
                Log.ShowInformation("{=BLT_UpgradeLogPurchasedKingdom}{Hero} purchased kingdom upgrade(s) for {Kingdom}"
                    .Translate(("Hero", hero.Name), ("Kingdom", kingdom.Name)), hero.CharacterObject, Log.Sound.Horns2);
        }

        private List<PurchaseResult> ExecuteKingdomChain(Kingdom kingdom, List<string> chain, Hero hero, GlobalCommonConfig gc, HashSet<string> owned)
        {
            var results = new List<PurchaseResult>();
            foreach (var id in chain)
            {
                if (owned.Contains(id)) continue;
                var up = gc.KingdomUpgrades.FirstOrDefault(u => u.ID == id);
                if (up == null) { results.Add(new PurchaseResult { UpgradeId = id, Success = false, Message = "{=BLT_UpgradeNotFound}Upgrade '{Id}' not found".Translate(("Id", id)) }); return results; }
                int gold = BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(hero);
                if (gold < up.GoldCost) { results.Add(new PurchaseResult { UpgradeId = id, Success = false, Message = Naming.NotEnoughGold(up.GoldCost, gold) }); return results; }
                if (up.InfluenceCost > 0 && hero.Clan.Influence < up.InfluenceCost)
                {
                    results.Add(new PurchaseResult { UpgradeId = id, Success = false, Message = "{=BLT_UpgradeNotEnoughInfluence}Not enough influence (need {Need}, have {Have})"
                        .Translate(("Need", up.InfluenceCost), ("Have", (int)hero.Clan.Influence)) });
                    return results;
                }
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, -up.GoldCost, true);
                if (up.InfluenceCost > 0) hero.Clan.Influence -= up.InfluenceCost;
                UpgradeBehavior.Current?.AddKingdomUpgrade(kingdom, id);
                owned.Add(id);
                results.Add(new PurchaseResult { UpgradeId = id, UpgradeName = up.Name, Success = true, Message = "{=BLT_UpgradePurchased}Purchased '{Upgrade}'".Translate(("Upgrade", up.Name)) });
            }
            return results;
        }

        // ════════════════════════════════════════════════════════════════════════
        // Chain result reporter
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Reports purchase results as at most two lines:
        ///   "Auto-purchased: Prereq 1-3, OtherThing"   (only if prereqs were bought)
        ///   "Purchased 'Target' {context}!"
        /// Plus an optional "Stopped: {reason}" line if the chain was cut short by lack of gold/influence.
        /// </summary>
        private static void ReportChainResults(List<PurchaseResult> results, string targetId, string context, Action<string> ok, Action<string> fail)
        {
            var bought = results.Where(r => r.Success).ToList();
            var blocked = results.FirstOrDefault(r => !r.Success);

            if (bought.Count == 0)
            {
                fail(blocked?.Message ?? "{=BLT_UpgradePurchaseFailed}Purchase failed".Translate());
                return;
            }

            var prereqs = bought.Where(r => !r.UpgradeId.Equals(targetId, OIC)).ToList();
            var target = bought.FirstOrDefault(r => r.UpgradeId.Equals(targetId, OIC));

            var sb = new StringBuilder();

            if (prereqs.Count > 0)
                sb.AppendLine("{=BLT_UpgradeAutoPurchased}Auto-purchased: {Upgrades}"
                    .Translate(("Upgrades", CollapseChainNames(prereqs))));

            if (target != null)
                sb.Append("{=BLT_UpgradePurchasedFor}Purchased '{Upgrade}' {Context}!"
                    .Translate(("Upgrade", target.UpgradeName), ("Context", context)));
            else // target wasn't reached (ran out of gold mid-chain), but some prereqs succeeded
                sb.Append("{=BLT_UpgradePartiallyPurchased}Partially purchased prerequisites {Context}"
                    .Translate(("Context", context)));

            if (blocked != null)
                sb.Append("{=BLT_UpgradeStoppedSeparator} | Stopped: {Reason}".Translate(("Reason", blocked.Message)));

            ok(sb.ToString());
        }

        /// <summary>
        /// Collapses a list of purchased upgrades into compact display strings.
        /// Consecutive numerically-suffixed upgrades sharing the same base ID are merged:
        ///   vineyards1, vineyards2, vineyards3 → "Vineyards 1-3"
        ///   vineyards1, vineyards3             → "Vineyards 1, 3"
        /// Non-numbered upgrades are shown by their display name as-is.
        /// </summary>
        private static string CollapseChainNames(IEnumerable<PurchaseResult> items)
        {
            var numPat = new System.Text.RegularExpressions.Regex(@"^(.+?)(\d+)$");
            var namePat = new System.Text.RegularExpressions.Regex(@"^(.+?)(\d+)$");

            // Annotate each item with its base-ID and numeric suffix
            var annotated = items.Select(r =>
            {
                var m = numPat.Match(r.UpgradeId);
                return new
                {
                    r.UpgradeName,
                    r.UpgradeId,
                    Base = m.Success ? m.Groups[1].Value : r.UpgradeId,
                    Num = m.Success ? int.Parse(m.Groups[2].Value) : (int?)null
                };
            });

            // Group by base ID, preserving first-seen order
            var groups = annotated
                .GroupBy(x => x.Base)
                .OrderBy(g => items.ToList().FindIndex(r => r.UpgradeId.StartsWith(g.Key, OIC)));

            var parts = new List<string>();
            foreach (var g in groups)
            {
                var unnumbered = g.Where(x => x.Num == null).ToList();
                var numbered = g.Where(x => x.Num != null).OrderBy(x => x.Num).ToList();

                foreach (var u in unnumbered)
                    parts.Add(u.UpgradeName);

                if (numbered.Count == 0) continue;
                if (numbered.Count == 1) { parts.Add(numbered[0].UpgradeName); continue; }

                // Derive display base name from the first item's Name (strip trailing digits)
                var nm = namePat.Match(numbered[0].UpgradeName);
                string baseName = nm.Success ? nm.Groups[1].Value.TrimEnd() : numbered[0].UpgradeName;

                // Split into consecutive runs
                var runs = new List<List<int>>();
                var cur = new List<int> { numbered[0].Num!.Value };
                for (int i = 1; i < numbered.Count; i++)
                {
                    if (numbered[i].Num == numbered[i - 1].Num + 1) cur.Add(numbered[i].Num!.Value);
                    else { runs.Add(cur); cur = new List<int> { numbered[i].Num!.Value }; }
                }
                runs.Add(cur);

                var runStrs = runs.Select(r => r.Count == 1 ? r[0].ToString() : $"{r.First()}-{r.Last()}");
                parts.Add($"{baseName} {string.Join(", ", runStrs)}");
            }

            return string.Join(", ", parts);
        }

        // ════════════════════════════════════════════════════════════════════════
        // Name lookups
        // ════════════════════════════════════════════════════════════════════════

        private Settlement FindSettlement(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var settlement = Settlement.All.FirstOrDefault(s => s?.Name?.ToString().Equals(name, OIC) == true);
            if (settlement?.IsVillage == true)
            {
                string englishCastleName = name.Add(" Castle", false);
                settlement = Settlement.All.FirstOrDefault(s => s?.Name?.ToString().Equals(englishCastleName, OIC) == true);
                if (settlement == null)
                {
                    string localizedCastleName = name.Add("{=BLT_UpgradeCastleSuffix} Castle".Translate(), false);
                    settlement = Settlement.All.FirstOrDefault(s => s?.Name?.ToString().Equals(localizedCastleName, OIC) == true);
                }
            }
            return settlement;
        }

        private Clan FindClan(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var clan = Clan.All.FirstOrDefault(c => c?.Name?.ToString().Equals(name, OIC) == true);
            clan ??= Clan.All.FirstOrDefault(c => c?.Name?.ToString().Equals("[BLT Clan]" + name, OIC) == true);
            return clan;
        }

        private Kingdom FindKingdom(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var kingdom = Kingdom.All.FirstOrDefault(k => k?.Name?.ToString().Equals(name, OIC) == true);
            return kingdom;
        }

        // ════════════════════════════════════════════════════════════════════════
        // Remove command (unchanged logic, refactored slightly)
        // ════════════════════════════════════════════════════════════════════════

        private void HandleRemoveCommand(string type, string name, string upgradeId, Hero hero, Settings settings, GlobalCommonConfig gc, Action<string> ok, Action<string> fail)
        {
            switch (type)
            {
                case "fief": RemoveFiefUpgrade(name, upgradeId, hero, settings, gc, ok, fail); break;
                case "clan": RemoveClanUpgrade(upgradeId, hero, settings, gc, ok, fail); break;
                case "kingdom": RemoveKingdomUpgrade(upgradeId, hero, gc, ok, fail); break;
                default: fail("{=BLT_UpgradeInvalidType}Invalid type. Use 'fief', 'clan', or 'kingdom'".Translate()); break;
            }
        }

        private void RemoveFiefUpgrade(string name, string upgradeId, Hero hero, Settings settings, GlobalCommonConfig gc, Action<string> ok, Action<string> fail)
        {
            var settlement = FindSettlement(name);
            if (settlement == null) { fail("{=BLT_UpgradeSettlementNotFound}Settlement '{Name}' not found".Translate(("Name", name))); return; }
            if (settlement.Town == null) { fail("{=BLT_UpgradeTownsCastlesOnly}Only towns and castles can have upgrades".Translate()); return; }

            bool isOwner = settlement.OwnerClan == hero.Clan;
            bool isKingdomLeader = settings.AllowKingdomLeadersForFiefs && hero.Clan?.Kingdom != null && hero.Clan.Kingdom.Leader == hero && settlement.OwnerClan?.Kingdom == hero.Clan.Kingdom;
            if (!isOwner && !isKingdomLeader) { fail("{=BLT_UpgradeNoModifyPermission}You don't have permission to modify {Settlement}".Translate(("Settlement", settlement.Name))); return; }
            if (!hero.IsClanLeader && !isKingdomLeader) { fail("{=BLT_UpgradeRemoveFiefLeaderOnly}Only clan leaders can remove fief upgrades".Translate()); return; }

            var up = gc.FiefUpgrades?.FirstOrDefault(u => u.ID == upgradeId);
            if (up == null) { fail("{=BLT_UpgradeNotFound}Upgrade '{Id}' not found".Translate(("Id", upgradeId))); return; }
            if (!up.CanBeRemoved) { fail("{=BLT_UpgradeCannotRemove}'{Upgrade}' cannot be removed".Translate(("Upgrade", up.Name))); return; }

            if (up.CapitalOnly)
            {
                if (CapitalBehavior.Current?.HasCapitalUpgrade(hero.Clan, upgradeId) != true)
                { fail("{=BLT_UpgradeCapitalNotOwned}{Clan} doesn't have capital upgrade '{Id}'"
                    .Translate(("Clan", hero.Clan.Name), ("Id", upgradeId))); return; }
                CapitalBehavior.Current.RemoveCapitalUpgrade(hero.Clan, upgradeId);
                ok("{=BLT_UpgradeRemovedCapital}Removed capital upgrade '{Upgrade}' from {Clan}!"
                    .Translate(("Upgrade", up.Name), ("Clan", hero.Clan.Name)));
                Log.ShowInformation("{=BLT_UpgradeLogRemovedCapital}{Hero} removed capital upgrade {Upgrade}"
                    .Translate(("Hero", hero.Name), ("Upgrade", up.Name)), hero.CharacterObject, Log.Sound.Notification1);
            }
            else
            {
                if (UpgradeBehavior.Current?.HasFiefUpgrade(settlement, upgradeId) != true)
                { fail("{=BLT_UpgradeNotOwned}{Name} doesn't have this upgrade".Translate(("Name", settlement.Name))); return; }
                UpgradeBehavior.Current?.RemoveFiefUpgrade(settlement, upgradeId);
                ok("{=BLT_UpgradeRemovedFrom}Removed '{Upgrade}' from {Name}!".Translate(("Upgrade", up.Name), ("Name", settlement.Name)));
                Log.ShowInformation("{=BLT_UpgradeLogRemovedFrom}{Hero} removed {Upgrade} from {Name}"
                    .Translate(("Hero", hero.Name), ("Upgrade", up.Name), ("Name", settlement.Name)), hero.CharacterObject, Log.Sound.Notification1);
            }
        }


        private void RemoveClanUpgrade(string upgradeId, Hero hero, Settings settings, GlobalCommonConfig gc, Action<string> ok, Action<string> fail)
        {
            var clan = hero?.Clan;
            if (clan == null) { fail("{=BLT_UpgradeNotInClan}You are not in a clan!".Translate()); return; }
            if (!settings.AllowAnyClanMemberForClanUpgrades && !hero.IsClanLeader) { fail("{=BLT_UpgradeRemoveClanLeaderOnly}Only clan leaders can remove clan upgrades".Translate()); return; }

            var up = gc.ClanUpgrades?.FirstOrDefault(u => u.ID == upgradeId);
            if (up == null) { fail("{=BLT_UpgradeNotFound}Upgrade '{Id}' not found".Translate(("Id", upgradeId))); return; }
            if (!up.CanBeRemoved) { fail("{=BLT_UpgradeCannotRemove}'{Upgrade}' cannot be removed".Translate(("Upgrade", up.Name))); return; }
            if (UpgradeBehavior.Current?.HasClanUpgrade(clan, upgradeId) != true) { fail("{=BLT_UpgradeNotOwned}{Name} doesn't have this upgrade".Translate(("Name", clan.Name))); return; }

            UpgradeBehavior.Current?.RemoveClanUpgrade(clan, upgradeId);
            ok("{=BLT_UpgradeRemovedFrom}Removed '{Upgrade}' from {Name}!".Translate(("Upgrade", up.Name), ("Name", clan.Name)));
            Log.ShowInformation("{=BLT_UpgradeLogRemovedFrom}{Hero} removed {Upgrade} from {Name}"
                .Translate(("Hero", hero.Name), ("Upgrade", up.Name), ("Name", clan.Name)), hero.CharacterObject, Log.Sound.Notification1);
        }

        private void RemoveKingdomUpgrade(string upgradeId, Hero hero, GlobalCommonConfig gc, Action<string> ok, Action<string> fail)
        {
            if (hero.Clan == null) { fail("{=BLT_UpgradeNotInClan}You are not in a clan!".Translate()); return; }
            var kingdom = hero.Clan.Kingdom;
            if (kingdom == null) { fail("{=BLT_UpgradeNotInKingdom}You are not in a kingdom!".Translate()); return; }
            if (kingdom.Leader != hero) { fail("{=BLT_UpgradeRemoveKingdomRulerOnly}Only the kingdom ruler can remove kingdom upgrades".Translate()); return; }

            var up = gc.KingdomUpgrades?.FirstOrDefault(u => u.ID == upgradeId);
            if (up == null) { fail("{=BLT_UpgradeNotFound}Upgrade '{Id}' not found".Translate(("Id", upgradeId))); return; }
            if (!up.CanBeRemoved) { fail("{=BLT_UpgradeCannotRemove}'{Upgrade}' cannot be removed".Translate(("Upgrade", up.Name))); return; }
            if (UpgradeBehavior.Current?.HasKingdomUpgrade(kingdom, upgradeId) != true) { fail("{=BLT_UpgradeNotOwned}{Name} doesn't have this upgrade".Translate(("Name", kingdom.Name))); return; }

            UpgradeBehavior.Current?.RemoveKingdomUpgrade(kingdom, upgradeId);
            ok("{=BLT_UpgradeRemovedFrom}Removed '{Upgrade}' from {Name}!".Translate(("Upgrade", up.Name), ("Name", kingdom.Name)));
            Log.ShowInformation("{=BLT_UpgradeLogRemovedFrom}{Hero} removed {Upgrade} from {Name}"
                .Translate(("Hero", hero.Name), ("Upgrade", up.Name), ("Name", kingdom.Name)), hero.CharacterObject, Log.Sound.Horns2);
        }
    }
}
