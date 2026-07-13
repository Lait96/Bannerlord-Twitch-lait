using System;
using BannerlordTwitch;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Rewards;
using BannerlordTwitch.Util;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace BLTAdoptAHero
{
    [LocDisplayName("{=fmftNyHh}Discard Item"),
     LocDescription("{=f3LrrLHP}Allows viewers to discard one of their own custom items"),
     UsedImplicitly]
    public class DiscardItem : HeroCommandHandlerBase
    {
        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config,
            Action<string> onSuccess, Action<string> onFailure)
        {
            if (string.IsNullOrWhiteSpace(context.Args))
            {
                ActionManager.SendReply(context, context.ArgsErrorMessage("{=by80aboy}(custom item index)".Translate()));
                return;
            }

            if (BLTAdoptAHeroCampaignBehavior.Current.GetCustomItems(adoptedHero).Count == 0)
            {
                ActionManager.SendReply(context, "{=oXQ4En4P}You have no items to discard".Translate());
                return;
            }

            (var element, string error) = BLTAdoptAHeroCampaignBehavior.Current.FindCustomItemByIndex(adoptedHero, context.Args);
            if (element.IsEqualTo(EquipmentElement.Invalid))
            {
                ActionManager.SendReply(context, error ?? "{=DiscardItem_UnknownError}(unknown error)".Translate());
                return;
            }

            BLTAdoptAHeroCampaignBehavior.Current.DiscardCustomItem(adoptedHero, element);

            ActionManager.SendReply(context,
                "{=bNqd3AzN}'{ItemName}' was discarded"
                    .Translate(
                        ("ItemName", RewardHelpers.GetItemNameAndModifiers(element))
                        ));
        }
    }
}
