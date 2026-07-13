using System;
using System.Linq;
using System.Collections.Generic;
using BannerlordTwitch;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Rewards;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using BLTAdoptAHero;
using BLTAdoptAHero.Behaviors;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero.Actions
{
    [LocDisplayName("{=BLTCampaignLogsDisplayName}Campaign Logs"),
     LocDescription("{=BLTCampaignLogsDescription}Shows relevant campaign logs"),
     UsedImplicitly]
    public class CampaignLogs : HeroCommandHandlerBase
    {
        public static Settings CurrentSettings { get; private set; } = new Settings();
        [CategoryOrder("General", 0)]
        public class Settings : IDocumentable
        {
            // General
            [LocDisplayName("{=BLTCampaignLogsHero}Hero"),
             LocCategory("General", "{=BLTCampaignLogsCategoryGeneral}General"),
             LocDescription("{=BLTCampaignLogsMaxHeroLogs}Maximum logs per hero"),
             PropertyOrder(1), UsedImplicitly]
            public int hLogs { get; set; } = 10;

            [LocDisplayName("{=BLTCampaignLogsClan}Clan"),
             LocCategory("General", "{=BLTCampaignLogsCategoryGeneral}General"),
             LocDescription("{=BLTCampaignLogsMaxClanLogs}Maximum logs per clan"),
             PropertyOrder(2), UsedImplicitly]
            public int cLogs { get; set; } = 10;

            [LocDisplayName("{=BLTCampaignLogsKingdom}Kingdom"),
             LocCategory("General", "{=BLTCampaignLogsCategoryGeneral}General"),
             LocDescription("{=BLTCampaignLogsMaxKingdomLogs}Maximum logs per kingdom"),
             PropertyOrder(3), UsedImplicitly]
            public int kLogs { get; set; } = 10;

            [LocDisplayName("{=BLTCampaignLogsFief}Fief"),
             LocCategory("General", "{=BLTCampaignLogsCategoryGeneral}General"),
             LocDescription("{=BLTCampaignLogsMaxFiefLogs}Maximum logs per fief"),
             PropertyOrder(4), UsedImplicitly]
            public int fLogs { get; set; } = 10;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                generator.Value("{=BLTCampaignLogsDocumentation}Usage: !logs hero/clan (clan)/kingdom (kingdom)/fief (fief)".Translate());
            }

        }
        public override Type HandlerConfigType => typeof(Settings);

        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config,
            Action<string> onSuccess, Action<string> onFailure)
        {
            if (config is not Settings settings) return;
            if (adoptedHero == null)
            {
                onFailure(AdoptAHero.NoHeroMessage);
                return;
            }
            if (context.Args.Length == 0)
            {
                onFailure("{=BLTCampaignLogsInvalidArguments}Invalid. Use !logs hero/clan (clan)/kingdom (kingdom)/fief (fief)".Translate());
                return;
            }
            var splitArgs = context.Args.Split(' ');
            var mode = GetCampaignLogsMode(splitArgs[0]);
            string name = string.Join(" ", splitArgs.Skip(1));

            switch (mode)
            {
                case "hero":
                    HandleHeroLogs(adoptedHero, context, onFailure);
                    break;
                case "clan":
                    HandleClanLogs(adoptedHero, name, context, onFailure);
                    break;
                case "kingdom":
                    HandleKingdomLogs(adoptedHero, name, context, onFailure);
                    break;
                case "fief":
                    HandleFiefLogs(name, context, onFailure);
                    break;
                default:
                    onFailure("{=BLTCampaignLogsInvalidArguments}Invalid. Use !logs hero/clan (clan)/kingdom (kingdom)/fief (fief)".Translate());
                    break;
            }

        }

        private string GetCampaignLogsMode(string mode)
        {
            if (MatchesCommand(mode, "{=BLTCampaignLogsSubHero}hero".Translate(), "hero")) return "hero";
            if (MatchesCommand(mode, "{=BLTCampaignLogsSubClan}clan".Translate(), "clan")) return "clan";
            if (MatchesCommand(mode, "{=BLTCampaignLogsSubKingdom}kingdom".Translate(), "kingdom")) return "kingdom";
            if (MatchesCommand(mode, "{=BLTCampaignLogsSubFief}fief".Translate(), "fief")) return "fief";

            return mode;
        }

        private bool MatchesCommand(string command, string translatedCommand, string defaultCommand)
            => command.Equals(defaultCommand, StringComparison.OrdinalIgnoreCase)
               || command.Equals(translatedCommand, StringComparison.OrdinalIgnoreCase);

        private void HandleHeroLogs(Hero hero, ReplyContext context, Action<string> onFailure)
        {
            var logsBehavior = Campaign.Current.GetCampaignBehavior<BLTLogsBehavior>();

            if (logsBehavior == null)
            {
                onFailure("{=BLTCampaignLogsBehaviorNotFound}Logs behavior not found.".Translate());
                return;
            }

            var dict = logsBehavior.heroLogs._heroLogs;

            if (!dict.TryGetValue(hero.StringId, out var logs) || logs.Count == 0)
            {
                onFailure("{=BLTCampaignLogsNoHeroLogs}No logs found for this hero.".Translate());
                return;
            }

            //Reply(hero.Name.ToString(), logs, onSuccess);
            ActionManager.SendReply(context, logs.ToArray());
        }

        private void HandleClanLogs(Hero adoptedHero, string filter, ReplyContext context, Action<string> onFailure)
        {
            var logsBehavior = Campaign.Current.GetCampaignBehavior<BLTLogsBehavior>();

            if (logsBehavior == null)
            {
                onFailure("{=BLTCampaignLogsBehaviorNotFound}Logs behavior not found.".Translate());
                return;
            }

            Clan desiredClan = adoptedHero.Clan;
            if (string.IsNullOrWhiteSpace(filter) && desiredClan == null)
            {
                onFailure("{=BLTCampaignLogsNeedClanName}Need clan name".Translate());
                return;
            }
            else if (!string.IsNullOrWhiteSpace(filter))
            {
                desiredClan = Clan.All.Where(k => !k.IsEliminated).FirstOrDefault(c =>
                c.Name.ToString().ToLower() == filter.ToLower()) ?? Clan.All.FirstOrDefault(c =>
                c.Name.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            if (desiredClan == null)
            {
                onFailure("{=BLTCampaignLogsClanNotFound}Could not find a clan with the name {name}".Translate(("name", filter)));
                return;
            }


            var dict = logsBehavior.clanLogs._clanLogs;
            if (!dict.TryGetValue(desiredClan.StringId, out var logs) || logs.Count == 0)
            {
                onFailure("{=BLTCampaignLogsNoClanLogs}No logs found for this clan.".Translate());
                return;
            }

            //Reply(desiredClan.Name.ToString(), logs, onSuccess);
            ActionManager.SendReply(context, logs.ToArray());
        }

        private void HandleKingdomLogs(Hero adoptedHero, string filter, ReplyContext context, Action<string> onFailure)
        {
            var logsBehavior = Campaign.Current.GetCampaignBehavior<BLTLogsBehavior>();

            if (logsBehavior == null)
            {
                onFailure("{=BLTCampaignLogsBehaviorNotFound}Logs behavior not found.".Translate());
                return;
            }

            Kingdom desiredKingdom = adoptedHero?.Clan?.Kingdom;
            if (string.IsNullOrWhiteSpace(filter) && desiredKingdom == null)
            {
                onFailure("{=BLTCampaignLogsNeedKingdomName}Need kingdom name".Translate());
                return;
            }
            else if (!string.IsNullOrWhiteSpace(filter))
            {
                desiredKingdom = Kingdom.All.Where(k => !k.IsEliminated).FirstOrDefault(c =>
                c.Name.ToString().ToLower() == filter.ToLower()) ?? Kingdom.All.FirstOrDefault(c =>
                c.Name.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            if (desiredKingdom == null)
            {
                onFailure("{=BLTCampaignLogsKingdomNotFound}Could not find a kingdom with the name {name}".Translate(("name", filter)));
                return;
            }



            var dict = logsBehavior.kingdomLogs._kingdomLogs;

            if (!dict.TryGetValue(desiredKingdom.StringId, out var logs) || logs.Count == 0)
            {
                onFailure("{=BLTCampaignLogsNoKingdomLogs}No logs found for this kingdom.".Translate());
                return;
            }

            //Reply(desiredKingdom.Name.ToString(), logs, onSuccess);
            ActionManager.SendReply(context, logs.ToArray());
        }

        private void HandleFiefLogs(string filter, ReplyContext context, Action<string> onFailure)
        {
            var logsBehavior = Campaign.Current.GetCampaignBehavior<BLTLogsBehavior>();

            if (logsBehavior == null)
            {
                onFailure("{=BLTCampaignLogsBehaviorNotFound}Logs behavior not found.".Translate());
                return;
            }

            if (string.IsNullOrWhiteSpace(filter))
            {
                onFailure("{=BLTCampaignLogsNeedFiefName}Need fief name".Translate());
                return;
            }

            var desiredFief = Town.AllFiefs.FirstOrDefault(c =>
                c.Name.ToString().ToLower() == filter.ToLower());
            if (desiredFief == null)
            {
                desiredFief = Town.AllFiefs.FirstOrDefault(c =>
                c.Name.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (desiredFief == null)
            {
                onFailure("{=BLTCampaignLogsFiefNotFound}Could not find a fief with the name {name}".Translate(("name", filter)));
                return;
            }


            var dict = logsBehavior.fiefLogs._fiefLogs;

            if (!dict.TryGetValue(desiredFief.StringId, out var logs) || logs.Count == 0)
            {
                onFailure("{=BLTCampaignLogsNoFiefLogs}No logs found for this fief.".Translate());
                return;
            }
            //Reply(desiredFief.Name.ToString(), logs, onSuccess);
            ActionManager.SendReply(context, logs.ToArray());
        }

        private void Reply(string name, List<string> logs, Action<string> onSuccess)
        {
            const int maxLength = 500;
            string prefix = $"{name}:";
            string currentMessage = prefix;

            foreach (var log in logs)
            {
                string separator = currentMessage == prefix ? "" : " | ";
                string nextEntry = separator + log;

                // Check if adding this entry would exceed limit
                if (currentMessage.Length + nextEntry.Length > maxLength)
                {
                    // Send current message and start a new one
                    if (currentMessage != prefix)
                    {
                        onSuccess(currentMessage);
                    }
                    currentMessage = prefix + log;
                }
                else
                {
                    currentMessage += nextEntry;
                }
            }

            // Send remaining message
            if (currentMessage != prefix)
            {
                onSuccess(currentMessage);
            }
        }
    }
}
