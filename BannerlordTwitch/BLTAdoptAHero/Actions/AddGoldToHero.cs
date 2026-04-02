using System;
using BannerlordTwitch;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Rewards;
using JetBrains.Annotations;

namespace BLTAdoptAHero
{
    [LocDisplayName("{=action_add_gold_to_hero_name}Add Gold To Hero"),
     LocDescription("{=action_add_gold_to_hero_desc}Gives gold to the adopted hero"),
     UsedImplicitly]
    internal class AddGoldToHero : IRewardHandler
    {
        private class Settings : IDocumentable
        {
            [LocDisplayName("{=action_add_gold_to_hero_amount_name}Amount"),
             LocDescription("{=action_add_gold_to_hero_amount_desc}How much gold to give the adopted hero"),
             UsedImplicitly, Document]
            public int Amount { get; set; }

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                generator.PropertyValuePair("Amount", $"{Amount}{Naming.Gold}");
            }
        }

        Type IRewardHandler.RewardConfigType => typeof(Settings);
        void IRewardHandler.Enqueue(ReplyContext context, object config)
        {
            var settings = (Settings)config;
            var adoptedHero = BLTAdoptAHeroCampaignBehavior.Current.GetAdoptedHero(context.UserName);
            if (adoptedHero == null)
            {
                ActionManager.NotifyCancelled(context, AdoptAHero.NoHeroMessage);
                return;
            }
            int newGold = BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, settings.Amount);

            ActionManager.NotifyComplete(context, $"{Naming.Inc}{settings.Amount}{Naming.Gold}{Naming.To}{newGold}{Naming.Gold}");
        }

    }
}