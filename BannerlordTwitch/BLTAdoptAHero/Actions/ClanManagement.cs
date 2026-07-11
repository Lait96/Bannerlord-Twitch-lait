using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using BLTAdoptAHero;
using BLTAdoptAHero.Annotations;
using TaleWorlds.ObjectSystem;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using NavalDLC.GameComponents;
using NavalDLC.CampaignBehaviors;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero.Actions
{
    [LocDisplayName("{=BLTClanManagementName}Clan Management"),
     LocDescription("{=BLTClanManagementDescription}Allow viewers to change their clan or make leader decisions"),
     UsedImplicitly]
    public class ClanManagement : HeroCommandHandlerBase
    {
        //private static Harmony harmonyInstance = null;
        //static ClanManagement()
        //{
        //    InitializeHarmony();
        //}

        //private static void InitializeHarmony()
        //{
        //    if (harmonyInstance == null)
        //    {
        //        harmonyInstance = new Harmony("BLTClanManagement");
        //        harmonyInstance.PatchAll();
        //    }
        //}
        [CategoryOrder("Join", 0),
         CategoryOrder("Create", 1),
         CategoryOrder("Lead", 2),
         CategoryOrder("Rename", 3),
         CategoryOrder("Stats", 4),
         CategoryOrder("Party", 5),
         CategoryOrder("Leave", 6),
         //CategoryOrder("Disband", 6),
         CategoryOrder("Buy Noble Title", 7),
         CategoryOrder("Edit Banner", 8),
         CategoryOrder("Ship", 9),
         CategoryOrder("Home", 10)]
        private class Settings : IDocumentable
        {
            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Join", "{=q5JhpNMF}Join"),
             LocDescription("{=BLTClanJoinEnabledDescription}Enable the command for joining clans"),
             PropertyOrder(1), UsedImplicitly]
            public bool JoinEnabled { get; set; } = true;

            [LocDisplayName("{=jwSrIS8n}Max Heroes"),
             LocCategory("Join", "{=q5JhpNMF}Join"),
             LocDescription("{=nC2MvvB6}Maximum heroes (includes NPC's) before join is disallowed"),
             PropertyOrder(2), UsedImplicitly]
            public int JoinMaxHeroes { get; set; } = 50;

            [LocDisplayName("{=d5WMYSvO}Gold Cost"),
             LocCategory("Join", "{=q5JhpNMF}Join"),
             LocDescription("{=bxuW8r3J}Cost of joining a clan"),
             PropertyOrder(3), UsedImplicitly]
            public int JoinPrice { get; set; } = 150000;

            [LocDisplayName("{=k3ihPbMl}Players Clan?"),
             LocCategory("Join", "{=q5JhpNMF}Join"),
             LocDescription("{=KA3w5CSP}Allow viewers to join the players clan"),
             PropertyOrder(4), UsedImplicitly]
            public bool JoinAllowPlayer { get; set; } = false;

            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Create", "{=9lAIycwE}Create"),
             LocDescription("{=x5aY2Ryn}Enable creating clans command"),
             PropertyOrder(1), UsedImplicitly]
            public bool CreateEnabled { get; set; } = true;

            [LocDisplayName("{=d5WMYSvO}Gold Cost"),
             LocCategory("Create", "{=9lAIycwE}Create"),
             LocDescription("{=KvYA5eAy}Cost of creating a clan"),
             PropertyOrder(2), UsedImplicitly]
            public int CreatePrice { get; set; } = 1000000;

            [LocDisplayName("{=d5WMYSvO}Starting renown"),
             LocCategory("Create", "{=9lAIycwE}Create"),
             LocDescription("{=KvYA5eAy}Starting renown(T1:50, T2:150, T3:350, T4:900, T5:2350, T6:6150)"),
             PropertyOrder(3), UsedImplicitly]
            public int Renown { get; set; } = 100;

            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Lead", "{=TrSSHcbH}Lead"),
             LocDescription("{=KQWPJonx}Enable leading clans command"),
             PropertyOrder(1), UsedImplicitly]
            public bool LeadEnabled { get; set; } = true;

            [LocDisplayName("{=d5WMYSvO}Gold Cost"),
             LocCategory("Lead", "{=TrSSHcbH}Lead"),
             LocDescription("{=7Zqi5Ehg}Cost of leading a clan"),
             PropertyOrder(2), UsedImplicitly]
            public int LeadPrice { get; set; } = 1000000;

            [LocDisplayName("{=xcThCwjr}Challenge Heroes"),
             LocCategory("Lead", "{=TrSSHcbH}Lead"),
             LocDescription("{=LWj6LPyH}Toggle whether or not trying to lead a clan already led by a BLT hero is possible - random chance they win based on skill difference"),
             PropertyOrder(3), UsedImplicitly]
            public bool LeadChallengeHeroes { get; set; } = true;

            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Rename", "{=ugFdRADy}Rename"),
             LocDescription("{=NhJk9hgu}Enable renaming clans command"),
             PropertyOrder(1), UsedImplicitly]
            public bool RenameEnabled { get; set; } = true;

            [LocDisplayName("{=d5WMYSvO}Gold Cost"),
             LocCategory("Rename", "{=ugFdRADy}Rename"),
             LocDescription("{=d2H2BrIG}Cost of renaming a clan"),
             PropertyOrder(2), UsedImplicitly]
            public int RenamePrice { get; set; } = 1000000;

            [LocDisplayName("{=mlayrmHr}Stats"),
             LocCategory("Stats", "{=mlayrmHr}Stats"),
             LocDescription("{=vNGlBZUB}Enable stats command"),
             PropertyOrder(1), UsedImplicitly]
            public bool StatsEnabled { get; set; } = true;

            [LocDisplayName("{=7XUApUQM}Fiefs"),
             LocCategory("Stats", "{=mlayrmHr}Stats"),
             LocDescription("{=KC7IE9Bt}Enable fiefs command"),
             PropertyOrder(2), UsedImplicitly]
            public bool FiefsEnabled { get; set; } = true;

            [LocDisplayName("{=BLTClanCategoryLeave}Leave"),
             LocCategory("Leave", "{=BLTClanCategoryLeave}Leave"),
             LocDescription("{=BLTClanLeaveDescription}Allow BLTs to leave their clan. WARNING: Leaving will turn their character into a wanderer, and they may have to buy their Nobility back!"),
             PropertyOrder(1), UsedImplicitly]
            public bool LeaveEnabled { get; set; } = true;

            //[LocDisplayName("{=pYjIUlTE}Enabled"),
            // LocCategory("Disband", "{=TESTING}Disband"),
            // LocDescription("Enable BLTs disbanding an empty clan.   WARNING: Disbanding will turn their character into a wanderer, and they may have to buy their Nobility back!"),
            // PropertyOrder(1), UsedImplicitly]
            //public bool DisbandEnabled { get; set; } = true;

            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Buy Noble Title", "{=BLTClanCategoryBuyTitle}Buy Noble Title"),
             LocDescription("{=BLTClanBuyTitleDescription}Allow non-noble BLTs to buy their way into being a Lord, allowing their Hero's AI many more clan and kingdom actions. NOTE: Buying a title is required when joining; if disabled, BLTs become Lords automatically when joining a clan."),
             PropertyOrder(1), UsedImplicitly]
            public bool BuyTitleEnabled { get; set; } = true;

            [LocDisplayName("{=d5WMYSvO}Gold Cost"),
             LocCategory("Buy Noble Title", "{=BLTClanCategoryBuyTitle}Buy Noble Title"),
             LocDescription("{=BLTClanTitlePriceDescription}Cost of becoming a noble"),
             PropertyOrder(2), UsedImplicitly]
            public int TitlePrice { get; set; } = 200000;

            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Edit Banner", "{=BLTClanCategoryEditBanner}Edit Banner"),
             LocDescription("{=BLTClanBannerDescription}Edit your banner with a code. Make your banner at https://bannerlord.party/banner"),
             PropertyOrder(1), UsedImplicitly]
            public bool EditBannerEnabled { get; set; } = true;

            [LocDisplayName("{=pYjIUlTE}Enabled"),
             LocCategory("Ship", "{=BLTClanCategoryShip}Ship"),
             LocDescription("{=BLTClanShipDescription}Buy ships"),
             PropertyOrder(1), UsedImplicitly]
            public bool BuyShipEnabled { get; set; } = true;

            [LocDisplayName("{=d5WMYSvO}Gold Cost"),
             LocCategory("Ship", "{=BLTClanCategoryShip}Ship"),
             LocDescription("{=BLTClanShipPriceDescription}Cost of buying a light ship. Medium is 2x, Heavy is 3x."),
             PropertyOrder(2), UsedImplicitly]
            public int BuyShipPrice { get; set; } = 50000;

            [LocDisplayName("{=BLTClanSelectHome}Select home"),
             LocCategory("Home", "{=BLTClanCategoryHome}Home"),
             LocDescription("{=BLTClanHomeDescription}Select the clan home"),
             PropertyOrder(1), UsedImplicitly]
            public bool SelectHome { get; set; } = true;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                var EnabledCommands = new StringBuilder();
                if (JoinEnabled)
                    EnabledCommands = EnabledCommands.Append("{=q5JhpNMF}Join, ".Translate());
                if (CreateEnabled)
                    EnabledCommands = EnabledCommands.Append("{=9lAIycwE}Create, ".Translate());
                if (LeadEnabled)
                    EnabledCommands = EnabledCommands.Append("{=TrSSHcbH}Lead, ".Translate());
                if (RenameEnabled)
                    EnabledCommands = EnabledCommands.Append("{=ugFdRADy}Rename, ".Translate());
                if (StatsEnabled)
                    EnabledCommands = EnabledCommands.Append("{=mlayrmHr}Stats, ".Translate());
                if (FiefsEnabled)
                    EnabledCommands = EnabledCommands.Append("{=7XUApUQM}Fiefs, ".Translate());
                if (LeaveEnabled)
                    EnabledCommands = EnabledCommands.Append("{=BLTClanCategoryLeave}Leave, ".Translate());
                //if (DisbandEnabled)
                //    EnabledCommands = EnabledCommands.Append("{=TESTING}Disband, ".Translate());
                //    Log.ShowInformation("disband");
                if (BuyTitleEnabled)
                    EnabledCommands = EnabledCommands.Append("{=BLTClanCategoryBuyTitle}Buy Noble Title, ".Translate());
                if (EditBannerEnabled)
                    EnabledCommands = EnabledCommands.Append("{=BLTClanCategoryEditBanner}Edit Banner, ".Translate());
                if (BuyShipEnabled)
                    EnabledCommands = EnabledCommands.Append("{=BLTClanCategoryShip}Ship, ".Translate());
                if (SelectHome)
                    EnabledCommands = EnabledCommands.Append("{=BLTClanCategoryHome}Home, ".Translate());
                if (EnabledCommands != null)
                    generator.Value("{=BLTClanDocsEnabledCommands}<strong>Enabled Commands:</strong> {commands}".Translate(("commands", EnabledCommands.ToString().Substring(0, EnabledCommands.ToString().Length - 2))));

                if (JoinEnabled)
                    generator.Value("{=BLTClanDocsJoin}<strong>Join Config:</strong> Max Heroes={maxHeroes}, Price={price}{icon}, Allow Join Players Clan?={allowPlayer}"
                        .Translate(("maxHeroes", JoinMaxHeroes), ("price", JoinPrice), ("icon", Naming.Gold), ("allowPlayer", JoinAllowPlayer)));
                if (CreateEnabled)
                    generator.Value("{=BLTClanDocsCreate}<strong>Create Config:</strong> Price={price}{icon}, Renown={renown}"
                        .Translate(("price", CreatePrice), ("icon", Naming.Gold), ("renown", Renown)));
                if (LeadEnabled)
                    generator.Value("{=BLTClanDocsLead}<strong>Lead Config:</strong> Price={price}{icon}, Challenge Heroes?={challengeHeroes}"
                        .Translate(("price", LeadPrice), ("icon", Naming.Gold), ("challengeHeroes", LeadChallengeHeroes)));
                if (RenameEnabled)
                    generator.Value("{=BLTClanDocsRename}<strong>Rename Config:</strong> Price={price}{icon}"
                        .Translate(("price", RenamePrice), ("icon", Naming.Gold)));
                if (BuyTitleEnabled)
                    generator.Value("{=BLTClanDocsBuyTitle}<strong>Buy Noble Title Config:</strong> Price={price}{icon}"
                        .Translate(("price", TitlePrice), ("icon", Naming.Gold)));
                if (EditBannerEnabled)
                    generator.Value("{=BLTClanDocsBanner}<strong>Create a banner:</strong> (bannerlord.party/banner/)\nFor long banners: !clan banner start -> !clan banner {code} (repeat) -> !clan banner end".Translate());
                if (BuyShipEnabled)
                    generator.Value("{=BLTClanDocsShip}<strong>Ship:</strong> Light/Medium/Heavy, Price={price}{icon}"
                        .Translate(("price", BuyShipPrice), ("icon", Naming.Gold)));
            }
        }
        public override Type HandlerConfigType => typeof(Settings);
        //private string ConvertPastebinUrlToRaw(string url)
        //{
        //    if (url.Contains("pastes.io/"))
        //    {
        //        return url.Replace("pastes.io/", "pastes.io/raw/");
        //    }
        //    if (url.Contains("pastesio/"))
        //    {
        //        return url.Replace("pastesio/", "pastes.io/raw/");
        //    }

        //    return url;
        //}

        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config, Action<string> onSuccess, Action<string> onFailure)
        {
            if (config is not Settings settings) return;
            //var adoptedHero = BLTAdoptAHeroCampaignBehavior.Current.GetAdoptedHero(context.UserName);
            if (adoptedHero == null)
            {
                onFailure(AdoptAHero.NoHeroMessage);
                return;
            }
            if (Mission.Current != null)
            {
                onFailure("{=MPTOZqMS}You cannot manage your clan, as a mission is active!".Translate());
                return;
            }
            //This is deemed annoying and unnecessary in the community, especially since you can't effect being captured or released at all through clan commands, so there are very few possible conflicts or errors from this.
            //if (adoptedHero.HeroState == Hero.CharacterStates.Prisoner)
            //{
            //    onFailure("{=oxNqBy4k}You cannot manage your clan, as you are a prisoner!".Translate());
            //    return;
            //}

            if (context.Args.IsEmpty())
            {
                if (adoptedHero.Clan == null)
                {
                    onFailure("{=B86KnTcu}You are not in a clan".Translate());
                    return;
                }
                onSuccess("{=xMSAI7HK}Your clan is {clanName}".Translate(("clanName", adoptedHero.Clan.Name.ToString())));
                return;
            }

            var splitArgs = context.Args.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var command = GetClanCommand(splitArgs, out int commandArgCount);
            var desiredName = string.Join(" ", splitArgs.Skip(commandArgCount)).Trim();
            var bannerCodeOrUrl = desiredName;

            switch (command)
            {
                case "join":
                    HandleJoinCommand(settings, adoptedHero, desiredName, onSuccess, onFailure);
                    break;
                case "create":
                    HandleCreateCommand(settings, adoptedHero, desiredName, onSuccess, onFailure);
                    break;
                case "lead":
                    HandleLeadCommand(settings, adoptedHero, onSuccess, onFailure);
                    break;
                case "rename":
                    HandleRenameCommand(settings, adoptedHero, desiredName, onSuccess, onFailure);
                    break;
                case "stats":
                    HandleStatsCommand(settings, adoptedHero, onSuccess, onFailure);
                    break;
                case "party":
                    HandlePartyCommand(settings, adoptedHero, onSuccess, onFailure);
                    break;
                case "fiefs":
                    HandleFiefsCommand(settings, adoptedHero, onSuccess, onFailure);
                    break;
                case "leave":
                    HandleLeaveCommand(settings, adoptedHero, onSuccess, onFailure);
                    break;
                //case var _ when command.ToLower() == disbandCommand:
                //    HandleDisbandCommand(settings, adoptedHero, onSuccess, onFailure);
                //    break;
                case "buy title":
                    HandleBuyTitleCommand(settings, adoptedHero, onSuccess, onFailure);
                    break;
                case "banner":
                    {
                        string bannerCode = bannerCodeOrUrl;
                        //if (bannerCodeOrUrl.StartsWith("https://pastes.io/", StringComparison.OrdinalIgnoreCase) || bannerCodeOrUrl.StartsWith("https://pastesio/", StringComparison.OrdinalIgnoreCase))
                        //{
                        //    bannerCodeOrUrl = ConvertPastebinUrlToRaw(bannerCodeOrUrl);
                        //    try
                        //    {
                        //        using var client = new HttpClient();
                        //        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                        //        bannerCode = client.GetStringAsync(bannerCodeOrUrl).GetAwaiter().GetResult().Trim();
                        //    }
                        //    catch (Exception ex)
                        //    {
                        //        onFailure($"Failed to fetch banner code from URL: {ex.Message}");
                        //        return;
                        //    }
                        //}

                        HandleBannerCommand(settings, adoptedHero, bannerCode, onSuccess, onFailure);
                        break;
                    }
                case "ship":
                    HandleShipCommand(settings, adoptedHero, desiredName, onSuccess, onFailure);
                    break;
                case "home":
                    HandleHomeCommand(settings, adoptedHero, desiredName, onSuccess, onFailure);
                    break;
                default:
                    onFailure("{=pkzDqw18}Invalid or empty clan action, try (join/create/lead/rename/stats/party/fiefs/leave/buy title/banner/ship/home)".Translate());
                    break;
            }
        }

        private static string GetClanCommand(string[] args, out int commandArgCount)
        {
            commandArgCount = 1;
            if (args.Length == 0) return "";

            if (args.Length > 1 && MatchesCommand(string.Join(" ", args.Take(2)),
                    "{=BLTClanSubBuyTitle}buy title".Translate(), "buy title"))
            {
                commandArgCount = 2;
                return "buy title";
            }

            string command = args[0];
            if (MatchesCommand(command, "{=BLTClanSubJoin}join".Translate(), "join")) return "join";
            if (MatchesCommand(command, "{=BLTClanSubCreate}create".Translate(), "create")) return "create";
            if (MatchesCommand(command, "{=BLTClanSubLead}lead".Translate(), "lead")) return "lead";
            if (MatchesCommand(command, "{=BLTClanSubRename}rename".Translate(), "rename")) return "rename";
            if (MatchesCommand(command, "{=BLTClanSubStats}stats".Translate(), "stats")) return "stats";
            if (MatchesCommand(command, "{=BLTClanSubParty}party".Translate(), "party")) return "party";
            if (MatchesCommand(command, "{=BLTClanSubFiefs}fiefs".Translate(), "fiefs")) return "fiefs";
            if (MatchesCommand(command, "{=BLTClanSubLeave}leave".Translate(), "leave")) return "leave";
            if (MatchesCommand(command, "{=BLTClanSubBanner}banner".Translate(), "banner")) return "banner";
            if (MatchesCommand(command, "{=BLTClanSubShip}ship".Translate(), "ship")) return "ship";
            if (MatchesCommand(command, "{=BLTClanSubHome}home".Translate(), "home")) return "home";

            return command.ToLowerInvariant();
        }

        private static bool MatchesCommand(string command, string translatedCommand, string defaultCommand)
            => command.Equals(defaultCommand, StringComparison.OrdinalIgnoreCase)
               || command.Equals(translatedCommand, StringComparison.OrdinalIgnoreCase);

        private void HandleJoinCommand(Settings settings, Hero adoptedHero, string desiredName, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.JoinEnabled)
            {
                onFailure("{=VupTnRNX}Joining clans is disabled".Translate());
                return;
            }
            if (adoptedHero.IsClanLeader)
            {
                onFailure("{=OrBEbanC}You cannot join another clan as you are the leader of your clan".Translate());
                return;
            }
            if (string.IsNullOrWhiteSpace(desiredName))
            {
                onFailure("{=3ktTpCyC}(join) (clan name)".Translate());
                return;
            }

            var desiredClan = CampaignHelpers.AllHeroes.Select(h => h.Clan).Distinct().FirstOrDefault(c => c?.Name.ToString().Equals(desiredName, StringComparison.OrdinalIgnoreCase) == true);
            if (desiredClan == null)
            {
                onFailure("{=xylTvKyE}Could not find the clan with the name {name}".Translate(("name", desiredName)));
                return;
            }
            if (desiredClan.Heroes.Count >= settings.JoinMaxHeroes)
            {
                onFailure("{=aoxW7fmn}The clan {name} is full".Translate(("name", desiredName)));
                return;
            }
            if (desiredClan == Hero.MainHero.Clan && !settings.JoinAllowPlayer)
            {
                onFailure("{=jptOPf36}Joining the players clan is disabled".Translate());
                return;
            }
            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.JoinPrice)
            {
                onFailure(Naming.NotEnoughGold(settings.JoinPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                return;
            }

            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.JoinPrice, true);
            adoptedHero.Clan = desiredClan;
            onSuccess("{=T7U1Piwx}Joined clan {name}".Translate(("name", desiredName)));
            Log.ShowInformation("{=WseRTV8W}{heroName} has joined clan {clanName}!".Translate(("heroName", adoptedHero.Name.ToString()), ("clanName", adoptedHero.Clan.Name.ToString())), adoptedHero.CharacterObject, Log.Sound.Horns2);
            if ((!settings.BuyTitleEnabled) && ((adoptedHero.Occupation != Occupation.Lord) && (adoptedHero.Clan != null)))
            {
                onFailure("{=6yQUu78N}{heroName} has become a noble!".Translate(("heroName", adoptedHero.Name.ToString())));
                adoptedHero.SetNewOccupation(Occupation.Lord);
            }
        }

        private void HandleCreateCommand(Settings settings, Hero adoptedHero, string desiredName, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.CreateEnabled)
            {
                onFailure("{=drmqcbE2}Creating clans is disabled".Translate());
                return;
            }
            if (adoptedHero.IsClanLeader)
            {
                onFailure("{=zZYHVBZ6}You cannot create another clan as you are the leader of your clan".Translate());
                return;
            }
            if (string.IsNullOrWhiteSpace(desiredName))
            {
                onFailure("{=6vTxAMVx}(create) (clan name)".Translate());
                return;
            }
            if (adoptedHero.PartyBelongedTo == Hero.MainHero.PartyBelongedTo || (adoptedHero.PartyBelongedTo != null && Clan.PlayerClan.WarPartyComponents.Select(w => w.MobileParty).Contains(adoptedHero.PartyBelongedTo)))
            {
                onFailure("{=BLTClanCreateInPlayerParty}You cannot create a clan while in the player's party".Translate());
                return;
            }
            

            var fullClanName = $"[BLT Clan] {desiredName}";
            var existingClan = CampaignHelpers.AllHeroes.Select(h => h.Clan).Distinct().FirstOrDefault(c => c?.Name.ToString().Equals(fullClanName, StringComparison.OrdinalIgnoreCase) == true);
            if (existingClan != null)
            {
                onFailure("{=Aae45bKp}A clan with the name {name} already exists".Translate(("name", desiredName)));
                return;
            }
            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.CreatePrice)
            {
                onFailure(Naming.NotEnoughGold(settings.CreatePrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                return;
            }

            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.CreatePrice, true);
            if (adoptedHero.Clan == Clan.PlayerClan)
            {
                adoptedHero.CompanionOf = null;
            }
            var newClan = Clan.CreateClan(fullClanName);
            newClan.ChangeClanName(new TextObject(fullClanName), new TextObject(fullClanName));
            newClan.Culture = adoptedHero.Culture;
            newClan.Banner = Banner.CreateRandomBanner();
            //newClan.Initialize(new TextObject(fullClanName), new TextObject(fullClanName), clanCulture, clanBanner);
            newClan.Kingdom = null;
            newClan.AddRenown(settings.Renown, false);
            newClan.SetInitialHomeSettlement(Settlement.All.Where(s => s.Culture == adoptedHero.Culture).SelectRandom() ?? (Settlement.All.SelectRandom()));
            adoptedHero.Clan = newClan;
            if ((adoptedHero.Occupation != Occupation.Lord) && (adoptedHero.Clan != null))
            {
                onSuccess("{=BLTClanHeroBecameNoble}{heroName} has become a noble!".Translate(("heroName", adoptedHero.Name.ToString())));
                adoptedHero.SetNewOccupation(Occupation.Lord);
            }
            newClan.SetLeader(adoptedHero);
            newClan.IsNoble = true;
            CampaignEventDispatcher.Instance.OnClanCreated(newClan, false);

            adoptedHero.Gold = 50000;
             

            if (!CampaignHelpers.IsEncyclopediaBookmarked(newClan))
                CampaignHelpers.AddEncyclopediaBookmarkToItem(newClan);
            onSuccess("{=omDrEeDx}Created and leading clan {name}".Translate(("name", fullClanName)));
            Log.ShowInformation("{=TsmDfvuz}{heroName} has created and is leading clan {clanName}!".Translate(("heroName", adoptedHero.Name.ToString()), ("clanName", adoptedHero.Clan.Name.ToString())), adoptedHero.CharacterObject, Log.Sound.Horns2);
            //ChangeClanLeaderAction.ApplyWithSelectedNewLeader(newClan, adoptedHero);

            if (adoptedHero?.PartyBelongedTo?.Party?.MobileParty == null && !adoptedHero.IsPartyLeader)
            {
                var newParty = Helpers.MobilePartyHelper.CreateNewClanMobileParty(adoptedHero, newClan);
                
                var retinue = BLTAdoptAHeroCampaignBehavior.Current.GetRetinue(adoptedHero).ToList();
                var retinue2 = BLTAdoptAHeroCampaignBehavior.Current.GetRetinue2(adoptedHero).ToList();
                if (newParty != null)
                {
                    foreach (var retinueTroop in retinue)
                    {
                        if (retinueTroop != null)
                        {
                            newParty.MemberRoster.AddToCounts(retinueTroop, 1);
                        }
                    }
                    foreach (var retinue2Troop in retinue2)
                    {
                        if (retinue2Troop != null)
                        {
                            newParty.MemberRoster.AddToCounts(retinue2Troop, 1);
                        }
                    }
                    float num = 2f * Campaign.Current.EstimatedAverageLordPartySpeed * (float)CampaignTime.HoursInDay;
                    foreach (Settlement settlement in Campaign.Current.Settlements)
                    {
                        if (settlement.IsVillage)
                        {
                            float num2;
                            float distance = Campaign.Current.Models.MapDistanceModel.GetDistance(newParty, settlement, false, newParty.NavigationCapability, out num2);
                            if (distance < num)
                            {
                                foreach (ValueTuple<ItemObject, float> valueTuple in settlement.Village.VillageType.Productions)
                                {
                                    ItemObject item = valueTuple.Item1;
                                    float item2 = valueTuple.Item2;
                                    float num3 = (item.ItemType == ItemObject.ItemTypeEnum.Horse && item.HorseComponent.IsRideable && !item.HorseComponent.IsPackAnimal) ? 7f : (item.IsFood ? 0.1f : 0f);
                                    float num4 = ((float)newParty.MemberRoster.TotalManCount + 2f) / 200f;
                                    float num5 = 1f - distance / num;
                                    int num6 = MBRandom.RoundRandomized(num3 * item2 * num5 * num4);
                                    if (num6 > 0)
                                    {
                                        newParty.ItemRoster.AddToCounts(item, num6);
                                    }
                                }
                            }
                        }
                    }
                }
                //onSuccess("Party created");
            }
        }

        private void HandleLeadCommand(Settings settings, Hero adoptedHero, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.LeadEnabled)
            {
                onFailure("{=OVxeTkYW}Leading clans is disabled".Translate());
                return;
            }
            if (adoptedHero.Clan == null)
            {
                onFailure("{=xH2geJ28}You are not in a clan".Translate());
                return;
            }
            if (adoptedHero.Clan == Hero.MainHero.Clan)
            {
                onFailure("{=jGg5q1JD}You cannot lead the players clan".Translate());
                return;
            }
            if (adoptedHero.IsClanLeader)
            {
                onFailure("{=cRpqnI3B}You are already the leader of your clan".Translate());
                return;
            }
            if ((adoptedHero.Occupation != Occupation.Lord) && (!settings.BuyTitleEnabled))
            {
                onFailure("{=xjAujM6b}You must be a noble to usurp a clan!".Translate());
                return;
            }
            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.LeadPrice)
            {
                onFailure(Naming.NotEnoughGold(settings.LeadPrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                return;
            }
            if (adoptedHero.Clan.Leader.Name.Contains(BLTAdoptAHeroModule.Tag) || adoptedHero.Clan.Leader.Name.Contains(BLTAdoptAHeroModule.DevTag))
            {
                if (!settings.LeadEnabled)
                {
                    onFailure("{=L7PFIoD6}Leading clans led by other BLT Heroes is disabled".Translate());
                    return;
                }
                Hero oldLeader = adoptedHero.Clan.Leader;
                if (MBRandom.RandomInt(0, 10) < MBMath.ClampInt(oldLeader.Level - adoptedHero.Level, 0, 10))
                {
                    BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.LeadPrice, true);
                    onFailure("{=PlG3BI1y}You have been bested in battle by {oldLeader} and failed to lead your clan".Translate(("oldLeader", oldLeader.Name.ToString())));
                    return;
                }
                else
                {
                    BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.LeadPrice, true);
                    adoptedHero.Clan.SetLeader(adoptedHero);
                    Log.ShowInformation("{=ZCYqf89T}{heroName} has usurped {oldLeader} and is now leading clan {clanName}!".Translate(("heroName", adoptedHero.Name.ToString()), ("oldLeader", oldLeader.Name.ToString()), ("clanName", adoptedHero.Clan.Name.ToString())), adoptedHero.CharacterObject, Log.Sound.Horns2);
                    onSuccess("{=nDZKenCx}You have successfully taken over the leadership of your clan".Translate());
                    return;
                }
            }
            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.LeadPrice, true);
            ChangeClanLeaderAction.ApplyWithSelectedNewLeader(adoptedHero.Clan, adoptedHero);
            onSuccess("{=MbMibbNm}You are now the leader of your clan".Translate());
            Log.ShowInformation("{=Zc5EPvQU}{heroName} is now leading clan {clanName}!".Translate(("heroName", adoptedHero.Name.ToString()), ("clanName", adoptedHero.Clan.Name.ToString())), adoptedHero.CharacterObject, Log.Sound.Horns2);
        }

        private void HandleRenameCommand(Settings settings, Hero adoptedHero, string desiredName, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.RenameEnabled)
            {
                onFailure("{=4pDk2rNm}Renaming clans is disabled".Translate());
                return;
            }
            if (string.IsNullOrWhiteSpace(desiredName))
            {
                onFailure("{=vjHYEbRR}(rename) (clan name)".Translate());
                return;
            }
            if (!adoptedHero.IsClanLeader)
            {
                onFailure("{=jQZ93EID}You are not the leader of your clan".Translate());
                return;
            }
            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.RenamePrice)
            {
                onFailure(Naming.NotEnoughGold(settings.RenamePrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                return;
            }

            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.RenamePrice, true);
            var fullClanName = $"[BLT Clan] {desiredName}";
            var oldName = adoptedHero.Clan.Name.ToString();
            adoptedHero.Clan.ChangeClanName(new TextObject(fullClanName), new TextObject(fullClanName));
            onSuccess("{=hNtBu8rx}Renamed clan to {name}".Translate(("name", fullClanName)));
            Log.ShowInformation("{=d3tUyvv3}{heroName} has renamed clan {oldName} to {clanName}!".Translate(("heroName", adoptedHero.Name.ToString()), ("oldName", oldName), ("clanName", adoptedHero.Clan.Name.ToString())), adoptedHero.CharacterObject, Log.Sound.Horns2);
        }

        private void HandleStatsCommand(Settings settings, Hero adoptedHero, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.StatsEnabled)
            {
                onFailure("{=9XKKVMKf}Clan stats is disabled".Translate());
                return;
            }
            if (adoptedHero.Clan == null)
            {
                onFailure("{=BLTClanNotInClan}You are not in a clan".Translate());
                return;
            }

            var clanStats = new StringBuilder();
            clanStats.Append("{=Ki8jvwkw}Clan Name: {name} | ".Translate(("name", adoptedHero.Clan.Name.ToString())));
            clanStats.Append("{=BLTClanStatsLeader}Leader: {leader} | ".Translate(("leader", adoptedHero.Clan.Leader.Name.ToString())));
            if (adoptedHero.Clan.Kingdom != null)
            {
                clanStats.Append("{=BLTClanStatsKingdom}Kingdom: {kingdom} | ".Translate(("kingdom", adoptedHero.Clan.Kingdom.Name.ToString())));
                clanStats.Append("{=6VFGXqRe}Influence: {influence} | ".Translate(("influence", Math.Round(adoptedHero.Clan.Influence).ToString())));
                if (adoptedHero.Clan.IsUnderMercenaryService)
                {
                    string mercGold = (adoptedHero.Clan.MercenaryAwardMultiplier * Math.Round(adoptedHero.Clan.Influence / 5f)).ToString() + "/" + adoptedHero.Clan.MercenaryAwardMultiplier.ToString();
                    clanStats.Append("{=PbxexPi9}Mercenary💰: {mercenary} | ".Translate(("mercenary", mercGold)));
                }
            }
            clanStats.Append("{=Sg11nEUe}Tier: {tier}({renown}) | ".Translate(("tier", adoptedHero.Clan.Tier.ToString()), ("renown", Math.Round(adoptedHero.Clan.Renown).ToString())));
            clanStats.Append("{=ZFGikYn8}Strength: {strength} | ".Translate(("strength", Math.Round(adoptedHero.Clan.CurrentTotalStrength).ToString())));
            if (adoptedHero.IsPrisoner && adoptedHero.PartyBelongedToAsPrisoner.IsMobile)
                clanStats.Append("{=BLTClanStatsPrisoner}Prisoner: {prisoner} | ".Translate(("prisoner", adoptedHero.PartyBelongedToAsPrisoner.Name.ToString())));
            if (adoptedHero.IsPrisoner && adoptedHero.PartyBelongedToAsPrisoner.IsSettlement)
                clanStats.Append("{=BLTClanStatsPrisoner}Prisoner: {prisoner} | ".Translate(("prisoner", adoptedHero.PartyBelongedToAsPrisoner.Settlement.Name.ToString())));
            int income = Campaign.Current.Models.ClanFinanceModel.CalculateClanGoldChange(adoptedHero.Clan).RoundedResultNumber;
            clanStats.Append("{=SDVLj0nw}Wealth: {wealth}({income}) | ".Translate(("wealth", adoptedHero.Clan.Leader.Gold.ToString()),("income", (income > 0 ? "+" : "")+income)));
            clanStats.Append("{=eHJYAZha}Members: {members} ".Translate(("members", adoptedHero.Clan.Heroes.Count.ToString())));
            int parties = 0;
            int ships = 0;
            if (adoptedHero.Clan.WarPartyComponents.Count > 0)
            {
                foreach (var partyComponent in adoptedHero.Clan.WarPartyComponents)
                {
                    MobileParty party = partyComponent.MobileParty;

                    if (party == null || party.LeaderHero == null) continue;

                    if (party.IsLordParty) parties += 1;
                    ships += party.Ships.Count;
                }
            }
            var partyLimit = Campaign.Current.Models.ClanTierModel.GetPartyLimitForTier(
                adoptedHero.Clan,
                adoptedHero.Clan.Tier
            );

            clanStats.Append("{=Ib213Hp9}| Parties: {cparties}/{mparties} | ".Translate(
                ("cparties", parties),
                ("mparties", partyLimit)
            ));
            clanStats.Append("{=BLTClanStatsShips}Ships: {ships} ".Translate(("ships", ships)));
            if (adoptedHero.Clan.Fiefs.Count >= 1)
            {
                int townCount = 0;
                int castleCount = 0;
                foreach (var settlement in adoptedHero.Clan.Fiefs)
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
            onSuccess(clanStats.ToString());
        }

        private void HandlePartyCommand(Settings settings, Hero adoptedHero, Action<string> onSuccess, Action<string> onFailure)
        {
            var partyStats = new StringBuilder();
            if (!settings.StatsEnabled)
            {
                onFailure("{=9XKKVMKf}Clan stats is disabled".Translate());
                return;
            }
            if (adoptedHero.Clan == null)
            {
                onFailure("{=yPeUCq8t}You are not in a clan".Translate());
                return;
            }
            int count = 0;
            var parties = adoptedHero.Clan.WarPartyComponents;
            var vassals = VassalBehavior.Current?.GetVassalClans(adoptedHero.Clan);
            partyStats.Append("{=BLTClanPartyClanHeader}{clan}:".Translate(("clan", adoptedHero.Clan.Name)));
            foreach (var wparty in parties)
            {
                var party1 = wparty?.MobileParty;
                if (party1 == null || party1.LeaderHero == null)
                    continue;
                count += 1;
                partyStats.Append("{=BLTClanPartyEntry}Party({number})[Leader: {leader} - Troops: {troops}] | "
                    .Translate(("number", count), ("leader", party1.LeaderHero.FirstName), ("troops", party1.MemberRoster.TotalHealthyCount)));
            }
            if (count == 0)
            {
                partyStats.Append("{=BLTClanNoParties}No parties | ".Translate());
            }
            if (vassals.Count > 0)
            {
                foreach (Clan vassal in vassals)
                {
                    int vcount = 0;
                    var vparties = vassal.WarPartyComponents;
                    partyStats.Append("{=BLTClanPartyClanHeader}{clan}:".Translate(("clan", vassal.Name)));
                    foreach (var vparty in vparties)
                    {
                        var party2 = vparty?.MobileParty;
                        if (party2 == null || party2.LeaderHero == null)
                            continue;
                        vcount += 1;
                        partyStats.Append("{=BLTClanPartyEntry}Party({number})[Leader: {leader} - Troops: {troops}] | "
                            .Translate(("number", vcount), ("leader", party2.LeaderHero.FirstName), ("troops", party2.MemberRoster.TotalHealthyCount)));
                    }
                    if (vcount == 0)
                    {
                        partyStats.Append("{=BLTClanNoParties}No parties | ".Translate());
                    }
                }
            }

            string partyString = partyStats.ToString().TrimEnd('|', ' ');

            onSuccess(partyString);
        }

        private void HandleFiefsCommand(Settings settings, Hero adoptedHero, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.FiefsEnabled)
            {
                onFailure("{=6q8NKGam}Clan fiefs is disabled".Translate());
                return;
            }
            if (adoptedHero.Clan == null)
            {
                onFailure("{=yPeUCq8t}You are not in a clan".Translate());
                return;
            }
            if (adoptedHero.Clan.Fiefs.Count == 0)
            {
                onFailure("{=ssqRB9Ye}You have no fiefs".Translate());
                return;
            }
            var fiefList = new StringBuilder();
            string townInfo = "";
            string castleInfo = "";

            foreach (Town f in adoptedHero.Clan.Fiefs)
            {

                int profit = (int)(
                    Campaign.Current.Models.SettlementTaxModel.CalculateTownTax(f, false).ResultNumber +
                    Campaign.Current.Models.ClanFinanceModel.CalculateTownIncomeFromTariffs(adoptedHero.Clan, f, false).ResultNumber +
                    Campaign.Current.Models.ClanFinanceModel.CalculateTownIncomeFromProjects(f) +
                    f.Settlement.BoundVillages.Sum(v => Campaign.Current.Models.ClanFinanceModel.CalculateVillageIncome(adoptedHero.Clan, v, false)) -
                    (f.GarrisonParty?.TotalWage ?? 0)
                    );
                if (f.IsCastle)
                {
                    castleInfo = castleInfo + f.Name.ToString() + "[";
                    castleInfo += "{=BLTClanFiefGovernor}Governor: {governor}".Translate(("governor", f?.Governor?.Name?.ToString() ?? "{=BLTClanNone}None".Translate()));
                    castleInfo += "{=BLTClanFiefIncome}, Income💰: {income}".Translate(("income", profit));
                    castleInfo += "{=BLTClanFiefForces}, M/G⚔: {militia}/{garrison}".Translate(("militia", (int)f.Militia), ("garrison", f.GarrisonParty?.MemberRoster.TotalHealthyCount ?? 0));
                    if (f.IsUnderSiege && f.Settlement.SiegeEvent != null)
                    {
                        castleInfo += "{=BLTClanFiefUnderSiege}, Under siege] ".Translate();
                    }
                    else castleInfo += "] ";
                }
                if (!f.IsCastle)
                {
                    townInfo = townInfo + f.Name.ToString() + "[";
                    townInfo += "{=BLTClanFiefGovernor}Governor: {governor}".Translate(("governor", f?.Governor?.Name?.ToString() ?? "{=BLTClanNone}None".Translate()));
                    townInfo += "{=BLTClanFiefIncome}, Income💰: {income}".Translate(("income", profit));
                    townInfo += "{=BLTClanFiefForces}, M/G⚔: {militia}/{garrison}".Translate(("militia", (int)f.Militia), ("garrison", f.GarrisonParty?.MemberRoster.TotalHealthyCount ?? 0));
                    if (f.IsUnderSiege && f.Settlement.SiegeEvent != null)
                    {
                        townInfo += "{=BLTClanFiefUnderSiege}, Under siege] ".Translate();
                    }
                    else townInfo += "] ";
                }
            }
            fiefList.Append("{=BwuFSJU1}Towns: {towns} | ".Translate(("towns", (object)townInfo)));
            fiefList.Append("{=0rMNNQ7R}Castles: {castles}".Translate(("castles", (object)castleInfo)));
            onSuccess(fiefList.ToString());
        }

        private void HandleLeaveCommand(Settings settings, Hero adoptedHero, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.LeaveEnabled)
            {
                onFailure("{=BLTClanLeaveDisabled}Leaving clans is disabled".Translate());
                return;
            }
            if (adoptedHero.Clan == null)
            {
                onFailure("{=yPeUCq8t}You are not in a clan".Translate());
                return;
            }
            if (adoptedHero.IsClanLeader)
            {
                onFailure("{=cRpqnI3B}You are already the leader of your clan".Translate());
                return;
            }
            var oldParty = adoptedHero.PartyBelongedTo;
            if (oldParty != null && oldParty.MapEvent != null)
            {
                onFailure("{=BLTClanHeroInBattle}Your hero is in battle! Try again later".Translate());
                return;
            }
            if (adoptedHero.GovernorOf != null)
            {
                ChangeGovernorAction.RemoveGovernorOf(adoptedHero);
            }
            if (oldParty != null)
            {
                bool wasLeader = oldParty.LeaderHero == adoptedHero;
                oldParty.MemberRoster.RemoveTroop(adoptedHero.CharacterObject, 1, default(UniqueTroopDescriptor), 0);
                if (wasLeader && oldParty.IsLordParty)
                    DisbandPartyAction.StartDisband(oldParty);
            }
            var oldClan = adoptedHero.Clan;
            if (adoptedHero.IsClanLeader)
            {
                ChangeClanLeaderAction.ApplyWithoutSelectedNewLeader(oldClan);
            }
            adoptedHero.SetNewOccupation(Occupation.Wanderer);
            
            adoptedHero.Clan = null;

            var targetSettlement = Settlement.All.Where(s => s.IsTown).SelectRandom();
            EnterSettlementAction.ApplyForCharacterOnly(adoptedHero, targetSettlement);
            onSuccess("{=BLTClanLeft}Your hero has left {clan}".Translate(("clan", oldClan.Name)));
        }

        private void HandleShipCommand(Settings settings, Hero adoptedHero, string desiredName, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.BuyShipEnabled)
            {
                onFailure("{=BLTClanShipDisabled}Ship buying is disabled".Translate());
                return;
            }
            if (adoptedHero.Clan == null)
            {
                onFailure("{=yPeUCq8t}You are not in a clan".Translate());
                return;
            }
            var cult = adoptedHero.Culture;
            var clan = adoptedHero.Clan;
            var party = adoptedHero.PartyBelongedTo;
            int limit = Campaign.Current.Models.ClanTierModel.GetPartyLimitForTier(clan, clan.Tier) * 3;
            if (clan.WarPartyComponents.Sum(w => w.Party.Ships.Count()) >= limit)
            {
                onFailure("{=BLTClanMaxShips}Maximum ships reached ({limit})".Translate(("limit", limit)));
                return;
            }
            if (party == null || party.MapEvent != null)
            {
                party = clan.WarPartyComponents.Select(p => p.MobileParty).Where(p => p.MapEvent == null).SelectRandom();
            }
            if (party == null)
            {
                onFailure("{=BLTClanNoValidParties}Your clan has no valid parties".Translate());
                return;
            }

            ShipHull.ShipType type;

            string shipType = GetShipType(desiredName);
            switch (shipType)
            {
                case "light":
                    type = ShipHull.ShipType.Light;
                    break;

                case "medium":
                    type = ShipHull.ShipType.Medium;
                    break;

                case "heavy":
                    type = ShipHull.ShipType.Heavy;
                    break;

                default:
                    onFailure("{=BLTClanInvalidShipType}Invalid ship type. Use light/medium/heavy".Translate());
                    return;
            }

            var hulls = cult.AvailableShipHulls.Where(h => h.Type == type).ToList();
            if (hulls == null || hulls.Count == 0) 
                hulls = MBObjectManager.Instance.GetObjectTypeList<ShipHull>().Where(h => h.Type == type).ToList();

            var hull = hulls.SelectRandom();
            if (hull == null)
            {
                onFailure("{=BLTClanNoHulls}No ship hulls are available".Translate());
                return;
            }
            Ship newShip = new Ship(hull);
            ChangeShipOwnerAction.ApplyByProduction(party.Party, newShip);
            onSuccess("{=BLTClanShipBought}Bought {type} ship: {ship}".Translate(
                ("type", GetShipTypeDisplayName(shipType)), ("ship", hull.Name)));
        }

        private static string GetShipType(string value)
        {
            if (MatchesCommand(value, "{=BLTClanShipLight}light".Translate(), "light")) return "light";
            if (MatchesCommand(value, "{=BLTClanShipMedium}medium".Translate(), "medium")) return "medium";
            if (MatchesCommand(value, "{=BLTClanShipHeavy}heavy".Translate(), "heavy")) return "heavy";
            return value.ToLowerInvariant();
        }

        private static string GetShipTypeDisplayName(string value) => value switch
        {
            "light" => "{=BLTClanShipLight}light".Translate(),
            "medium" => "{=BLTClanShipMedium}medium".Translate(),
            "heavy" => "{=BLTClanShipHeavy}heavy".Translate(),
            _ => value
        };

        private void HandleBuyTitleCommand(Settings settings, Hero adoptedHero, Action<string> onSuccess, Action<string> onFailure)
        {
            if ((adoptedHero.Occupation == Occupation.Lord) && (!settings.BuyTitleEnabled))
            {
                onFailure("{=BLTClanBuyTitleDisabledAlreadyNoble}Buying noble titles is disabled, and you are already a noble!".Translate());
                return;
            }
            if (!settings.BuyTitleEnabled)
            {
                onFailure("{=BLTClanBuyTitleDisabled}Buying noble titles is disabled".Translate());
                return;
            }
            if (adoptedHero.Occupation == Occupation.Lord)
            {
                onFailure("{=BLTClanAlreadyNoble}You are already a noble!".Translate());
                return;
            }
            if (adoptedHero.Clan == null)
            {
                onFailure("{=yPeUCq8t}You are not in a clan".Translate());
                return;
            }
            if (BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero) < settings.TitlePrice)
            {
                onFailure(Naming.NotEnoughGold(settings.TitlePrice, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                return;
            }

            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.TitlePrice, true);
            adoptedHero.SetNewOccupation(Occupation.Lord);
            onSuccess("{=6yQUu78N}{heroName} has become a noble!".Translate(("heroName", adoptedHero.Name.ToString())));
        }

        private Dictionary<Hero, string> _bannerBuffer = new ();
        private void HandleBannerCommand(Settings settings, Hero adoptedHero, string bannerCode, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.EditBannerEnabled)
            {
                onFailure("{=sJdd2kgr}Editing banners is disabled".Translate());
                return;
            }
            if (adoptedHero.Clan == null)
            {
                onFailure("{=yPeUCq8t}You are not in a clan".Translate());
                return;
            }
            if (!adoptedHero.IsClanLeader)
            {
                onFailure("{=jQZ93EID}You are not the leader of your clan".Translate());
                return;
            }
            if (string.IsNullOrWhiteSpace(bannerCode))
            {
                onFailure("{=PSDbhv3a}Make your banner at https://bannerlord.party/banner and paste it directly".Translate());
                return;
            }
            string bannerSubcommand = GetBannerSubcommand(bannerCode);
            if (bannerSubcommand == "start")
            {
                if (_bannerBuffer.ContainsKey(adoptedHero))
                {
                    onFailure("{=BLTClanBannerAlreadyStarted}Banner input has already started".Translate());
                    return;
                }

                _bannerBuffer[adoptedHero] = "";
                onSuccess("{=BLTClanBannerStarted}Banner input started. Send lines. Use 'end' to finish.".Translate());
                return;
            }
            if (bannerSubcommand == "end")
            {
                if (!_bannerBuffer.TryGetValue(adoptedHero, out string stored))
                {
                    onFailure("{=BLTClanBannerNotStarted}Banner input was not started".Translate());
                    return;
                }

                bannerCode = stored;
                _bannerBuffer.Remove(adoptedHero);
            }
            else if (_bannerBuffer.TryGetValue(adoptedHero, out string current))
            {
                _bannerBuffer[adoptedHero] = current + bannerCode;
                onSuccess("{=BLTClanBannerLineAdded}Line added".Translate());
                return;
            }
            try
            {
                if (Banner.TryGetBannerDataFromCode(bannerCode, out var bannerDataList))
                {
                    var newBanner = new Banner(bannerCode);  // creates a Banner object directly from the code                  
                    var color1 = newBanner.GetPrimaryColor();
                    var color2 = newBanner.GetFirstIconColor();


                    adoptedHero.Clan.Banner = newBanner;
                    adoptedHero.Clan.Banner.ChangeBackgroundColor(color1, color2);
                    adoptedHero.Clan.Color = color1;
                    adoptedHero.Clan.Color2 = color2;
                    if (adoptedHero.IsKingdomLeader)
                    {
                        adoptedHero.Clan.Kingdom.Banner = newBanner;
                        adoptedHero.Clan.Kingdom.Banner.ChangeBackgroundColor(color1, color2);
                    }
                }
                onSuccess("{=BiiO7KQx}Banner updated successfully!".Translate());
            }
            catch (Exception ex)
            {
                onFailure("{=BLTClanBannerUpdateFailed}Failed to update banner: {error}".Translate(("error", ex.Message)));
            }
        }

        private static string GetBannerSubcommand(string value)
        {
            if (MatchesCommand(value, "{=BLTClanBannerStart}start".Translate(), "start")) return "start";
            if (MatchesCommand(value, "{=BLTClanBannerEnd}end".Translate(), "end")) return "end";
            return value;
        }

        private void HandleHomeCommand(Settings settings, Hero adoptedHero, string desiredName, Action<string> onSuccess, Action<string> onFailure)
        {
            if (!settings.SelectHome) 
            {
                onFailure("{=BLTClanHomeDisabled}Setting clan home is disabled".Translate());
                return;
            }
            if (adoptedHero.Clan == null)
            {
                onFailure("{=yPeUCq8t}You are not in a clan".Translate());
                return;
            }
            if (!adoptedHero.IsClanLeader)
            {
                onFailure("{=jQZ93EID}You are not the leader of your clan".Translate());
                return;
            }
            if (string.IsNullOrEmpty(desiredName))
            {
                onSuccess("{=BLTClanCurrentHome}Clan home is {settlement}".Translate(("settlement", adoptedHero.Clan.HomeSettlement.Name)));
                return;
            }
            if (adoptedHero.Clan.Fiefs.Count > 0)
            {               
                Settlement newHome = adoptedHero.Clan.Fiefs.FirstOrDefault(t => t.Name.ToString().IndexOf(desiredName, StringComparison.InvariantCultureIgnoreCase) > 0).Settlement;
                if (newHome == null)
                {
                    onFailure("{=BLTClanHomeNotFoundInFiefs}No settlement named {name}. Choose from your clan fiefs".Translate(("name", desiredName)));
                    return;
                }

                // --- Update clan home ---
                var homeProp = typeof(Clan).GetProperty(
                    "HomeSettlement",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
                );
                homeProp.SetValue(adoptedHero.Clan, newHome);

                // --- Update all heroes in clan ---
                foreach (var hero in adoptedHero.Clan.Heroes)
                {
                    hero.UpdateHomeSettlement();
                }

                var vassals = VassalBehavior.Current?.GetVassalClans(adoptedHero.Clan);
                foreach (Clan vassal in vassals)
                {
                    if (vassal.Fiefs.Count > 0) { vassal.ConsiderAndUpdateHomeSettlement(); continue; }

                    var vassalHomeProp = typeof(Clan).GetProperty(
                        "HomeSettlement",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
                    );
                    vassalHomeProp.SetValue(vassal, newHome);

                    foreach (var hero in vassal.Heroes)
                    {
                        hero.UpdateHomeSettlement();
                    }
                }
                onSuccess("{=BLTClanHomeUpdated}Clan home updated to {settlement}".Translate(("settlement", newHome.Name)));
            }
            else
            {
                bool hasKingdom = adoptedHero.Clan.Kingdom != null;
                Settlement newHome = hasKingdom ? adoptedHero.Clan.Kingdom.Fiefs.FirstOrDefault(t => t.Name.ToString().IndexOf(desiredName, StringComparison.InvariantCultureIgnoreCase) >= 0).Settlement : Town.AllFiefs.FirstOrDefault(t => t.Name.ToString().IndexOf(desiredName, StringComparison.InvariantCultureIgnoreCase) >= 0).Settlement;
                if (newHome == null)
                {
                    onFailure(hasKingdom
                        ? "{=BLTClanHomeNotFoundInKingdom}No settlement named {name}. Choose from your kingdom's fiefs".Translate(("name", desiredName))
                        : "{=BLTClanHomeNotFound}No settlement named {name}".Translate(("name", desiredName)));
                    return;
                }

                // --- Update clan home ---
                var homeProp = typeof(Clan).GetProperty(
                    "HomeSettlement",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
                );
                homeProp.SetValue(adoptedHero.Clan, newHome);

                // --- Update all heroes in clan ---
                foreach (var hero in adoptedHero.Clan.Heroes)
                {
                    hero.UpdateHomeSettlement();
                }

                var vassals = VassalBehavior.Current?.GetVassalClans(adoptedHero.Clan);
                foreach (Clan vassal in vassals)
                {
                    if (vassal.Fiefs.Count > 0) { vassal.ConsiderAndUpdateHomeSettlement(); continue; }

                    var vassalHomeProp = typeof(Clan).GetProperty(
                        "HomeSettlement",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
                    );
                    vassalHomeProp.SetValue(vassal, newHome);

                    foreach (var hero in vassal.Heroes)
                    {
                        hero.UpdateHomeSettlement();
                    }
                }
                onSuccess("{=BLTClanHomeUpdated}Clan home updated to {settlement}".Translate(("settlement", newHome.Name)));
            }
        }
    }
}
