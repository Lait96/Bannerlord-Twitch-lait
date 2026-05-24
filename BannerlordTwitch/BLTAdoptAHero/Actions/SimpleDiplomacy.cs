using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BannerlordTwitch;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Rewards;
using BannerlordTwitch.Util;
using BannerlordTwitch.Helpers;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.BarterSystem.Barterables;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Localization;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;
using BLTAdoptAHero.Actions;

namespace BLTAdoptAHero
{
    [LocDisplayName("{=SimpleDiplomacyCmd}Simple Diplomacy"),
     LocDescription("{=SimpleDiplomacyDesc}Manage your kingdom diplomacy and other actions."),
     UsedImplicitly]
    class SimpleDiplomacy : HeroCommandHandlerBase
    {
        [CategoryOrder("War", 0),
         CategoryOrder("Peace", 1),
         CategoryOrder("Alliance", 2),
         CategoryOrder("Trade", 3),
         CategoryOrder("Policy", 4)]
        private class Settings : IDocumentable
        {
            [LocDisplayName("{=SimpleDiplomacyWar}War"),
             LocCategory("War", "{=SimpleDiplomacyWar}War"),
             LocDescription("{=SimpleDiplomacyWarEnabledDesc}Enable declaring war command"),
             PropertyOrder(1), UsedImplicitly]
            public bool WarEnabled { get; set; } = true;

            [LocDisplayName("{=SimpleDiplomacyPrice}Price"),
             LocCategory("War", "{=SimpleDiplomacyWar}War"),
             LocDescription("{=SimpleDiplomacyWarPriceDesc}War command price"),
             PropertyOrder(2), UsedImplicitly]
            public int WarPrice { get; set; } = 250000;

            [LocDisplayName("{=SimpleDiplomacyCooldown}Cooldown"),
             LocCategory("War", "{=SimpleDiplomacyWar}War"),
             LocDescription("{=SimpleDiplomacyWarCooldownDesc}War cooldown"),
             PropertyOrder(3), UsedImplicitly]
            public int WarCooldown { get; set; } = 20;

            [LocDisplayName("{=SimpleDiplomacyPeace}Peace"),
             LocCategory("Peace", "{=SimpleDiplomacyPeace}Peace"),
             LocDescription("{=SimpleDiplomacyPeaceEnabledDesc}Enable peace command"),
             PropertyOrder(1), UsedImplicitly]
            public bool PeaceEnabled { get; set; } = true;

            [LocDisplayName("{=SimpleDiplomacyPrice}Price"),
             LocCategory("Peace", "{=SimpleDiplomacyPeace}Peace"),
             LocDescription("{=SimpleDiplomacyPeacePriceDesc}Peace command price"),
             PropertyOrder(2), UsedImplicitly]
            public int PeacePrice { get; set; } = 100000;

            [LocDisplayName("{=SimpleDiplomacyAlly}Ally"),
             LocCategory("Alliance", "{=SimpleDiplomacyAlliance}Alliance"),
             LocDescription("{=SimpleDiplomacyAllianceEnabledDesc}Enable alliance command"),
             PropertyOrder(1), UsedImplicitly]
            public bool AllyEnabled { get; set; } = true;

            [LocDisplayName("{=SimpleDiplomacyPrice}Price"),
             LocCategory("Alliance", "{=SimpleDiplomacyAlliance}Alliance"),
             LocDescription("{=SimpleDiplomacyAllyPriceDesc}Ally command price"),
             PropertyOrder(2), UsedImplicitly]
            public int AllyPrice { get; set; } = 100000;

            [LocDisplayName("{=SimpleDiplomacyTrade}Trade"),
             LocCategory("Trade", "{=SimpleDiplomacyTrade}Trade"),
             LocDescription("{=SimpleDiplomacyTradeEnabledDesc}Enable trade alliance command"),
             PropertyOrder(1), UsedImplicitly]
            public bool TradeEnabled { get; set; } = true;

            [LocDisplayName("{=SimpleDiplomacyPrice}Price"),
             LocCategory("Trade", "{=SimpleDiplomacyTrade}Trade"),
             LocDescription("{=SimpleDiplomacyTradePriceDesc}Trade command price"),
             PropertyOrder(2), UsedImplicitly]
            public int TradePrice { get; set; } = 50000;

            [LocDisplayName("{=SimpleDiplomacyPolicy}Policy"),
             LocCategory("Policy", "{=SimpleDiplomacyPolicy}Policy"),
             LocDescription("{=SimpleDiplomacyPolicyEnabledDesc}Enable viewing,adding and removing policies"),
             PropertyOrder(1), UsedImplicitly]
            public bool PolicyEnabled { get; set; } = true;

            [LocDisplayName("{=SimpleDiplomacyPrice}Price"),
             LocCategory("Policy", "{=SimpleDiplomacyPolicy}Policy"),
             LocDescription("{=SimpleDiplomacyPolicyPriceDesc}Policy command price"),
             PropertyOrder(2), UsedImplicitly]
            public int PolicyPrice { get; set; } = 50000;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                var sb = new StringBuilder();
                if (WarEnabled) sb.Append("{=SimpleDiplomacyWar}War, ".Translate());
                if (PeaceEnabled) sb.Append("{=SimpleDiplomacyPeace}Peace, ".Translate());
                if (AllyEnabled) sb.Append("{=SimpleDiplomacyAlliance}Alliance, ".Translate());
                if (TradeEnabled) sb.Append("{=SimpleDiplomacyTrade}Trade, ".Translate());
                if (PolicyEnabled) sb.Append("{=SimpleDiplomacyPolicy}Policy".Translate());
                if (sb.Length > 0)
                    generator.Value("<strong>Enabled Commands:</strong> {commands}".Translate(
                        ("commands", sb.ToString(0, sb.Length - 2))));

                if (WarEnabled)
                    generator.Value("<strong>War Config: </strong>" +
                                    "Price={price}{icon}-".Translate(("price", WarPrice.ToString()), ("icon", Naming.Gold)) +
                                    "Cooldown={cd}".Translate(("cd", WarCooldown.ToString())));
                if (PeaceEnabled)
                    generator.Value("<strong>Peace Config: </strong>" +
                                    "Price={price}{icon}-".Translate(("price", PeacePrice.ToString()), ("icon", Naming.Gold)));
                if (AllyEnabled)
                    generator.Value("<strong>Alliance Config: </strong>" +
                                    "Price={price}{icon}".Translate(("price", AllyPrice.ToString()), ("icon", Naming.Gold)));
                if (TradeEnabled)
                    generator.Value("<strong>Trade Config: </strong>" +
                                    "Price={price}{icon}".Translate(("price", TradePrice.ToString()), ("icon", Naming.Gold)));
                if (PolicyEnabled)
                    generator.Value("<strong>Policy Config: </strong>" +
                                    "Price={price}{icon}".Translate(("price", PolicyPrice.ToString()), ("icon", Naming.Gold)));
            }
        }

        public override Type HandlerConfigType => typeof(Settings);

        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config,
            Action<string> onSuccess, Action<string> onFailure)
        {
            if (config is not Settings settings) return;
            if (string.IsNullOrWhiteSpace(context.Args))
            {
                ActionManager.SendReply(context,
                    context.ArgsErrorMessage("{=SimpleDiplomacyArgs}invalid mode (use war (kingdom), peace (kingdom), alliance (kingdom), trade (kingdom), army (defend/siege/patrol)".Translate()));
                return;
            }
            if (Mission.Current != null)
            {
                onFailure("{=SimpleDiplomacyMissionActive}Mission is active!".Translate());
                return;
            }
            if (adoptedHero.Clan == null)
            {
                onFailure("{=B86KnTcu}You are not in a clan".Translate());
                return;
            }
            if (adoptedHero.Clan.Kingdom == null)
            {
                onFailure("{=EJ4Pd2Lg}Your clan is not in a Kingdom".Translate());
                return;
            }
            if (!adoptedHero.IsClanLeader)
            {
                onFailure("{=HS14GdUa}You cannot manage your kingdom, as you are not your clans leader!".Translate());
                return;
            }
            if (adoptedHero.Clan.IsUnderMercenaryService)
            {
                onFailure("{=SimpleDiplomacyMercenary}Mercenary".Translate());
                return;
            }
            //if (adoptedHero.Clan == Clan.PlayerClan)
            //{
            //    onFailure("")
            //}

            var splitArgs = context.Args.Split(' ');
            var mode = splitArgs[0];
            var desiredName = string.Join(" ", splitArgs.Skip(1)).Trim();
            string warCommand = "{=SimpleDiplomacyWarCommand}war".Translate();
            string peaceCommand = "{=SimpleDiplomacyPeaceCommand}peace".Translate();
            string policyCommand = "{=SimpleDiplomacyPolicyCommand}policy".Translate();
            string allianceCommand = "{=SimpleDiplomacyAllianceCommand}alliance".Translate();
            string tradeCommand = "{=SimpleDiplomacyTradeCommand}trade".Translate();
            string listCommand = "{=SimpleDiplomacyListCommand}list".Translate();
            var kingdom = adoptedHero.Clan.Kingdom;
            bool atWar = Kingdom.All.Any(k => k.IsAtWarWith(kingdom));
            AllianceCampaignBehavior allianceBehavior = Campaign.Current.GetCampaignBehavior<AllianceCampaignBehavior>();
            IAllianceCampaignBehavior iallianceBehavior = Campaign.Current.GetCampaignBehavior<IAllianceCampaignBehavior>();
            TradeAgreementsCampaignBehavior tradeBehavior = Campaign.Current.GetCampaignBehavior<TradeAgreementsCampaignBehavior>();
            var diplomacyHelper = Campaign.Current.GetCampaignBehavior<BLTDiplomacyHelper>();

            var desiredKingdom = Kingdom.All.FirstOrDefault(c => c.Name.ToString().IndexOf(desiredName, StringComparison.OrdinalIgnoreCase) >= 0);

            switch (mode)
            {
                case var _ when mode == warCommand:
                    {
                        if (!settings.WarEnabled)
                        {
                            onFailure("{=SimpleDiplomacyWarDisabled}War disabled".Translate());
                            return;
                        }
                        if (desiredKingdom == null)
                        {
                            onFailure("{=JdZ2CelP}Could not find the kingdom with the name {name}".Translate(("name", desiredName)));
                            return;
                        }
                        if (!adoptedHero.IsKingdomLeader)
                        {
                            onFailure("{=SimpleDiplomacyNotKing}Not a king.".Translate());
                            return;
                        }
                        int influenceCost = Campaign.Current.Models.DiplomacyModel.GetInfluenceCostOfProposingWar(adoptedHero.Clan);
                        if (adoptedHero.Clan.Influence < influenceCost)
                        {
                            onFailure("{=SimpleDiplomacyNotEnoughInfluence}Not enough influence:{influenceCost}".Translate(("influenceCost", influenceCost)));
                            return;
                        }
                        if (kingdom.IsAtWarWith(desiredKingdom))
                        {
                            onFailure("{=SimpleDiplomacyAlreadyAtWar}Already at war with {kingdom}".Translate(("kingdom", desiredKingdom)));
                            return;
                        }
                        if (kingdom == desiredKingdom)
                        {
                            onFailure("{=SimpleDiplomacyCantWarSelf}Cant declare war on yourself!".Translate());
                            return;
                        }
                        var stance = kingdom.GetStanceWith(desiredKingdom);
                        if (stance.PeaceDeclarationDate.ElapsedDaysUntilNow < settings.WarCooldown)
                        {
                            onFailure("{=SimpleDiplomacyWarCooldownRemaining}Cant war yet. {days} days remaining.".Translate(("days", (int)(settings.WarCooldown - stance.PeaceDeclarationDate.ElapsedDaysUntilNow))));
                            return;
                        }
                        if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.WarPrice)
                        {
                            onFailure(Naming.NotEnoughGold(settings.WarPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                            return;
                        }
                        //BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.WarPrice, true);
                        //if (kingdom == Hero.MainHero.Clan.Kingdom)
                        //{
                        //    bool isAlreadyProposed = kingdom.UnresolvedDecisions
                        //    .Any(d => d is DeclareWarDecision warDecision &&
                        //              warDecision.FactionToDeclareWarOn == desiredKingdom);
                        //    if (isAlreadyProposed)
                        //    {
                        //        Log.LogFeedMessage($"Vote already ongoing.");
                        //        return;
                        //    }

                        //    DeclareWarDecision newWarProposal = new DeclareWarDecision(adoptedHero.Clan, desiredKingdom);
                        //    adoptedHero.Clan.Kingdom.AddDecision(newWarProposal);
                        //    onSuccess("Proposed war decision.");
                        //}
                        else
                        {
                            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.WarPrice, true);
                            if (allianceBehavior.IsAllyWithKingdom(kingdom, desiredKingdom))
                            {
                                allianceBehavior.EndAlliance(kingdom, desiredKingdom);
                            }
                            if (tradeBehavior.HasTradeAgreement(kingdom, desiredKingdom, out _))
                            {
                                tradeBehavior.EndTradeAgreement(kingdom, desiredKingdom);
                            }
                            DeclareWarAction.ApplyByDefault(kingdom, desiredKingdom);
                            adoptedHero.Clan.Influence -= influenceCost;
                            onSuccess("{=SimpleDiplomacyDeclaredWar}Declared war on {kingdom}".Translate(("kingdom", desiredKingdom)));

                        }
                        break;
                    }
                case var _ when mode == peaceCommand:
                    {
                        if (!settings.PeaceEnabled)
                        {
                            onFailure("{=SimpleDiplomacyPeaceDisabled}Peace disabled".Translate());
                            return;
                        }
                        if (!adoptedHero.IsKingdomLeader)
                        {
                            onFailure("{=SimpleDiplomacyNotKing}Not a king.".Translate());
                            return;
                        }
                        if (desiredKingdom == null)
                        {
                            onFailure("{=JdZ2CelP}Could not find the kingdom with the name {name}".Translate(("name", desiredName)));
                            return;
                        }
                        if (kingdom == desiredKingdom)
                        {
                            onFailure("{=SimpleDiplomacyCantPeaceSelf}Cant peace yourself!".Translate());
                            return;
                        }
                        int influenceCost = Campaign.Current.Models.DiplomacyModel.GetInfluenceCostOfProposingPeace(adoptedHero.Clan);


                        var stance = kingdom.GetStanceWith(desiredKingdom);
                        if (!kingdom.IsAtWarWith(desiredKingdom))
                        {
                            onFailure("{=SimpleDiplomacyAlreadyAtPeace}Already at peace with {kingdom}".Translate(("kingdom", desiredKingdom)));
                            return;
                        }
                        if (diplomacyHelper.IsPeaceBlocked(kingdom, desiredKingdom))
                        {
                            onFailure("{=SimpleDiplomacyCannotPeaceRebellion}Cannot peace rebellion wars".Translate());
                            return;
                        }
                        if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.PeacePrice)
                        {
                            onFailure(Naming.NotEnoughGold(settings.PeacePrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                            return;
                        }
                        if (adoptedHero.Clan.Influence < influenceCost)
                        {
                            onFailure("{=SimpleDiplomacyNotEnoughInfluence}Not enough influence:{influenceCost}".Translate(("influenceCost", influenceCost)));
                            return;
                        }
                        BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.PeacePrice, true);

                        Clan proposer = adoptedHero.Clan;
                        var diplomacy = Campaign.Current.Models.DiplomacyModel;
                        Clan recipient = desiredKingdom.RulingClan;
                        int dailyTribute = diplomacy.GetDailyTributeToPay(proposer, recipient, out int tributeDurationInDays);


                        if (desiredKingdom == Hero.MainHero.Clan.Kingdom && Hero.MainHero.IsKingdomLeader)
                        {
                            CampaignEventDispatcher.Instance.OnPeaceOfferedToPlayer(kingdom, dailyTribute, tributeDurationInDays);

                            onSuccess("{=SimpleDiplomacyPeaceOfferSent}Peace offer sent to the player.".Translate());
                        }
                        //else if (kingdom.Leader.IsAdopted())
                        //{
                        //    bool isAlreadyProposed = kingdom.UnresolvedDecisions
                        //    .Any(d => d is MakePeaceKingdomDecision peaceDecision &&
                        //              peaceDecision.FactionToMakePeaceWith == desiredKingdom);
                        //    if (isAlreadyProposed)
                        //    {
                        //        Log.LogFeedMessage($"Vote already ongoing.");
                        //        return;
                        //    }

                        //    MakePeaceKingdomDecision newPeaceProposal = new MakePeaceKingdomDecision(adoptedHero.Clan, desiredKingdom, dailyTribute, tributeDurationInDays);
                        //    adoptedHero.Clan.Kingdom.AddDecision(newPeaceProposal);
                        //    adoptedHero.Clan.Influence -= influenceCost;
                        //    onSuccess($"Proposed peace decision");
                        //}
                        else
                        {
                            TextObject reason;
                            bool acceptPeace = Campaign.Current.Models.KingdomDecisionPermissionModel.IsPeaceDecisionAllowedBetweenKingdoms(kingdom, desiredKingdom, out reason);
                            if (!acceptPeace)
                            {
                                onFailure(reason.ToString());
                                return;
                            }

                            MakePeaceAction.ApplyByKingdomDecision(kingdom, desiredKingdom, dailyTribute, tributeDurationInDays);
                            adoptedHero.Clan.Influence -= influenceCost;
                            influenceCost *= -1;
                            onSuccess("{=BLTTribute}Peace applied between {Proposer} and {Recipient}. Daily tribute: {DailyTribute}, Duration: {Days} days."
                                .Translate(
                                    ("Proposer", proposer.Name),
                                    ("Recipient", recipient.Name),
                                    ("DailyTribute", dailyTribute),
                                    ("Days", tributeDurationInDays)
                                ));
                        }
                        BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.PeacePrice, true);
                        break;
                    }
                case var _ when mode == policyCommand:
                    {
                        var desiredPolicy = PolicyObject.All.FirstOrDefault(c => c.Name.ToString().IndexOf(desiredName, StringComparison.OrdinalIgnoreCase) >= 0);
                        int policyCost = Campaign.Current.Models.DiplomacyModel.GetInfluenceCostOfPolicyProposalAndDisavowal(adoptedHero.Clan);
                        if (!settings.PolicyEnabled)
                        {
                            onFailure("{=SimpleDiplomacyPolicyDisabled}Policy disabled".Translate());
                            return;
                        }
                        if (!adoptedHero.IsKingdomLeader)
                        {
                            onFailure("{=SimpleDiplomacyNotKing}Not a king.".Translate());
                            return;
                        }

                        if (desiredName == listCommand)
                        {
                            var listString = string.Join(", ", PolicyObject.All.Select(k => k.Name.ToString()));
                            onSuccess(listString);
                            return;
                        }
                        if (string.IsNullOrEmpty(desiredName) || splitArgs.Count() <= 2)
                        {
                            var listString = string.Join(", ", kingdom.ActivePolicies.Select(p => p.ToString()));
                            onSuccess(listString);
                            return;
                        }
                        if (desiredPolicy != null)
                        {
                            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.PolicyPrice)
                            {
                                onFailure(Naming.NotEnoughGold(settings.PeacePrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                                return;
                            }
                            if (adoptedHero.Clan.Influence < policyCost)
                            {
                                onFailure("{=SimpleDiplomacyNotEnoughInfluence}Not enough influence:{influenceCost}".Translate(("influenceCost", policyCost)));
                                return;
                            }
                            if (kingdom.ActivePolicies.Contains(desiredPolicy))
                            {
                                kingdom.RemovePolicy(desiredPolicy);
                                onSuccess("{=SimpleDiplomacyPolicyRemoved}Removed {policy}".Translate(("policy", desiredPolicy)));
                                return;
                            }
                            else
                            {
                                kingdom.AddPolicy(desiredPolicy);
                                onSuccess("{=SimpleDiplomacyPolicyAdded}Added {policy}".Translate(("policy", desiredPolicy)));
                                return;
                            }
                        }
                        else { onFailure("{=SimpleDiplomacyInvalidAction}Invalid action".Translate()); }
                        break;
                    }
                case var _ when mode == allianceCommand:
                    {

                        if (!settings.AllyEnabled)
                        {
                            onFailure("{=SimpleDiplomacyAllianceDisabled}Alliance disabled".Translate());
                            return;
                        }
                        if (desiredKingdom == null)
                        {
                            onFailure("{=JdZ2CelP}Could not find the kingdom with the name {name}".Translate(("name", desiredName)));
                            return;
                        }
                        if (!adoptedHero.IsKingdomLeader)
                        {
                            onFailure("{=SimpleDiplomacyNotKing}Not a king.".Translate());
                            return;
                        }

                        if (kingdom.IsAtWarWith(desiredKingdom))
                        {
                            onFailure("{=SimpleDiplomacyAtWarWith}At war with {kingdom}".Translate(("kingdom", desiredKingdom)));
                            return;
                        }
                        if (allianceBehavior.IsAllyWithKingdom(kingdom, desiredKingdom))
                        {
                            onFailure("{=SimpleDiplomacyAlreadyAllied}Already allied with {kingdom}".Translate(("kingdom", desiredKingdom)));
                            return;
                        }
                        if (kingdom == desiredKingdom)
                        {
                            onFailure("{=SimpleDiplomacyCantAllySelf}Cant ally on yourself!".Translate());
                            return;
                        }
                        int influenceCost = Campaign.Current.Models.AllianceModel.GetInfluenceCostOfProposingStartingAlliance(adoptedHero.Clan);
                        if (adoptedHero.Clan.Influence < influenceCost)
                        {
                            onFailure("{=SimpleDiplomacyNotEnoughInfluence}Not enough influence:{influenceCost}".Translate(("influenceCost", influenceCost)));
                            return;
                        }
                        if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.AllyPrice)
                        {
                            onFailure(Naming.NotEnoughGold(settings.AllyPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                            return;
                        }
                        if (desiredKingdom == Hero.MainHero.Clan.Kingdom && !Hero.MainHero.IsKingdomLeader)
                        {
                            iallianceBehavior.OnAllianceOfferedToPlayerKingdom(kingdom);
                            adoptedHero.Clan.Influence -= influenceCost;
                            onSuccess("{=SimpleDiplomacyAllianceOfferedToPlayerKingdom}Proposed alliance to player kingdom".Translate());
                        }
                        else if (desiredKingdom == Hero.MainHero.Clan.Kingdom && Hero.MainHero.IsKingdomLeader)
                        {
                            iallianceBehavior.OnAllianceOfferedToPlayer(kingdom);
                            adoptedHero.Clan.Influence -= influenceCost;
                            onSuccess("{=SimpleDiplomacyAllianceOfferedToPlayer}Proposed alliance to player".Translate());
                        }
                        else
                        {

                            allianceBehavior.StartAlliance(kingdom, desiredKingdom);
                            adoptedHero.Clan.Influence -= influenceCost;
                            onSuccess("{=SimpleDiplomacyAlliedWith}Allied with {kingdom}".Translate(("kingdom", desiredKingdom)));
                        }
                        BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.AllyPrice, true);
                        break;
                    }
                case var _ when mode == tradeCommand:
                    {

                        if (!settings.TradeEnabled)
                        {
                            onFailure("{=SimpleDiplomacyTradeDisabled}Trade alliances disabled".Translate());
                            return;
                        }
                        if (desiredKingdom == null)
                        {
                            onFailure("{=JdZ2CelP}Could not find the kingdom with the name {name}".Translate(("name", desiredName)));
                            return;
                        }
                        if (!adoptedHero.IsKingdomLeader)
                        {
                            onFailure("{=SimpleDiplomacyNotKing}Not a king.".Translate());
                            return;
                        }

                        if (kingdom.IsAtWarWith(desiredKingdom))
                        {
                            onFailure("{=SimpleDiplomacyAtWarWith}At war with {kingdom}".Translate(("kingdom", desiredKingdom)));
                            return;
                        }
                        if (tradeBehavior.HasTradeAgreement(kingdom, desiredKingdom, out _))
                        {
                            onFailure("{=SimpleDiplomacyAlreadyTrading}Already trading with {kingdom}".Translate(("kingdom", desiredKingdom)));
                            return;
                        }
                        if (kingdom == desiredKingdom)
                        {
                            onFailure("{=SimpleDiplomacyCantTradeSelf}Cant trade with yourself!".Translate());
                            return;
                        }
                        int influenceCost = Campaign.Current.Models.TradeAgreementModel.GetInfluenceCostOfProposingTradeAgreement(adoptedHero.Clan);
                        if (adoptedHero.Clan.Influence < influenceCost)
                        {
                            onFailure("{=SimpleDiplomacyNotEnoughInfluence}Not enough influence:{influenceCost}".Translate(("influenceCost", influenceCost)));
                            return;
                        }
                        if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.TradePrice)
                        {
                            onFailure(Naming.NotEnoughGold(settings.TradePrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                            return;
                        }
                        if (desiredKingdom == Hero.MainHero.Clan.Kingdom && !Hero.MainHero.IsKingdomLeader)
                        {
                            tradeBehavior.OnTradeAgreementOfferedToPlayer(kingdom);
                            adoptedHero.Clan.Influence -= influenceCost;
                            onSuccess("{=SimpleDiplomacyTradeOfferedToPlayerKingdom}Proposed trade agreement to player kingdom".Translate());
                        }
                        else if (desiredKingdom == Hero.MainHero.Clan.Kingdom && Hero.MainHero.IsKingdomLeader)
                        {
                            tradeBehavior.OnTradeAgreementOfferedToPlayer(kingdom);
                            adoptedHero.Clan.Influence -= influenceCost;
                            onSuccess("{=SimpleDiplomacyTradeOfferedToPlayerKingdom}Proposed trade agreement to player kingdom".Translate());
                        }
                        else
                        {
                            var duration = Campaign.Current.Models.TradeAgreementModel.GetTradeAgreementDurationInYears(kingdom, desiredKingdom);
                            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.TradePrice, true);
                            tradeBehavior.MakeTradeAgreement(kingdom, desiredKingdom, duration);
                            adoptedHero.Clan.Influence -= influenceCost;
                            onSuccess("{=SimpleDiplomacyTradeAgreedWith}Allied with {kingdom}".Translate(("kingdom", desiredKingdom)));
                        }
                        break;
                    }
                default:
                    {
                        ActionManager.SendReply(context,
                        context.ArgsErrorMessage("{=SimpleDiplomacyArgsShort}invalid mode (use war (kingdom), peace (kingdom), alliance (kingdom), trade (kingdom)".Translate()));
                        break;
                    }
            }
        }
    }
}
