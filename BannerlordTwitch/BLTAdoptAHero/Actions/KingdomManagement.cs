using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Annotations;
using BLTAdoptAHero.Actions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.LogEntries;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;
using System.ComponentModel.DataAnnotations;
using BLTAdoptAHero.Behaviors;

namespace BLTAdoptAHero.Actions
{
    [LocDisplayName("{=1yrA4CUf}Kingdom Management"),
     LocDescription("{=4vQgxBGr}Allow viewer to change their clans Kingdom or make leader decisions"),
     UsedImplicitly]
    public class KingdomManagement : HeroCommandHandlerBase
    {
        [CategoryOrder("Join", 0),
         CategoryOrder("Rebel", 1),
         CategoryOrder("Leave", 2),
         CategoryOrder("Create", 3),
         CategoryOrder("Policy", 4),
         //CategoryOrder("Vassal", 5),
         CategoryOrder("Stats", 6),
         CategoryOrder("Armies", 7),
         CategoryOrder("Release", 8),
         CategoryOrder("Expel", 9),
         CategoryOrder("Tax", 10),
         CategoryOrder("Sponsor", 11)]
        private class Settings : IDocumentable
        {
            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Join", "{=q5JhpNMF}Join"),
             LocDescription("{=583Jcer2}Enable joining kingdoms command"),
             PropertyOrder(1), UsedImplicitly]
            public bool JoinEnabled { get; set; } = true;

            [LocDisplayName("{=AI_MaxClans}Max Clans (AI Kingdoms)"),
             LocCategory("Join", "{=q5JhpNMF}Join"),
             LocDescription("{=AI_MaxClans_Desc}Maximum clans for AI-led kingdoms before join is disallowed"),
             PropertyOrder(2), UsedImplicitly]
            public int JoinMaxClansAI { get; set; } = 30;

            [LocDisplayName("{=Player_MaxClans}Max Clans (Player Kingdom)"),
             LocCategory("Join", "{=q5JhpNMF}Join"),
             LocDescription("{=Player_MaxClans_Desc}Maximum clans for player-led kingdom before join is disallowed"),
             PropertyOrder(3), UsedImplicitly]
            public int JoinMaxClansPlayer { get; set; } = 30;

            [LocDisplayName("{=BLT_MaxClans}Max Clans (BLT Kingdoms)"),
             LocCategory("Join", "{=q5JhpNMF}Join"),
             LocDescription("{=BLT_MaxClans_Desc}Maximum clans for BLT/Viewer-led kingdoms before join is disallowed"),
             PropertyOrder(4), UsedImplicitly]
            public int JoinMaxClansBLT { get; set; } = 30;

            [LocDisplayName("{=AI_MaxMercClans}Max Mercenary Clans (AI Kingdoms)"),
             LocCategory("Join", "{=q5JhpNMF}Join"),
             LocDescription("{=AI_MaxMercClans_Desc}Maximum Mercenary clans for AI-led kingdoms before join is disallowed"),
             PropertyOrder(5), UsedImplicitly]
            public int JoinMaxMercClansAI { get; set; } = 5;

            [LocDisplayName("{=Player_MaxMercClans}Max Mercenary Clans (Player Kingdom)"),
             LocCategory("Join", "{=q5JhpNMF}Join"),
             LocDescription("{=Player_MaxMercClans_Desc}Maximum Mercenary clans for player-led kingdom before join is disallowed"),
             PropertyOrder(6), UsedImplicitly]
            public int JoinMaxMercClansPlayer { get; set; } = 3;

            [LocDisplayName("{=BLT_MaxMercClans}Max Mercenary Clans (BLT Kingdoms)"),
             LocCategory("Join", "{=q5JhpNMF}Join"),
             LocDescription("{=BLT_MaxMercClans_Desc}Maximum Mercenary clans for BLT/Viewer-led kingdoms before join is disallowed"),
             PropertyOrder(7), UsedImplicitly]
            public int JoinMaxMercClansBLT { get; set; } = 10;

            // Helper methods to get the appropriate max clans setting
            public int GetMaxClansForKingdom(Kingdom kingdom)
            {
                if (kingdom?.Leader == null)
                    return JoinMaxClansAI;

                if (kingdom.Leader == Hero.MainHero)
                    return JoinMaxClansPlayer;

                if (kingdom.Leader.IsAdopted())
                    return JoinMaxClansBLT;

                return JoinMaxClansAI;
            }
            public int GetMaxMercClansForKingdom(Kingdom kingdom)
            {
                if (kingdom?.Leader == null)
                    return JoinMaxMercClansAI;

                if (kingdom.Leader == Hero.MainHero)
                    return JoinMaxMercClansPlayer;

                if (kingdom.Leader.IsAdopted())
                    return JoinMaxMercClansBLT;

                return JoinMaxMercClansAI;
            }

            [LocDisplayName("{=6PUxQuLg}Gold Cost"),
             LocCategory("Join", "{=q5JhpNMF}Join"),
             LocDescription("{=6fkIuAEC}Cost of joining a kingdom"),
             PropertyOrder(3), UsedImplicitly]
            public int JoinPrice { get; set; } = 150000;

            [LocDisplayName("{=vKsTAxDD}Mercenary"),
             LocCategory("Join", "{=q5JhpNMF}Join"),
             LocDescription("{=pEMiWgjg}!kingdom merc to enter mercenary contract"),
             PropertyOrder(4), UsedImplicitly]
            public bool MercenaryEnabled { get; set; } = true;

            [LocDisplayName("{=VTZ0Wc7R}Mercenary Cost"),
             LocCategory("Join", "{=q5JhpNMF}Join"),
             LocDescription("{=DUgSwHnD}Mercenary contract cost"),
             PropertyOrder(5), UsedImplicitly]
            public int MercPrice { get; set; } = 50000;

            [LocDisplayName("{=BLTKingdomPlayerMercCost}Player Mercenary Cost"),
             LocCategory("Join", "{=q5JhpNMF}Join"),
             LocDescription("{=BLTKingdomPlayerMercCostDescription}Player kingdom mercenary contract cost"),
             PropertyOrder(6), UsedImplicitly]
            public int PlayerMercPrice { get; set; } = 50000;

            [LocDisplayName("{=7KEOBexC}Players Kingdom?"),
             LocCategory("Join", "{=q5JhpNMF}Join"),
             LocDescription("{=7ivCO9JL}Allow viewers to join the players kingdom"),
             PropertyOrder(7), UsedImplicitly]
            public bool JoinAllowPlayer { get; set; } = true;

            [LocDisplayName("{=6PUxQuLg}Gold Cost"),
             LocCategory("Join", "{=q5JhpNMF}Join"),
             LocDescription("{=BLTKingdomPlayerJoinCostDescription}Cost of joining the player's kingdom"),
             PropertyOrder(8), UsedImplicitly]
            public int PlayerJoinPrice { get; set; } = 150000;

            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Rebel", "{=qgKGFYNu}Rebel"),
             LocDescription("{=88BqaM2k}Enable viewer clan rebelling against their kingdom"),
             PropertyOrder(1), UsedImplicitly]
            public bool RebelEnabled { get; set; } = true;

            [LocDisplayName("{=BLTKingdomBLTRebel}BLT Rebel"),
             LocCategory("Rebel", "{=qgKGFYNu}Rebel"),
             LocDescription("{=BLTKingdomBLTRebelDescription}Enable viewer clan rebelling against BLT kingdoms"),
             PropertyOrder(2), UsedImplicitly]
            public bool BLTRebelEnabled { get; set; } = true;

            [LocDisplayName("{=6PUxQuLg}Gold Cost"),
             LocCategory("Rebel", "{=qgKGFYNu}Rebel"),
             LocDescription("{=97hqQyTG}Cost of starting a rebellion"),
             PropertyOrder(3), UsedImplicitly]
            public int RebelPrice { get; set; } = 500000;

            [LocDisplayName("{=BLTKingdomBLTGoldCost}BLT Gold Cost"),
             LocCategory("Rebel", "{=qgKGFYNu}Rebel"),
             LocDescription("{=BLTKingdomBLTRebelCostDescription}Cost of rebelling against BLT kingdoms"),
             PropertyOrder(4), UsedImplicitly]
            public int BLTRebelPrice { get; set; } = 1000000;

            [LocDisplayName("{=9rmGjERc}Minimum Clan Tier"),
             LocCategory("Rebel", "{=qgKGFYNu}Rebel"),
             LocDescription("{=ANLOgDZU}Minimum clan tier to start a rebellion"),
             PropertyOrder(5), UsedImplicitly]
            public int RebelClanTierMinimum { get; set; } = 2;

            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Leave", "{=zG5I9PwG}Leave"),
             LocDescription("{=H0TsFPbu}Enable viewer clan leaving their kingdom"),
             PropertyOrder(1), UsedImplicitly]
            public bool LeaveEnabled { get; set; } = true;

            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Create", "{=BLTKingdomCategoryCreate}Create"),
             LocDescription("{=BLTKingdomCreateDescription}Enable viewer clan to create a kingdom"),
             PropertyOrder(1), UsedImplicitly]
            public bool CreateKEnabled { get; set; } = true;

            [LocDisplayName("{=9rmGjERc}Minimum Clan Tier"),
             LocCategory("Create", "{=BLTKingdomCategoryCreate}Create"),
             LocDescription("{=BLTKingdomCreateTierDescription}Minimum clan tier to create a kingdom"),
             PropertyOrder(2), UsedImplicitly]
            public int CreateKTierMinimum { get; set; } = 3;

            [LocDisplayName("{=BLTKingdomMinimumFiefs}Minimum Clan Fiefs"),
             LocCategory("Create", "{=BLTKingdomCategoryCreate}Create"),
             LocDescription("{=BLTKingdomMinimumFiefsDescription}Minimum clan fiefs to create a kingdom"),
             PropertyOrder(3), UsedImplicitly]
            public int CreateKFiefMinimum { get; set; } = 2;

            [LocDisplayName("{=6PUxQuLg}Gold Cost"),
             LocCategory("Create", "{=BLTKingdomCategoryCreate}Create"),
             LocDescription("{=BLTKingdomCreateCostDescription}Cost of creating a kingdom"),
             PropertyOrder(4), UsedImplicitly]
            public int CreateKPrice { get; set; } = 20000000;

            [LocDisplayName("{=BLTKingdomCategoryPolicy}Policy"),
             LocCategory("Policy", "{=BLTKingdomCategoryPolicy}Policy"),
             LocDescription("{=BLTKingdomPolicyDescription}Enable viewing, adding and removing policies"),
             PropertyOrder(1), UsedImplicitly]
            public bool PolicyEnabled { get; set; } = true;

            [LocDisplayName("{=BLTKingdomPrice}Price"),
             LocCategory("Policy", "{=BLTKingdomCategoryPolicy}Policy"),
             LocDescription("{=BLTKingdomPolicyPriceDescription}Policy command price"),
             PropertyOrder(2), UsedImplicitly]
            public int PolicyPrice { get; set; } = 50000;

            //[LocDisplayName("{=pYjIUlTE}Enabled"),
            // LocCategory("Vassal", "{=TESTING}Vassal"),
            // LocDescription("{=TESTING}Enable viewer create vassal"),
            // PropertyOrder(1), UsedImplicitly]
            //public bool VassalEnabled { get; set; } = true;

            //[LocDisplayName("{=BLT_MaxVassals}Max vassal"),
            // LocCategory("Vassal", "{=TESTING}Vassal"),
            // LocDescription("{=BLT_MaxVassalsDesc}Max vassal clans"),
            // PropertyOrder(2), UsedImplicitly]
            //public int VassalAmount { get; set; } = 3;

            //[LocDisplayName("{=6PUxQuLg}Gold Cost"),
            // LocCategory("Vassal", "{=TESTING}Vassal"),
            // LocDescription("{=TESTING}Cost of creating a vassal clan"),
            // PropertyOrder(3), UsedImplicitly]
            //public int VassalPrice { get; set; } = 250000;

            //[LocDisplayName("{=TESTING}Vassal Merc Income Share %"),
            // LocCategory("Vassal", "{=TESTING}Vassal"),
            // LocDescription("{=TESTING}Percentage of vassal mercenary income shared with master (0.0 - 2.0, 0.25 = 25%)"),
            // PropertyOrder(4), UsedImplicitly,
            // Range(0f, 2f)]
            //public float VassalMercIncomeShare { get; set; } = 0.25f; // 25% default

            //[LocDisplayName("{=TESTING}Vassal Fief Income Share %"),
            // LocCategory("Vassal", "{=TESTING}Vassal"),
            // LocDescription("{=TESTING}Percentage of vassal fief income shared with master (0.0 - 2.0, 0.25 = 25%)"),
            // PropertyOrder(5), UsedImplicitly,
            // Range(0f, 2f)]
            //public float VassalFiefIncomeShare { get; set; } = 0.25f; // 25% default

            //[LocDisplayName("{=TESTING}King Vassals Only"),
            // LocCategory("Vassal", "{=TESTING}Vassal"),
            // LocDescription("{=TESTING}Prevents anyone except kings to create vassal clans"),
            // PropertyOrder(6), UsedImplicitly]
            //public bool KingVassalsOnly { get; set; } = false;

            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Stats", "{=rTee27gM}Stats"),
             LocDescription("{=CFBJIpux}Enable stats command"),
             PropertyOrder(1), UsedImplicitly]
            public bool StatsEnabled { get; set; } = true;

            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Armies", "{=BLTKingdomCategoryArmies}Armies"),
             LocDescription("{=BLTKingdomArmiesDescription}Enable armies command"),
             PropertyOrder(1), UsedImplicitly]
            public bool ArmiesEnabled { get; set; } = true;

            [LocDisplayName("{=BLTKingdomAllowBLTArmies}Allow BLT Armies Default"),
             LocCategory("Armies", "{=BLTKingdomCategoryArmies}Armies"),
             LocDescription("{=BLTKingdomAllowBLTArmiesDescription}Default state when a BLT-led kingdom is created: allow BLT adopted heroes to create armies"),
             PropertyOrder(2), UsedImplicitly]
            public bool ArmiesAllowBLTDefault { get; set; } = true;

            [LocDisplayName("{=BLTKingdomAllowAIArmies}Allow AI Armies Default"),
             LocCategory("Armies", "{=BLTKingdomCategoryArmies}Armies"),
             LocDescription("{=BLTKingdomAllowAIArmiesDescription}Default state when a BLT-led kingdom is created: allow AI heroes to create armies"),
             PropertyOrder(3), UsedImplicitly]
            public bool ArmiesAllowAIDefault { get; set; } = true;

            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Release", "{=BLTKingdomCategoryRelease}Release"),
             LocDescription("{=BLTKingdomReleaseDescription}Enable king to release clans from kingdom (with their land)"),
             PropertyOrder(1), UsedImplicitly]
            public bool ReleaseEnabled { get; set; } = true;

            [LocDisplayName("{=6PUxQuLg}Gold Cost"),
             LocCategory("Release", "{=BLTKingdomCategoryRelease}Release"),
             LocDescription("{=BLTKingdomReleaseCostDescription}Cost for king to release a clan"),
             PropertyOrder(2), UsedImplicitly]
            public int ReleasePrice { get; set; } = 50000;

            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Expel", "{=BLTKingdomCategoryExpel}Expel"),
             LocDescription("{=BLTKingdomExpelDescription}Enable king to expel clans from kingdom (takes their land first)"),
             PropertyOrder(1), UsedImplicitly]
            public bool ExpelEnabled { get; set; } = true;

            [LocDisplayName("{=6PUxQuLg}Gold Cost"),
             LocCategory("Expel", "{=BLTKingdomCategoryExpel}Expel"),
             LocDescription("{=BLTKingdomExpelCostDescription}Cost for king to expel a clan"),
             PropertyOrder(2), UsedImplicitly]
            public int ExpelPrice { get; set; } = 100000;

            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Tax", "{=BLTKingdomCategoryTax}Tax"),
             LocDescription("{=BLTKingdomTaxDescription}Enable kingdom taxation system"),
             PropertyOrder(1), UsedImplicitly]
            public bool TaxEnabled { get; set; } = true;

            [LocDisplayName("{=BLTKingdomMinimumTaxRate}Minimum Tax Rate %"),
             LocCategory("Tax", "{=BLTKingdomCategoryTax}Tax"),
             LocDescription("{=BLTKingdomMinimumTaxRateDescription}Minimum tax rate kings can set (0-100)"),
             PropertyOrder(2), UsedImplicitly,
             Range(0f, 100f)]
            public float MinTaxRate { get; set; } = 0f;

            [LocDisplayName("{=BLTKingdomMaximumTaxRate}Maximum Tax Rate %"),
             LocCategory("Tax", "{=BLTKingdomCategoryTax}Tax"),
             LocDescription("{=BLTKingdomMaximumTaxRateDescription}Maximum tax rate kings can set (0-100)"),
             PropertyOrder(3), UsedImplicitly,
             Range(0f, 100f)]
            public float MaxTaxRate { get; set; } = 50f;

            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Sponsor", "{=BLTKingdomCategorySponsor}Sponsor"),
             LocDescription("{=BLTKingdomSponsorDescription}Enable the sponsor command (buy influence for gold)"),
             PropertyOrder(1), UsedImplicitly]
            public bool SponsorEnabled { get; set; } = true;

            [LocDisplayName("{=BLTKingdomGoldPerInfluence}Gold Per Influence"),
             LocCategory("Sponsor", "{=BLTKingdomCategorySponsor}Sponsor"),
             LocDescription("{=BLTKingdomGoldPerInfluenceDescription}Gold cost per 1 influence point purchased"),
             PropertyOrder(2), UsedImplicitly]
            public int SponsorGoldPerInfluence { get; set; } = 1000;

            [LocDisplayName("{=BLTKingdomKingCut}King Cut %"),
             LocCategory("Sponsor", "{=BLTKingdomCategorySponsor}Sponsor"),
             LocDescription("{=BLTKingdomKingCutDescription}Percentage of gold spent that is forwarded to the kingdom leader (0.0 - 1.0, 0.25 = 25%)"),
             PropertyOrder(3), UsedImplicitly,
             Range(0f, 1f)]
            public float SponsorKingCutPercent { get; set; } = 0.25f;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                var enabledCommands = new List<string>();

                if (JoinEnabled)
                    enabledCommands.Add("{=BLTKingdomSubJoin}join".Translate());
                if (MercenaryEnabled)
                    enabledCommands.Add("{=BLTKingdomSubMerc}merc".Translate());
                if (RebelEnabled)
                    enabledCommands.Add("{=BLTKingdomSubRebel}rebel".Translate());
                if (LeaveEnabled)
                    enabledCommands.Add("{=BLTKingdomSubLeave}leave".Translate());
                if (CreateKEnabled)
                    enabledCommands.Add("{=BLTKingdomSubCreate}create".Translate());
                //if (VassalEnabled)
                //    EnabledCommands.Append("Vassal, ");
                if (StatsEnabled)
                    enabledCommands.Add("{=BLTKingdomSubStats}stats".Translate());
                if (ArmiesEnabled)
                    enabledCommands.Add("{=BLTKingdomSubArmies}armies".Translate());
                if (ReleaseEnabled)
                    enabledCommands.Add("{=BLTKingdomSubRelease}release".Translate());
                if (ExpelEnabled)
                    enabledCommands.Add("{=BLTKingdomSubExpel}expel".Translate());
                if (TaxEnabled)
                    enabledCommands.Add("{=BLTKingdomSubTax}tax".Translate());
                if (SponsorEnabled)
                    enabledCommands.Add("{=BLTKingdomSubSponsor}sponsor".Translate());
                if (PolicyEnabled)
                    enabledCommands.Add("{=BLTKingdomSubPolicy}policy".Translate());

                if (enabledCommands.Count > 0)
                    generator.Value("{=BLTKingdomDocsEnabled}<strong>Enabled Commands:</strong> {commands}".Translate(("commands", string.Join(", ", enabledCommands))));

                if (JoinEnabled)
                    generator.Value("{=BLTKingdomDocsJoinHeading}<strong>Join Config: </strong>".Translate() +
                                    "{=BLTKingdomDocsJoin}Max AI Kingdom Clans={maxClans}, Max Player Kingdom Clans={maxPlayerClans}, Max BLT Kingdom Clans={maxBLTClans}, Max AI Kingdom Mercenary Clans={maxMercClans}, Max Player Kingdom Mercenary Clans={maxPlayerMercClans}, Max BLT Kingdom Mercenary Clans={maxBLTMercClans}, Price={price}{icon}, Allow Join Players Kingdom?={allowPlayer}, Player Kingdom Price={playerPrice}{icon}".Translate(("maxClans", JoinMaxClansAI), ("maxPlayerClans", JoinMaxClansPlayer), ("maxBLTClans", JoinMaxClansBLT), ("maxMercClans", JoinMaxMercClansAI), ("maxPlayerMercClans", JoinMaxMercClansPlayer), ("maxBLTMercClans", JoinMaxMercClansBLT), ("price", JoinPrice), ("icon", Naming.Gold), ("allowPlayer", JoinAllowPlayer), ("playerPrice", PlayerJoinPrice)));
                if (MercenaryEnabled)
                    generator.Value("{=BLTKingdomDocsMercHeading}<strong>Mercenary: </strong>".Translate() +
                                    "{=BLTKingdomDocsMerc}Price={price}{icon}, Player Kingdom Price={playerPrice}{icon}".Translate(("price", MercPrice), ("playerPrice", PlayerMercPrice), ("icon", Naming.Gold)));

                if (RebelEnabled)
                    generator.Value("{=BLTKingdomDocsRebelHeading}<strong>Rebel Config: </strong>".Translate() +
                                    "{=BLTKingdomDocsRebel}Price={price}{icon}, Allow Rebelling from BLT Kingdom?={allowBLT}, From BLT Kingdom Price={bltPrice}{icon}, Minimum Clan Tier={tier}".Translate(("price", RebelPrice), ("icon", Naming.Gold), ("allowBLT", BLTRebelEnabled), ("bltPrice", BLTRebelPrice), ("tier", RebelClanTierMinimum)));
                if (CreateKEnabled)
                    generator.Value("{=BLTKingdomDocsCreateHeading}<strong>Create Config: </strong>".Translate() +
                                    "{=BLTKingdomDocsCreate}Price={price}{icon}, Minimum Clan Tier={tier}, Minimum Fiefs Amount={count}".Translate(("price", CreateKPrice), ("icon", Naming.Gold), ("tier", CreateKTierMinimum), ("count", CreateKFiefMinimum)));
                //if (VassalEnabled)
                //    generator.Value("<strong>Vassal: </strong>" +
                //                    $"Only Kings can make Vassals: {KingVassalsOnly}, " +
                //                    $"Max Vassals: {VassalAmount}, " +
                //                    $"Price={VassalPrice.ToString()}{Naming.Gold}" +
                //                    $"Percent of Vassal's Mercenary Income given to Parent: " +
                //                    $"{(int)(VassalMercIncomeShare * 100)}%, " + 
                //                    $"Percent of Vassal's Fief Income given to Parent: " + 
                //                    $"{(int)(VassalFiefIncomeShare * 100)}%");
                if (ReleaseEnabled)
                    generator.Value("{=BLTKingdomDocsRelease}<strong>Release:</strong> Price={price}{icon}".Translate(("price", ReleasePrice), ("icon", Naming.Gold)));
                if (ExpelEnabled)
                    generator.Value("{=BLTKingdomDocsExpel}<strong>Expel:</strong> Price={price}{icon}".Translate(("price", ExpelPrice), ("icon", Naming.Gold)));
                if (TaxEnabled)
                    generator.Value("{=BLTKingdomDocsTax}<strong>Tax:</strong> Min Rate={min}%, Max Rate={max}%".Translate(("min", MinTaxRate), ("max", MaxTaxRate)));
                if (SponsorEnabled)
                    generator.Value("{=BLTKingdomDocsSponsor}<strong>Sponsor:</strong> Gold Per Influence={price}{icon}, King Cut={cut}%".Translate(("price", SponsorGoldPerInfluence), ("icon", Naming.Gold), ("cut", (SponsorKingCutPercent * 100f).ToString("F0"))));
            }
        }
        public override Type HandlerConfigType => typeof(Settings);
        

        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config, Action<string> onSuccess, Action<string> onFailure)
        {
            if (config is not Settings settings) return;
            // Set vassal mercenary income share percentage
            //if (VassalBehavior.Current != null)
            //{
            //    VassalBehavior.MercenaryIncomeSharePercent = settings.VassalMercIncomeShare;
            //    VassalBehavior.FiefIncomeSharePercent = settings.VassalFiefIncomeShare;
            //}
            if (adoptedHero == null)
            {
                onFailure(AdoptAHero.NoHeroMessage);
                return;
            }
            if (Mission.Current != null)
            {
                onFailure("{=CRCwDnag}You cannot manage your kingdom, as a mission is active!".Translate());
                return;
            }
            if (adoptedHero.HeroState == Hero.CharacterStates.Prisoner)
            {
                onFailure("{=Cjm2sCjR}You cannot manage your kingdom, as you are a prisoner!".Translate());
                return;
            }
            if (adoptedHero.Clan == null)
            {
                onFailure("{=DYgac2Ut}You cannot manage your kingdom, as you are not in a clan".Translate());
                return;
            }

            if (context.Args.IsEmpty())
            {
                if (adoptedHero.Clan.Kingdom == null)
                {
                    onFailure("{=EJ4Pd2Lg}Your clan is not in a Kingdom".Translate());
                    return;
                }
                onSuccess("{=EkmpJvML}Your clan {clanName} is a member of the kingdom {kingdomName}".Translate(("clanName", adoptedHero.Clan.Name.ToString()), ("kingdomName", adoptedHero.Clan.Kingdom.Name.ToString())));
                return;
            }

            var splitArgs = context.Args.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var command = GetKingdomCommand(splitArgs[0]);
            var desiredName = string.Join(" ", splitArgs.Skip(1)).Trim();

            switch (command)
            {
                case "join":
                    HandleJoinCommand(settings, adoptedHero, desiredName, onSuccess, onFailure);
                    break;
                case "merc":
                    HandleMercenaryCommand(settings, adoptedHero, desiredName, onSuccess, onFailure);
                    break;
                case "rebel":
                    HandleRebelCommand(settings, adoptedHero, onSuccess, onFailure);
                    break;
                case "leave":
                    HandleLeaveCommand(settings, adoptedHero, onSuccess, onFailure);
                    break;
                case "create":
                    HandleKCreateCommand(settings, adoptedHero, desiredName, onSuccess, onFailure);
                    break;
                //case "vassal":
                //    HandleVassalCommand(settings, adoptedHero, desiredName, onSuccess, onFailure);
                        break;
                case "release":
                    HandleReleaseCommand(settings, adoptedHero, desiredName, onSuccess, onFailure);
                    break;
                case "expel":
                    HandleExpelCommand(settings, adoptedHero, desiredName, onSuccess, onFailure);
                    break;
                case "stats":
                    HandleStatsCommand(settings, adoptedHero, onSuccess, onFailure);
                    break;
                case "armies":
                    HandleArmiesCommand(settings, adoptedHero, desiredName, onSuccess, onFailure);
                    break;
                case "tax":
                    HandleTaxCommand(settings, adoptedHero, desiredName, onSuccess, onFailure);
                    break;
                case "sponsor":
                    HandleSponsorCommand(settings, adoptedHero, desiredName, onSuccess, onFailure);
                    break;
                case "policy":
                    HandlePolicyCommand(settings, adoptedHero, desiredName, onSuccess, onFailure);
                    break;
                default:
                    onFailure("{=FFxXuX5i}Invalid or empty kingdom action, try (join/merc/rebel/leave/create/release/expel/stats/armies/tax/sponsor/policy)".Translate());
                    break;
            }

        }

        private static string GetKingdomCommand(string command)
        {
            if (MatchesCommand(command, "{=BLTKingdomSubJoin}join".Translate(), "join")) return "join";
            if (MatchesCommand(command, "{=BLTKingdomSubMerc}merc".Translate(), "merc")) return "merc";
            if (MatchesCommand(command, "{=BLTKingdomSubRebel}rebel".Translate(), "rebel")) return "rebel";
            if (MatchesCommand(command, "{=BLTKingdomSubLeave}leave".Translate(), "leave")) return "leave";
            if (MatchesCommand(command, "{=BLTKingdomSubCreate}create".Translate(), "create")) return "create";
            if (MatchesCommand(command, "{=BLTKingdomSubRelease}release".Translate(), "release")) return "release";
            if (MatchesCommand(command, "{=BLTKingdomSubExpel}expel".Translate(), "expel")) return "expel";
            if (MatchesCommand(command, "{=BLTKingdomSubStats}stats".Translate(), "stats")) return "stats";
            if (MatchesCommand(command, "{=BLTKingdomSubArmies}armies".Translate(), "armies")) return "armies";
            if (MatchesCommand(command, "{=BLTKingdomSubTax}tax".Translate(), "tax")) return "tax";
            if (MatchesCommand(command, "{=BLTKingdomSubSponsor}sponsor".Translate(), "sponsor")) return "sponsor";
            if (MatchesCommand(command, "{=BLTKingdomSubPolicy}policy".Translate(), "policy")) return "policy";
            return command.ToLowerInvariant();
        }

        private static bool MatchesCommand(string value, string localized, string english)
        {
            return value.Equals(localized, StringComparison.OrdinalIgnoreCase)
                || value.Equals(english, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetArmyPermissionCommand(string command)
        {
            if (MatchesCommand(command, "{=BLTKingdomArmyAllowBLT}allowblt".Translate(), "allowblt")) return "allowblt";
            if (MatchesCommand(command, "{=BLTKingdomArmyAllowAI}allowai".Translate(), "allowai")) return "allowai";
            return command.ToLowerInvariant();
        }

        private static string GetToggleCommand(string command)
        {
            if (MatchesCommand(command, "{=BLTKingdomToggleOn}on".Translate(), "on")) return "on";
            if (MatchesCommand(command, "{=BLTKingdomToggleOff}off".Translate(), "off")) return "off";
            return command.ToLowerInvariant();
        }

        private static bool IsPolicyListCommand(string command)
        {
            return MatchesCommand(command, "{=BLTKingdomPolicyList}list".Translate(), "list");
        }

        private void HandleJoinCommand(Settings settings, Hero adoptedHero, string desiredName, Action<string> onSuccess, Action<string> onFailure)
        {
            bool joiningPlayer = false;
            if (!settings.JoinEnabled)
            {
                onFailure("{=FHPbdYpk}Joining kingdoms is disabled".Translate());
                return;
            }
            if (adoptedHero.Clan.Kingdom != null)
            {
                onFailure("{=GEGrsLPm}Your clan is already in a kingdom, in order to leave you must rebel against them".Translate());
                return;
            }
            if (!adoptedHero.IsClanLeader)
            {
                onFailure("{=HS14GdUa}You cannot manage your kingdom, as you are not your clans leader!".Translate());
                return;
            }
            if (string.IsNullOrWhiteSpace(desiredName))
            {
                onFailure("{=IKXbDYU8}(join) (kingdom name)".Translate());
                return;
            }
            var desiredKingdom = CampaignHelpers.AllHeroes.Select(h => h?.Clan?.Kingdom).Distinct().FirstOrDefault(c => c?.Name.ToString().Equals(desiredName, StringComparison.OrdinalIgnoreCase) == true);
            if (desiredKingdom == null)
            {
                onFailure("{=JdZ2CelP}Could not find the kingdom with the name {name}".Translate(("name", desiredName)));
                return;
            }
            
            var diplomacyHelper = Campaign.Current.GetCampaignBehavior<BLTDiplomacyHelper>();
            bool hassharedwar = false;
            if (!desiredKingdom.IsAtWarWith(adoptedHero.Clan))
            {
                foreach (Kingdom k in Kingdom.All.ToList())
                {
                    if (desiredKingdom.IsAtWarWith(k) && adoptedHero.Clan.IsAtWarWith(k))
                    {
                        hassharedwar = true;
                        break;
                    }
                }
            }
            else
            {
                hassharedwar = false;
            }

            if (diplomacyHelper.IsPeaceBlocked(adoptedHero.Clan, desiredKingdom) && !hassharedwar)
            {
                onFailure("{=BLTKingdomRebellionBlocked}Rebellion is blocked".Translate());
                return;
            }
            int maxClans = settings.GetMaxClansForKingdom(desiredKingdom) + UpgradeBehavior.Current.GetTotalKingdomMaxClansBonus(desiredKingdom);
            int currentClans = desiredKingdom.Clans.Where(c => !VassalBehavior.Current.IsVassal(c) && !c.IsUnderMercenaryService).Count();

            if (currentClans >= maxClans)
            {
                onFailure("{=BLTKingdomFull}The kingdom {name} is full ({current}/{max} clans)".Translate(("name", desiredName), ("current", currentClans), ("max", maxClans)));
                return;
            }

            if (desiredKingdom == Hero.MainHero.Clan?.Kingdom && Hero.MainHero.IsKingdomLeader && !settings.JoinAllowPlayer)
            {
                onFailure("{=L4dccNIC}Joining the players kingdom is disabled".Translate());
                return;
            }
            else if (desiredKingdom == Hero.MainHero.Clan.Kingdom && Hero.MainHero.IsKingdomLeader && settings.JoinAllowPlayer)
            {
                joiningPlayer = true;
            }
            if (!joiningPlayer && BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.JoinPrice)
            {
                onFailure(Naming.NotEnoughGold(settings.JoinPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                return;
            }
            else if (joiningPlayer && BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.PlayerJoinPrice)
            {
                onFailure(Naming.NotEnoughGold(settings.PlayerJoinPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                return;
            }
            AdoptedHeroFlags._allowKingdomMove = true;
            if (joiningPlayer)
            {
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.PlayerJoinPrice, true);
            }
            else
            {
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.JoinPrice, true);
            }
            ChangeKingdomAction.ApplyByJoinToKingdom(adoptedHero.Clan, desiredKingdom);
            if (adoptedHero.Clan.Kingdom == null)
                adoptedHero.Clan.Kingdom = desiredKingdom;
            if (adoptedHero.Clan.Fiefs.Count == 0)
            {
                adoptedHero.Clan.ConsiderAndUpdateHomeSettlement();
                foreach (Hero hero in adoptedHero.Clan.Heroes)
                {
                    hero.UpdateHomeSettlement();
                }
            }
                
            adoptedHero.Clan.CalculateMidSettlement();
            

            onSuccess("{=LSea9bms}Your clan {clanName} has joined the kingdom {kingdomName}".Translate(("clanName", adoptedHero.Clan.Name.ToString()), ("kingdomName", adoptedHero.Clan.Kingdom.Name.ToString())));
            Log.ShowInformation("{=Lid1aV3k}{clanName} has joined kingdom {kingdomName}!".Translate(("clanName", adoptedHero.Clan.Name.ToString()), ("kingdomName", adoptedHero.Clan.Kingdom.Name.ToString())), adoptedHero.CharacterObject, Log.Sound.Horns2);
            AdoptedHeroFlags._allowKingdomMove = false;
        }

        private void HandleRebelCommand(Settings settings, Hero adoptedHero, Action<string> onSuccess, Action<string> onFailure)
        {
            bool BLTRebellion = false;
            if (!settings.RebelEnabled)
            {
                onFailure("{=MRstTtQa}Clan rebellion is disabled".Translate());
                return;
            }
            if (adoptedHero.Clan.Kingdom == null)
            {
                onFailure("{=NbvwN9z3}Your clan is not in a kingdom".Translate());
                return;
            }
            if (!adoptedHero.IsClanLeader)
            {
                onFailure("{=Nzm5bI4I}You cannot lead a rebellion against your kingdom, as you are not your clans leader!".Translate());
                return;
            }
            if (adoptedHero.Clan == adoptedHero.Clan.Kingdom.RulingClan)
            {
                onFailure("{=OgwKEDza}You already are the ruling clan".Translate());
                return;
            }
            if (adoptedHero.Clan.IsUnderMercenaryService)
            {
                onFailure("{=Py6VMkK6}Your clan is mercenary".Translate());
                return;
            }
            if (adoptedHero.Clan.Tier < settings.RebelClanTierMinimum)
            {
                onFailure("{=Ok94bnhi}Your clan is not high enough tier to rebel".Translate());
                return;
            }
            if (adoptedHero.Clan.Kingdom.RulingClan.Leader.IsAdopted())
            {
                BLTRebellion = true;
            }
            if (BLTRebellion && !settings.BLTRebelEnabled)
            {
                onFailure("{=BLTKingdomBLTRebellionDisabled}Rebelling from BLT-owned kingdoms is disabled!".Translate());
                return;
            }
            
            if (!BLTRebellion)
            {
                if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.RebelPrice)
                {
                    onFailure(Naming.NotEnoughGold(settings.RebelPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                    return;
                }
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.RebelPrice, true);
            }
            else
            {
                if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.BLTRebelPrice)
                {
                    onFailure(Naming.NotEnoughGold(settings.BLTRebelPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                    return;
                }
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.BLTRebelPrice, true);
            }
            AdoptedHeroFlags._allowKingdomMove = true;
            IFaction oldBoss = adoptedHero.Clan.Kingdom;
            adoptedHero.Clan.ClanLeaveKingdom();
            DeclareWarAction.ApplyByRebellion(oldBoss, adoptedHero.Clan);
            FactionManager.DeclareWar(adoptedHero.Clan, oldBoss);
            onSuccess("{=PHuBl5tJ}Your clan has rebelled against {oldBoss} and declared war".Translate(("oldBoss", oldBoss)));
            AdoptedHeroFlags._allowKingdomMove = false;
            return;
        }

        private void HandleStatsCommand(Settings settings, Hero adoptedHero, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.StatsEnabled)
            {
                onFailure("{=RtwwHrgB}Kingdom stats is disabled".Translate());
                return;
            }
            if (adoptedHero.Clan.Kingdom == null)
            {
                onFailure("{=RvkJO6J9}Your clan is not in a kingdom".Translate());
                return;
            }
            TradeAgreementsCampaignBehavior tradeBehavior = Campaign.Current.GetCampaignBehavior<TradeAgreementsCampaignBehavior>();
            bool war = false;
            bool ally = adoptedHero.Clan.Kingdom.AlliedKingdoms.Count > 0;
            bool trade = false;
            bool tribute = false;
            TextObject warList = new TextObject("");
            TextObject tributeList = new TextObject("");
            TextObject tradeList = new TextObject("");
            foreach (Kingdom k in Kingdom.All)
            {
                if (adoptedHero.Clan.Kingdom == k)
                    continue;

                StanceLink stance = adoptedHero.Clan.Kingdom.GetStanceWith(k);
                if (tradeBehavior.HasTradeAgreement(adoptedHero.Clan.Kingdom, k, out _))
                {
                    var tradeDate = tradeBehavior.GetTradeAgreementEndDate(adoptedHero.Clan.Kingdom, k);
                    int tradeDays = (int)(tradeDate - CampaignTime.Now).ToDays;
                    trade = true;
                    tradeList.Value += k.Name.Value + $"({tradeDays}), ";
                }
                if (adoptedHero.Clan.Kingdom.IsAtWarWith(k))
                {
                    war = true;
                    warList.Value += k.Name.Value + ":" + ((int)k.CurrentTotalStrength).ToString() + ", ";
                }
                else
                {
                    int dailyTributeFromUs = stance.GetDailyTributeToPay(adoptedHero.Clan.Kingdom);
                    int dailyTributeFromThem = stance.GetDailyTributeToPay(k);
                    int daysUs = k.GetStanceWith(adoptedHero.Clan.Kingdom).GetRemainingTributePaymentCount();
                    int daysThem = stance.GetRemainingTributePaymentCount();


                    if (dailyTributeFromUs > 0)
                    {
                        tribute = true;
                        tributeList.Value +=
                            $"{k.Name}:-{dailyTributeFromUs}({daysUs}), ";
                    }
                    else if (dailyTributeFromThem > 0)
                    {
                        tribute = true;
                        tributeList.Value +=
                            $"{k.Name}:+{dailyTributeFromThem}({daysThem}), ";
                    }
                }
            }
            warList.Value = warList.Value.TrimEnd(',', ' ');
            var allyList = string.Join(", ", adoptedHero.Clan.Kingdom.AlliedKingdoms.Select(k => k.Name.ToString()));
            tradeList.Value = tradeList.Value.TrimEnd(',', ' ');
            tributeList.Value = tributeList.Value.TrimEnd(',', ' ');

            var clanStats = new StringBuilder();
            clanStats.Append("{=SVlrGgol}Kingdom Name: {name} | ".Translate(("name", adoptedHero.Clan.Kingdom.Name.ToString())));
            clanStats.Append("{=Ss588M9l}Ruling Clan: {rulingClan} | ".Translate(("rulingClan", adoptedHero.Clan.Kingdom.RulingClan.Name.ToString())));
            clanStats.Append("{=T1FhhCH9}Clan Count: {clanCount} | ".Translate(("clanCount", adoptedHero.Clan.Kingdom.Clans.Count.ToString())));
            clanStats.Append("{=TUOmh7NY}Strength: {strength} | ".Translate(("strength", Math.Round(adoptedHero.Clan.Kingdom.CurrentTotalStrength).ToString())));
            if (war)
                clanStats.Append("{=QadZnUKh}Wars: {wars} | ".Translate(("wars", warList.ToString())));
            if (ally)
                clanStats.Append("{=BLTKingdomStatsAlliances}Alliances: {allies} | ".Translate(("allies", allyList)));
            if (trade)
                clanStats.Append("{=BLTKingdomStatsTrades}Trades: {trade} | ".Translate(("trade", tradeList.ToString())));
            if (tribute)
                clanStats.Append("{=0GhTvF3K}Tribute: {tribute} | ".Translate(("tribute", tributeList.ToString())));
            if (adoptedHero.Clan.Kingdom.RulingClan.HomeSettlement.Name != null)
                clanStats.Append("{=EXKsUpaU}Capital: {capital} ".Translate(("capital", adoptedHero.Clan.Kingdom.RulingClan.HomeSettlement.Name.ToString())));
            if (adoptedHero.Clan.Kingdom.Armies.Count >= 1)
            {
                clanStats.Append("{=BLTKingdomStatsArmies}Armies: {count} | ".Translate(("count", adoptedHero.Clan.Kingdom.Armies.Count)));
            }
            if (adoptedHero.Clan.Kingdom.Fiefs.Count >= 1)
            {
                int townCount = 0;
                int castleCount = 0;
                foreach (var settlement in adoptedHero.Clan.Kingdom.Fiefs)
                {
                    if (!settlement.IsCastle)
                    {
                        townCount++;
                    }
                    if (settlement.IsCastle)
                    {
                        castleCount++;
                    }
                }
                clanStats.Append("{=BwuFSJU1}| Towns: {towns} | ".Translate(("towns", (object)townCount)));
                clanStats.Append("{=0rMNNQ7R}Castles: {castles}".Translate(("castles", (object)castleCount)));
            }
            onSuccess("{stats}".Translate(("stats", clanStats.ToString())));
        }

        private void HandleArmiesCommand(Settings settings, Hero adoptedHero, string args, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.ArmiesEnabled)
            {
                onFailure("{=BLTKingdomArmiesDisabled}Kingdom armies are disabled".Translate());
                return;
            }
            if (adoptedHero.Clan.Kingdom == null)
            {
                onFailure("{=RvkJO6J9}Your clan is not in a kingdom".Translate());
                return;
            }

            // sub-commands: allowblt on/off   allowai on/off   (anything else = status report)
            var parts = args?.Split(' ') ?? Array.Empty<string>();
            var sub = parts.Length > 0 ? GetArmyPermissionCommand(parts[0]) : "";
            var val = parts.Length > 1 ? GetToggleCommand(parts[1]) : "";

            bool isKing = adoptedHero.Clan.Kingdom.Leader == adoptedHero;

            // ── Toggle: allowblt ────────────────────────────────────────────────────
            if (sub == "allowblt" || sub == "allowai")
            {
                if (!isKing)
                {
                    onFailure("{=BLTKingdomArmyLeaderRequired}You must be the kingdom leader to change army permissions".Translate());
                    return;
                }
                if (val != "on" && val != "off")
                {
                    onFailure("{=BLTKingdomArmyUsage}Usage: !kingdom armies (allowblt/allowai) (on/off)".Translate());
                    return;
                }

                bool allow = val == "on";
                var pb = PartyOrderBehavior.Current;
                if (pb == null)
                {
                    onFailure("{=BLTKingdomPartyOrdersUnavailable}Party order system is not initialized".Translate());
                    return;
                }

                if (sub == "allowblt")
                {
                    pb.SetBLTArmiesBlocked(adoptedHero.Clan.Kingdom, !allow);
                    onSuccess("{=BLTKingdomBLTArmyPermission}{kingdom}: BLT army creation is now {state}".Translate(("kingdom", adoptedHero.Clan.Kingdom.Name), ("state", allow ? "{=BLTKingdomAllowed}ALLOWED".Translate() : "{=BLTKingdomBlocked}BLOCKED".Translate())));
                }
                else // allowai
                {
                    pb.SetAIArmiesBlocked(adoptedHero.Clan.Kingdom, !allow);
                    onSuccess("{=BLTKingdomAIArmyPermission}{kingdom}: AI army creation is now {state}".Translate(("kingdom", adoptedHero.Clan.Kingdom.Name), ("state", allow ? "{=BLTKingdomAllowed}ALLOWED".Translate() : "{=BLTKingdomBlocked}BLOCKED".Translate())));
                }
                return;
            }

            // ── Default: status report ───────────────────────────────────────────────
            var armies = new StringBuilder();
            armies.Append("{=SVlrGgol}Kingdom Name: {name} | ".Translate(("name", adoptedHero.Clan.Kingdom.Name.ToString())));
            armies.Append("{=BLTKingdomArmyCount}{count} Armies | ".Translate(("count", adoptedHero.Clan.Kingdom.Armies.Count)));

            if (isKing && PartyOrderBehavior.Current != null)
            {
                bool bltBlocked = PartyOrderBehavior.Current.IsBLTArmiesBlocked(adoptedHero.Clan.Kingdom);
                bool aiBlocked = PartyOrderBehavior.Current.IsAIArmiesBlocked(adoptedHero.Clan.Kingdom);
                armies.Append("{=BLTKingdomBLTArmiesState}BLT armies: {state} | ".Translate(("state", bltBlocked ? "{=BLTKingdomBlocked}BLOCKED".Translate() : "{=BLTKingdomAllowed}ALLOWED".Translate())));
                armies.Append("{=BLTKingdomAIArmiesState}AI armies: {state} | ".Translate(("state", aiBlocked ? "{=BLTKingdomBlocked}BLOCKED".Translate() : "{=BLTKingdomAllowed}ALLOWED".Translate())));
            }

            if (adoptedHero.Clan.Kingdom.Armies.Count >= 1)
            {
                foreach (Army army in adoptedHero.Clan.Kingdom.Armies.ToList())
                {
                    armies.Append("{=BLTKingdomArmyName}\nArmy: {name} | ".Translate(("name", army.Name)));
                    armies.Append("{=BLTKingdomArmyStrength}{strength} Strength | ".Translate(("strength", (int)army.CalculateCurrentStrength())));
                    armies.Append("{=BLTKingdomArmyTroops}{troops} Troops | ".Translate(("troops", army.TotalHealthyMembers)));
                    armies.Append("{=BLTKingdomArmyParties}{parties} Parties | ".Translate(("parties", army.LeaderPartyAndAttachedPartiesCount)));
                    if (!string.IsNullOrEmpty(army?.LeaderParty?.GetBehaviorText()?.ToString()))
                        armies.Append("{=BLTKingdomArmyBehaviour}Behaviour: {behaviour} | ".Translate(("behaviour", army.LeaderParty.GetBehaviorText())));
                    if (army.LeaderParty.TargetParty != null || army.LeaderParty.ShortTermTargetParty != null)
                        armies.Append("{=BLTKingdomArmyTarget}Target: {target} | ".Translate(("target", army.LeaderParty.ShortTermTargetParty ?? army.LeaderParty.TargetParty)));
                }
            }

            onSuccess("{armies}".Translate(("armies", armies.ToString().TrimEnd(' ', '|', ' '))));
        }

        private void HandleLeaveCommand(Settings settings, Hero adoptedHero, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.LeaveEnabled)
            {
                onFailure("{=ozTfk7uB}Kingdom leaving is disabled".Translate());
                return;
            }
            if (adoptedHero.Clan.Kingdom == null)
            {
                onFailure("{=RvkJO6J9}Your clan is not in a kingdom".Translate());
                return;
            }
            if (!adoptedHero.IsClanLeader)
            {
                onFailure("{=PSmxb52U}You cannot leave your kingdom, as you are not your clans leader!".Translate());
                return;
            }
            if (adoptedHero.Clan == adoptedHero.Clan.Kingdom.RulingClan)
            {
                onFailure("{=BLTKingdomRulerCannotLeave}You are the ruling clan; transfer all kingdom fiefs to another clan before disbanding your kingdom".Translate());
                return;
            }
            IFaction oldBoss = adoptedHero.Clan.Kingdom;
            if (adoptedHero.Clan.IsUnderMercenaryService)
            {
                adoptedHero.Clan.EndMercenaryService(true);
                adoptedHero.Clan.ClanLeaveKingdom(true);
                onSuccess("{=BLTKingdomMercenaryContractEnded}Your clan has ended its mercenary contract".Translate());
                return;
            }
            AdoptedHeroFlags._allowKingdomMove = true;
            foreach (var fief in adoptedHero.Clan.Settlements.ToList())
            {
                Hero ruler = adoptedHero.Clan.Kingdom?.RulingClan?.Leader;
                if (ruler != null && ruler != adoptedHero)
                {
                    ChangeOwnerOfSettlementAction.ApplyByDefault(ruler, fief);
                }
            }
            onSuccess("{=sc77IxCW}Your clan has left {oldBoss}".Translate(("oldBoss", oldBoss)));
            adoptedHero.Clan.ClanLeaveKingdom();
            AdoptedHeroFlags._allowKingdomMove = false;
            if (VassalBehavior.Current != null)
            {
                VassalBehavior.Current.OnClanChangedKingdom(adoptedHero.Clan, (Kingdom)oldBoss, null, ChangeKingdomAction.ChangeKingdomActionDetail.LeaveKingdom, false);
            }
            return;
        }

        private void HandleMercenaryCommand(Settings settings, Hero adoptedHero, string desiredName, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.MercenaryEnabled)
            {
                onFailure("{=aSIP2AKk}Mercenary is disabled".Translate());
                return;
            }
            if (adoptedHero.Clan.Kingdom != null && adoptedHero.Clan.IsUnderMercenaryService)
            {
                onFailure("{=7nEJJGzL}Already a mercenary!".Translate());
                return;
            }
            if (adoptedHero.Clan.Kingdom != null)
            {
                onFailure("{=BLTKingdomMercenaryAlreadyInKingdom}Your clan is already in a kingdom, leave first".Translate());
                return;
            }
            if (!adoptedHero.IsClanLeader)
            {
                onFailure("{=HS14GdUa}You cannot manage your kingdom, as you are not your clans leader!".Translate());
                return;
            }
            if (string.IsNullOrWhiteSpace(desiredName))
            {
                onFailure("{=ETfJQatX}(merc) (kingdom name)".Translate());
                return;
            }

            bool mercforPlayer = false;
            var desiredKingdom = CampaignHelpers.AllHeroes.Select(h => h?.Clan?.Kingdom).Distinct().FirstOrDefault(c => c?.Name.ToString().Equals(desiredName, StringComparison.OrdinalIgnoreCase) == true);
            if (desiredKingdom == null)
            {
                onFailure("{=JdZ2CelP}Could not find the kingdom with the name {name}".Translate(("name", desiredName)));
                return;
            }

            int maxMercClans = settings.GetMaxMercClansForKingdom(desiredKingdom) + UpgradeBehavior.Current.GetTotalKingdomMaxMercClansBonus(desiredKingdom);
            int currentMercClans = desiredKingdom.Clans.Where(c => !VassalBehavior.Current.IsVassal(c) && c.IsUnderMercenaryService).Count();

            if (currentMercClans >= maxMercClans)
            {
                onFailure("{=BLTKingdomMercenaryFull}The kingdom {name} is full ({current}/{max} mercenary clans)".Translate(("name", desiredName), ("current", currentMercClans), ("max", maxMercClans)));
                return;
            }
            var diplomacyHelper = Campaign.Current.GetCampaignBehavior<BLTDiplomacyHelper>();
            if (diplomacyHelper.IsPeaceBlocked(adoptedHero.Clan, desiredKingdom))
            {
                onFailure("{=BLTKingdomRebellionBlocked}Rebellion is blocked".Translate());
                return;
            }
            if (desiredKingdom == Hero.MainHero.Clan.Kingdom && Hero.MainHero.Clan == Hero.MainHero.Clan.Kingdom.RulingClan && !settings.JoinAllowPlayer)
            {
                onFailure("{=L4dccNIC}Joining the players kingdom is disabled".Translate());
                return;
            }
            else if (desiredKingdom == Hero.MainHero.Clan.Kingdom && Hero.MainHero.Clan == Hero.MainHero.Clan.Kingdom.RulingClan)
            {
                mercforPlayer = true;
            }
            if (!mercforPlayer && BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.MercPrice)
            {
                onFailure(Naming.NotEnoughGold(settings.MercPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                return;
            }
            else if (mercforPlayer && BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.PlayerMercPrice)
            {
                onFailure(Naming.NotEnoughGold(settings.PlayerMercPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                return;
            }
            
            if (!mercforPlayer)
            {
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.MercPrice, true);
            }
            else
            {
                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.PlayerMercPrice, true);
            }
            ChangeKingdomAction.ApplyByJoinFactionAsMercenary(adoptedHero.Clan, desiredKingdom);

            adoptedHero.Clan.ConsiderAndUpdateHomeSettlement();
            foreach (Hero hero in adoptedHero.Clan.Heroes)
            {
                hero.UpdateHomeSettlement();
            }
            adoptedHero.Clan.CalculateMidSettlement();
            Log.ShowInformation("{=tpwW6Ix8}{clanName} is now under contract with {kingdomName}!".Translate(("clanName", adoptedHero.Clan.Name.ToString()), ("kingdomName", adoptedHero.Clan.Kingdom.Name.ToString())), adoptedHero.CharacterObject, Log.Sound.Horns2);
        }

        private void HandleKCreateCommand(Settings settings, Hero adoptedHero, string desiredName, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.CreateKEnabled)
            {
                onFailure("{=BLTKingdomCreationDisabled}Kingdom creation is disabled".Translate());
                return;
            }
            if (adoptedHero.Clan.Fiefs.Count < settings.CreateKFiefMinimum)
            {
                onFailure("{=BLTKingdomNotEnoughFiefs}Not enough fiefs ({current}/{required})".Translate(("current", adoptedHero.Clan.Fiefs.Count), ("required", settings.CreateKFiefMinimum)));
                return;
            }
            if (adoptedHero.Clan.Tier < settings.CreateKTierMinimum)
            {
                onFailure("{=BLTKingdomTierTooLow}Your clan is not high enough tier to create a kingdom".Translate());
                return;
            }

            if (adoptedHero.Clan.Kingdom != null)
            {
                onFailure("{=GEGrsLPm}Your clan is already in a kingdom, in order to leave you must rebel against them".Translate());
                return;
            }
            if (!adoptedHero.IsClanLeader)
            {
                onFailure("{=HS14GdUa}You cannot manage your kingdom, as you are not your clans leader!".Translate());
                return;
            }
            if (string.IsNullOrWhiteSpace(desiredName))
            {
                onFailure("{=BLTKingdomCreateUsage}(create) (kingdom name)".Translate());
                return;
            }
            var existingKingdom = CampaignHelpers.AllHeroes.Select(h => h?.Clan?.Kingdom).Distinct().FirstOrDefault(c => c?.Name.ToString().Equals(desiredName, StringComparison.OrdinalIgnoreCase) == true);
            if (existingKingdom != null)
            {
                onFailure("{=BLTKingdomNameExists}A kingdom with the name {name} already exists".Translate(("name", desiredName)));
                return;
            }
            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.CreateKPrice)
            {
                onFailure(Naming.NotEnoughGold(settings.CreateKPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                return;
            }
            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.CreateKPrice, true);

            var creator = Campaign.Current.KingdomManager;
            var culture = adoptedHero.Culture;
            creator.CreateKingdom(new TextObject(desiredName), new TextObject(desiredName), culture, adoptedHero.Clan, null, null, null, null);
            var newKingdom = adoptedHero.Clan.Kingdom;
            newKingdom.KingdomBudgetWallet = 2000000;
            adoptedHero.Clan.Influence = 2000;
            adoptedHero.Clan.Kingdom.Banner = adoptedHero.Clan.Banner;
            adoptedHero.Clan.Kingdom.Banner.ChangeBackgroundColor(adoptedHero.Clan.Banner.GetPrimaryColor(), adoptedHero.Clan.Banner.GetSecondaryColor());

            onSuccess("{=BLTKingdomCreated}Created kingdom {name}".Translate(("name", desiredName)));
            Log.ShowInformation("{=BLTKingdomFounded}{heroName} has founded kingdom {kingdom}!".Translate(("heroName", adoptedHero.Name.ToString()), ("kingdom", adoptedHero.Clan.Kingdom.Name.ToString())), adoptedHero.CharacterObject, Log.Sound.Horns2);
        }

        //private void HandleVassalCommand(Settings settings, Hero adoptedHero, string args, Action<string> onSuccess, Action<string> onFailure)
        //{
        //    if (!settings.VassalEnabled)
        //    {
        //        onFailure("Vassal creation is disabled");
        //        return;
        //    }

        //    var splitargs = args.Split(' ');
        //    var childName = splitargs[0];
        //    var setname = string.Join(" ", splitargs.Skip(1)).Trim();
        //    if (settings.KingVassalsOnly && adoptedHero.Clan.Kingdom.Leader != adoptedHero)
        //    {
        //        onFailure("{=GEGrsLPm}You must be a king to create vassals".Translate());
        //        return;
        //    }
        //    if (adoptedHero.Clan.Kingdom == null)
        //    {
        //        onFailure("{=RvkJO6J9}Your clan is not in a kingdom".Translate());
        //        return;
        //    }
        //    if (!adoptedHero.IsClanLeader)
        //    {
        //        onFailure("{=HS14GdUa}You cannot manage your kingdom, as you are not your clans leader!".Translate());
        //        return;
        //    }
        //    if (string.IsNullOrWhiteSpace(childName) || string.IsNullOrWhiteSpace(setname))
        //    {
        //        onFailure("{=ETfJQatX}Usage: (vassal) (hero name) (clan name)".Translate());
        //        return;
        //    }
        //    var existingClan = Clan.All.FirstOrDefault(c => c.Name.ToString().ToLower() == setname.ToLower() || c.Name.ToString().ToLower() == $"[vassal] {setname.ToLower()}" || c.Name.ToString().ToLower() == $"[blt clan] {setname.ToLower()}");
        //    if (existingClan != null)
        //    {
        //        onFailure("{=TESTING}A clan with the name {name} already exists".Translate(("name", setname)));
        //        return;
        //    }
        //    if (VassalBehavior.Current.GetVassalClans(adoptedHero.Clan).Count >= (settings.VassalAmount + UpgradeBehavior.Current.GetTotalMaxVassalsBonus(adoptedHero.Clan)))
        //    {
        //        onFailure($"Max vassals: {settings.VassalAmount + UpgradeBehavior.Current.GetTotalMaxVassalsBonus(adoptedHero.Clan)}");
        //        return;
        //    }
        //    if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.VassalPrice)
        //    {
        //        onFailure(Naming.NotEnoughGold(settings.VassalPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
        //        return;
        //    }


        //    Hero vassal = adoptedHero.Clan.Heroes.Find(h => h.FirstName.ToString().ToLower() == childName.ToLower());

        //    if (vassal == null)
        //    {
        //        onFailure($"No hero named {childName}");
        //        return;
        //    }
        //    if (vassal.Age < 18)
        //    {
        //        onFailure($"{childName} is too young");
        //        return;
        //    }
        //    if (vassal.Spouse != null && vassal.Spouse.IsAdopted())
        //    {
        //        onFailure("Cannot vassal a blt spouse");
        //        return;
        //    }
        //    if (vassal.IsAdopted())
        //    {
        //        onFailure("Cannot vassal a blt");
        //        return;                  
        //    }
        //    if (vassal.IsPrisoner)
        //    {
        //        onFailure($"{childName} is prisoner");
        //        return;
        //    }
        //    if (vassal.HeroState == Hero.CharacterStates.Fugitive || vassal.HeroState == Hero.CharacterStates.Released || vassal.HeroState == Hero.CharacterStates.Traveling)
        //    {
        //        onFailure($"{childName} is busy");
        //        return;
        //    }
        //    if (vassal.Spouse == null)
        //    {
        //        HeroFeatures.SpawnSpouse(vassal, vassal.Culture);
        //    }
        //    if (vassal.GovernorOf != null)
        //    {
        //        ChangeGovernorAction.RemoveGovernorOf(vassal);
        //    }
        //    if (vassal.PartyBelongedTo != null)
        //    {
        //        var oldParty = vassal.PartyBelongedTo;
        //        bool wasLeader = oldParty.LeaderHero == vassal;
        //        oldParty.MemberRoster.RemoveTroop(vassal.CharacterObject, 1, default(UniqueTroopDescriptor), 0);
        //        MakeHeroFugitiveAction.Apply(vassal, false);
        //        if (wasLeader && oldParty.IsLordParty)
        //            DisbandPartyAction.StartDisband(oldParty);
        //    }
        //    var fullClanName = $"[Vassal] {setname}";
        //    var newClan = Clan.CreateClan(fullClanName);
        //    newClan.ChangeClanName(new TextObject(fullClanName), new TextObject(fullClanName));
        //    newClan.Culture = vassal.Culture;
        //    newClan.Banner = Banner.CreateOneColoredBannerWithOneIcon(adoptedHero.Clan.Banner.GetPrimaryColor(), adoptedHero.Clan.Banner.GetFirstIconColor(), -1);
        //    newClan.SetInitialHomeSettlement(Settlement.All.SelectRandom());
        //    vassal.Clan = newClan;
        //    if (vassal.Spouse != null)
        //    {
        //        if (vassal.Spouse.GovernorOf != null)
        //        {
        //            ChangeGovernorAction.RemoveGovernorOf(vassal.Spouse);
        //        }
        //        if (vassal.Spouse.PartyBelongedTo != null)
        //        {
        //            var oldParty = vassal.Spouse.PartyBelongedTo;
        //            bool wasLeader = oldParty.LeaderHero == vassal.Spouse;
        //            oldParty.MemberRoster.RemoveTroop(vassal.Spouse.CharacterObject, 1, default(UniqueTroopDescriptor), 0);
        //            MakeHeroFugitiveAction.Apply(vassal.Spouse, false);
        //            if (wasLeader && oldParty.IsLordParty)
        //                DisbandPartyAction.StartDisband(oldParty);
        //        }
        //        vassal.Spouse.Clan = newClan;
        //    }
        //    if (vassal.Children.Count > 0)
        //    {
        //        foreach (Hero child in vassal.Children)
        //        {
        //            if (child.GovernorOf != null)
        //            {
        //                ChangeGovernorAction.RemoveGovernorOf(child);
        //            }
        //            if (child.PartyBelongedTo != null)
        //            {
        //                var oldParty = child.PartyBelongedTo;
        //                bool wasLeader = oldParty.LeaderHero == child;
        //                oldParty.MemberRoster.RemoveTroop(child.CharacterObject, 1, default(UniqueTroopDescriptor), 0);
        //                MakeHeroFugitiveAction.Apply(child, false);
        //                if (wasLeader && oldParty.IsLordParty)
        //                    DisbandPartyAction.StartDisband(oldParty);
        //            }
        //            child.Clan = newClan;
        //        }
        //    }
        //    var tierModel = Campaign.Current.Models.ClanTierModel;
        //    newClan.AddRenown(tierModel.GetRequiredRenownForTier(tierModel.CompanionToLordClanStartingTier));
        //    newClan.SetLeader(vassal);
        //    newClan.IsNoble = true;
        //    vassal.Gold += 50000;
        //    if (adoptedHero.Clan.Kingdom != null)
        //    {
        //        AdoptedHeroFlags._allowKingdomMove = true;
        //        if (adoptedHero.Clan.IsUnderMercenaryService)
        //            ChangeKingdomAction.ApplyByJoinFactionAsMercenary(newClan, adoptedHero.Clan.Kingdom);
        //        else
        //            ChangeKingdomAction.ApplyByJoinToKingdom(newClan, adoptedHero.Clan.Kingdom);
        //        AdoptedHeroFlags._allowKingdomMove = false;
        //    }
        //    CampaignEventDispatcher.Instance.OnClanCreated(newClan, false);
        //    ChangeRelationAction.ApplyRelationChangeBetweenHeroes(adoptedHero, vassal, 100, false);

        //    // Register the vassal with the VassalBehavior
        //    VassalBehavior.Current?.RegisterVassal(newClan, adoptedHero.Clan);

        //    BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.VassalPrice, true);
        //    string response = $"Vassal created by {adoptedHero.FirstName}: {newClan.Name}";
        //    Log.LogFeedResponse(response);
        //}
        private void HandleReleaseCommand(Settings settings, Hero adoptedHero, string targetName, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.ReleaseEnabled)
            {
                onFailure("{=BLTKingdomReleaseDisabled}Release is disabled".Translate());
                return;
            }
            if (adoptedHero.Clan.Kingdom == null || adoptedHero.Clan.Kingdom.Leader != adoptedHero)
            {
                onFailure("{=BLTKingdomReleaseLeaderRequired}You must be the kingdom leader to release clans".Translate());
                return;
            }
            if (string.IsNullOrWhiteSpace(targetName))
            {
                onFailure("{=BLTKingdomReleaseUsage}Usage: (release) (hero name or clan name)".Translate());
                return;
            }
            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.ReleasePrice)
            {
                onFailure(Naming.NotEnoughGold(settings.ReleasePrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                return;
            }

            // Find the target clan by searching for clan name or leader name with possible prefixes/suffixes
            Clan targetClan = null;
            var possiblePrefixes = new[] { "", "[Vassal] ", "[BLT Clan] " };
            var possibleSuffixes = new[] { "", " [BLT]", " [DEV]" };

            // Search by clan name with prefixes
            foreach (var prefix in possiblePrefixes)
            {
                var searchName = prefix + targetName;
                targetClan = adoptedHero.Clan.Kingdom.Clans
                    .FirstOrDefault(c => c.Name.ToString().Equals(searchName, StringComparison.OrdinalIgnoreCase));

                if (targetClan != null) break;
            }

            // If not found, search by leader name with suffixes
            if (targetClan == null)
            {
                foreach (var suffix in possibleSuffixes)
                {
                    var searchName = targetName + suffix;
                    targetClan = adoptedHero.Clan.Kingdom.Clans
                        .FirstOrDefault(c => c.Leader?.FirstName.ToString().Equals(searchName, StringComparison.OrdinalIgnoreCase) == true);

                    if (targetClan != null) break;
                }
            }

            if (targetClan == null)
            {
                onFailure("{=BLTKingdomTargetNotFound}Could not find clan or hero named {name} in your kingdom".Translate(("name", targetName)));
                return;
            }

            if (targetClan == adoptedHero.Clan)
            {
                onFailure("{=BLTKingdomReleaseOwnClan}You cannot release your own clan".Translate());
                return;
            }
            if (targetClan.Kingdom != adoptedHero.Clan.Kingdom)
            {
                onFailure("{=BLTKingdomClanNotMember}{clan} is not in your kingdom".Translate(("clan", targetClan.Name)));
                return;
            }

            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.ReleasePrice, true);

            // Release vassals first
            if (VassalBehavior.Current != null)
            {
                var vassals = VassalBehavior.Current.GetVassalClans(targetClan).ToList();
                foreach (var vassal in vassals)
                {
                    AdoptedHeroFlags._allowKingdomMove = true;
                    vassal.ClanLeaveKingdom();
                    AdoptedHeroFlags._allowKingdomMove = false;
                    VassalBehavior.Current.OnClanChangedKingdom(vassal, adoptedHero.Clan.Kingdom, null, ChangeKingdomAction.ChangeKingdomActionDetail.LeaveKingdom, false);
                }
            }

            // Release the main clan
            AdoptedHeroFlags._allowKingdomMove = true;
            if (targetClan.IsUnderMercenaryService)
            {
                targetClan.EndMercenaryService(true);
            }
            targetClan.ClanLeaveKingdom();
            AdoptedHeroFlags._allowKingdomMove = false;

            if (VassalBehavior.Current != null)
            {
                VassalBehavior.Current.OnClanChangedKingdom(targetClan, adoptedHero.Clan.Kingdom, null, ChangeKingdomAction.ChangeKingdomActionDetail.LeaveKingdom, false);
            }

            onSuccess("{=BLTKingdomReleased}Released {clan} from {kingdom} with all their lands".Translate(("clan", targetClan.Name), ("kingdom", adoptedHero.Clan.Kingdom.Name)));
            Log.ShowInformation("{=BLTKingdomReleasedLog}{hero} has released {clan} from {kingdom}!".Translate(("hero", adoptedHero.Name), ("clan", targetClan.Name), ("kingdom", adoptedHero.Clan.Kingdom.Name)), adoptedHero.CharacterObject, Log.Sound.Horns2);
        }

        private void HandleExpelCommand(Settings settings, Hero adoptedHero, string targetName, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.ExpelEnabled)
            {
                onFailure("{=BLTKingdomExpelDisabled}Expel is disabled".Translate());
                return;
            }
            if (adoptedHero.Clan.Kingdom == null || adoptedHero.Clan.Kingdom.Leader != adoptedHero)
            {
                onFailure("{=BLTKingdomExpelLeaderRequired}You must be the kingdom leader to expel clans".Translate());
                return;
            }
            if (string.IsNullOrWhiteSpace(targetName))
            {
                onFailure("{=BLTKingdomExpelUsage}Usage: (expel) (hero name or clan name)".Translate());
                return;
            }
            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.ExpelPrice)
            {
                onFailure(Naming.NotEnoughGold(settings.ExpelPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                return;
            }

            // Find the target clan by searching for clan name or leader name with possible prefixes/suffixes
            Clan targetClan = null;
            var possiblePrefixes = new[] { "", "[Vassal] ", "[BLT Clan] " };
            var possibleSuffixes = new[] { "", " [BLT]", " [DEV]" };

            // Search by clan name with prefixes
            foreach (var prefix in possiblePrefixes)
            {
                var searchName = prefix + targetName;
                targetClan = adoptedHero.Clan.Kingdom.Clans
                    .FirstOrDefault(c => c.Name.ToString().Equals(searchName, StringComparison.OrdinalIgnoreCase));

                if (targetClan != null) break;
            }

            // If not found, search by leader name with suffixes
            if (targetClan == null)
            {
                foreach (var suffix in possibleSuffixes)
                {
                    var searchName = targetName + suffix;
                    targetClan = adoptedHero.Clan.Kingdom.Clans
                        .FirstOrDefault(c => c.Leader?.FirstName.ToString().Equals(searchName, StringComparison.OrdinalIgnoreCase) == true);

                    if (targetClan != null) break;
                }
            }

            if (targetClan == null)
            {
                onFailure("{=BLTKingdomTargetNotFound}Could not find clan or hero named {name} in your kingdom".Translate(("name", targetName)));
                return;
            }

            if (targetClan == adoptedHero.Clan)
            {
                onFailure("{=BLTKingdomExpelOwnClan}You cannot expel your own clan".Translate());
                return;
            }
            if (targetClan.Kingdom != adoptedHero.Clan.Kingdom)
            {
                onFailure("{=BLTKingdomClanNotMember}{clan} is not in your kingdom".Translate(("clan", targetClan.Name)));
                return;
            }

            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.ExpelPrice, true);

            // Transfer all fiefs from vassals first
            if (VassalBehavior.Current != null)
            {
                var vassals = VassalBehavior.Current.GetVassalClans(targetClan).ToList();
                foreach (var vassal in vassals)
                {
                    AdoptedHeroFlags._allowKingdomMove = true;
                    foreach (var fief in vassal.Settlements.ToList())
                    {
                        ChangeOwnerOfSettlementAction.ApplyByDefault(adoptedHero, fief);
                    }
                    vassal.ClanLeaveKingdom();
                    AdoptedHeroFlags._allowKingdomMove = false;
                    VassalBehavior.Current.OnClanChangedKingdom(vassal, adoptedHero.Clan.Kingdom, null, ChangeKingdomAction.ChangeKingdomActionDetail.LeaveKingdom, false);
                }
            }

            // Transfer all fiefs from the main clan to the king
            AdoptedHeroFlags._allowKingdomMove = true;
            foreach (var fief in targetClan.Settlements.ToList())
            {
                ChangeOwnerOfSettlementAction.ApplyByDefault(adoptedHero, fief);
            }

            // Expel the clan
            if (targetClan.IsUnderMercenaryService)
            {
                targetClan.EndMercenaryService(true);
            }
            targetClan.ClanLeaveKingdom();
            AdoptedHeroFlags._allowKingdomMove = false;

            if (VassalBehavior.Current != null)
            {
                VassalBehavior.Current.OnClanChangedKingdom(targetClan, adoptedHero.Clan.Kingdom, null, ChangeKingdomAction.ChangeKingdomActionDetail.LeaveKingdom, false);
            }

            onSuccess("{=BLTKingdomExpelled}Expelled {clan} from {kingdom} and seized all their lands".Translate(("clan", targetClan.Name), ("kingdom", adoptedHero.Clan.Kingdom.Name)));
            Log.ShowInformation("{=BLTKingdomExpelledLog}{hero} has expelled {clan} from {kingdom}!".Translate(("hero", adoptedHero.Name), ("clan", targetClan.Name), ("kingdom", adoptedHero.Clan.Kingdom.Name)), adoptedHero.CharacterObject, Log.Sound.Horns2);
        }
        private void HandleTaxCommand(Settings settings, Hero adoptedHero, string args, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.TaxEnabled)
            {
                onFailure("{=BLTKingdomTaxDisabled}Kingdom taxation is disabled".Translate());
                return;
            }

            if (adoptedHero.Clan.Kingdom == null)
            {
                onFailure("{=BLTKingdomTaxMembershipRequired}You need to be in a kingdom to view tax rates".Translate());
                return;
            }

            if (KingdomTaxBehavior.Current == null)
            {
                onFailure("{=BLTKingdomTaxUnavailable}Tax system is not initialized".Translate());
                return;
            }

            bool isKing = adoptedHero.Clan.Kingdom.Leader == adoptedHero;
            float currentRate = KingdomTaxBehavior.Current.GetKingdomTaxRate(adoptedHero.Clan.Kingdom);

            // If not king, just show the tax rate
            if (!isKing)
            {
                onSuccess("{=BLTKingdomTaxRate}{kingdom} has a tax rate of {rate}%".Translate(("kingdom", adoptedHero.Clan.Kingdom.Name), ("rate", (currentRate * 100f).ToString("F1"))));
                return;
            }

            // King functionality
            // If no args, show current tax rate and instructions
            if (string.IsNullOrWhiteSpace(args))
            {
                onSuccess("{=BLTKingdomTaxInfo}Current tax rate: {rate}% | Range: {min}%-{max}% | Usage: !kingdom tax <rate>".Translate(("rate", (currentRate * 100f).ToString("F1")), ("min", settings.MinTaxRate), ("max", settings.MaxTaxRate)));
                return;
            }

            // Parse the tax rate
            if (!float.TryParse(args, out float newRate))
            {
                onFailure("{=BLTKingdomTaxUsage}Invalid tax rate. Usage: !kingdom tax <rate> (e.g., !kingdom tax 15 for 15%)".Translate());
                return;
            }

            // Validate range
            if (newRate < settings.MinTaxRate || newRate > settings.MaxTaxRate)
            {
                onFailure("{=BLTKingdomTaxRange}Tax rate must be between {min}% and {max}%".Translate(("min", settings.MinTaxRate), ("max", settings.MaxTaxRate)));
                return;
            }

            // Set the new tax rate (convert percentage to decimal)
            float taxRateDecimal = newRate / 100f;
            KingdomTaxBehavior.Current.SetKingdomTaxRate(adoptedHero.Clan.Kingdom, taxRateDecimal);

            onSuccess("{=BLTKingdomTaxSet}Set {kingdom} tax rate to {rate}%".Translate(("kingdom", adoptedHero.Clan.Kingdom.Name), ("rate", newRate.ToString("F1"))));
            Log.ShowInformation("{=BLTKingdomTaxSetLog}{hero} has set {kingdom} tax rate to {rate}%!".Translate(("hero", adoptedHero.Name), ("kingdom", adoptedHero.Clan.Kingdom.Name), ("rate", newRate.ToString("F1"))), adoptedHero.CharacterObject);
        }

        private void HandleSponsorCommand(Settings settings, Hero adoptedHero, string args, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.SponsorEnabled)
            {
                onFailure("{=BLTKingdomSponsorDisabled}Sponsor command is disabled".Translate());
                return;
            }
            if (adoptedHero.Clan.Kingdom == null)
            {
                onFailure("{=BLTKingdomSponsorMembershipRequired}You must be in a kingdom to sponsor".Translate());
                return;
            }
            if (adoptedHero.Clan.IsUnderMercenaryService)
            {
                onFailure("{=BLTKingdomSponsorMercenaryDenied}Mercenary clans cannot use the sponsor command".Translate());
                return;
            }
            if (adoptedHero.Clan.Kingdom.Leader == adoptedHero)
            {
                onFailure("{=BLTKingdomSponsorKingDenied}Kings cannot sponsor their own kingdom — use the tax system instead".Translate());
                return;
            }
            if (!int.TryParse(args, out int influenceAmount) || influenceAmount <= 0)
            {
                onFailure("{=BLTKingdomSponsorUsage}Usage: !kingdom sponsor <amount> — costs {cost}{icon} per influence".Translate(("cost", settings.SponsorGoldPerInfluence), ("icon", Naming.Gold)));
                return;
            }

            int totalCost = influenceAmount * settings.SponsorGoldPerInfluence;
            int heroGold = BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero);

            if (heroGold < totalCost)
            {
                onFailure(Naming.NotEnoughGold(totalCost, heroGold));
                return;
            }

            // Deduct gold and grant influence
            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -totalCost, true);
            adoptedHero.Clan.Influence += influenceAmount;

            // Forward king cut
            Hero king = adoptedHero.Clan.Kingdom.Leader;
            if (king != null && king != adoptedHero && settings.SponsorKingCutPercent > 0f)
            {
                int kingCut = (int)(totalCost * settings.SponsorKingCutPercent);
                if (kingCut > 0)
                {
                    if (king.IsAdopted())
                        BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(king, kingCut, true);
                    else
                        king.Gold += kingCut;
                }
            }

            onSuccess("{=BLTKingdomSponsorSuccess}Bought {influence} influence for {cost}{icon} — King {king} received {cut}{icon}".Translate(("influence", influenceAmount), ("cost", totalCost), ("icon", Naming.Gold), ("king", king.Name), ("cut", (int)(totalCost * settings.SponsorKingCutPercent))));
        }

        private void HandlePolicyCommand(Settings settings, Hero adoptedHero, string desiredName, Action<string> onSuccess, Action<string> onFailure)
        {
            var desiredPolicy = PolicyObject.All.FirstOrDefault(c => c.Name.ToString().IndexOf(desiredName, StringComparison.OrdinalIgnoreCase) >= 0);
            int policyCost = Campaign.Current.Models.DiplomacyModel.GetInfluenceCostOfPolicyProposalAndDisavowal(adoptedHero.Clan);
            if (!settings.PolicyEnabled)
            {
                onFailure("{=BLTKingdomPolicyDisabled}Policy command is disabled".Translate());
                return;
            }
            if (!adoptedHero.IsKingdomLeader)
            {
                onFailure("{=BLTKingdomPolicyKingRequired}You must be the kingdom leader to manage policies".Translate());
                return;
            }

            if (IsPolicyListCommand(desiredName))
            {
                var listString = string.Join(", ", PolicyObject.All.Select(k => k.Name.ToString()));
                onSuccess(listString);
                return;
            }
            if (string.IsNullOrEmpty(desiredName))
            {
                var listString = string.Join(", ", adoptedHero.Clan.Kingdom.ActivePolicies.Select(p => p.ToString()));
                onSuccess(listString);
                return;
            }
            if (desiredPolicy != null)
            {
                if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.PolicyPrice)
                {
                    onFailure(Naming.NotEnoughGold(settings.PolicyPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                    return;
                }
                if (adoptedHero.Clan.Influence < policyCost)
                {
                    onFailure("{=BLTKingdomNotEnoughInfluence}Not enough influence: {cost}".Translate(("cost", policyCost)));
                    return;
                }
                if (adoptedHero.Clan.Kingdom.ActivePolicies.Contains(desiredPolicy))
                {
                    adoptedHero.Clan.Kingdom.RemovePolicy(desiredPolicy);
                    onSuccess("{=BLTKingdomPolicyRemoved}Removed policy {policy}".Translate(("policy", desiredPolicy)));
                    adoptedHero.Clan.Influence -= policyCost;
                    return;
                }
                else
                {
                    adoptedHero.Clan.Kingdom.AddPolicy(desiredPolicy);
                    onSuccess("{=BLTKingdomPolicyAdded}Added policy {policy}".Translate(("policy", desiredPolicy)));
                    adoptedHero.Clan.Influence -= policyCost;
                    return;
                }
            }
            else { onFailure("{=BLTKingdomPolicyInvalid}Invalid policy action".Translate()); }
            
        }      
    }
}
