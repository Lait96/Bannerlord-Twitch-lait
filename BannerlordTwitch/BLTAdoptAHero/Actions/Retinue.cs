using System;
using BannerlordTwitch;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Rewards;
using BannerlordTwitch.Util;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero
{
    [LocDisplayName("{=tLSFX9Xc}Retinue"),
     LocDescription("{=bhC3VcmU}Add and improve adopted heroes retinue"),
     UsedImplicitly]
    public class Retinue : ActionHandlerBase
    {
        private class Settings : IDocumentable
        {
            [LocDisplayName("{=tLSFX9Xc}Retinue"),
             LocDescription("{=iNoFrKsN}Retinue Upgrade Settings"),
             PropertyOrder(1), ExpandableObject, Expand, UsedImplicitly]
            public BLTAdoptAHeroCampaignBehavior.RetinueSettings Retinue { get; set; } = new();

            [LocDisplayName("{=nIsuuFMC}All By Default"),
             LocDescription("{=mJSGvWlR}Whether this action should attempt to buy/upgrade as many times as possible when called with no parameter."),
             PropertyOrder(2), UsedImplicitly]
            public bool AllByDefault { get; set; } = true;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                Retinue.GenerateDocumentation(generator);
            }
        }

        protected override Type ConfigType => typeof(Settings);

        protected override void ExecuteInternal(ReplyContext context, object config, Action<string> onSuccess, Action<string> onFailure)
        {
            var settings = (Settings)config;
            var adoptedHero = BLTAdoptAHeroCampaignBehavior.Current.GetAdoptedHero(context.UserName);

            if (adoptedHero == null)
            {
                onFailure(AdoptAHero.NoHeroMessage);
                return;
            }

            if (Mission.Current != null && IsHeroInCurrentMission(adoptedHero))
            {
                onFailure("{=mCcpMwrN}You cannot modify retinue while you are active in the current mission!".Translate());
                return;
            }
            
            int numToUpgrade = settings.AllByDefault ? int.MaxValue : 1;

            if (!string.IsNullOrEmpty(context.Args))
            {
                var args = context.Args.Split(' ');

                // Handle !retinue clear <index>
                if (args.Length > 0 && string.Compare(args[0], "clear", StringComparison.CurrentCultureIgnoreCase) == 0)
                {
                    if (args.Length > 1 && int.TryParse(args[1], out int index))
                    {
                        BLTAdoptAHeroCampaignBehavior.Current.KillRetinueAtIndex(adoptedHero, index - 1);
                        onSuccess($"Removed retinue at slot {index}.");
                    }
                    else
                    {
                        onFailure("You must specify a valid retinue index to clear.");
                    }
                    return; // exit after clear
                }

                // Handle upgrades (!retinue all or !retinue <number>)
                if (string.Compare(args[0], "all", StringComparison.CurrentCultureIgnoreCase) == 0)
                {
                    numToUpgrade = int.MaxValue;
                }
                else if (!int.TryParse(args[0], out numToUpgrade) || numToUpgrade <= 0)
                {
                    onFailure(context.ArgsErrorMessage("{=NexXxYvj}(number, or all)".Translate()));
                    return;
                }
            }

            // Perform upgrade
            (bool success, string status) = BLTAdoptAHeroCampaignBehavior.Current
                .UpgradeRetinue(adoptedHero, settings.Retinue, numToUpgrade);

            if (success)
                onSuccess(status);
            else
                onFailure(status);
        }
        
        private static bool IsHeroInCurrentMission(Hero hero)
        {
            if (hero == null || Mission.Current == null || BLTSummonBehavior.Current == null)
                return false;

            var summonState = BLTSummonBehavior.Current.GetHeroSummonState(hero);
            if (summonState == null)
                return false;

            if (summonState.TimesSummoned > 0)
                return true;

            if (summonState.CurrentAgent != null)
                return true;

            if (summonState.ActiveRetinue > 0 || summonState.ActiveRetinue2 > 0)
                return true;

            return false;
        }
    }
}
