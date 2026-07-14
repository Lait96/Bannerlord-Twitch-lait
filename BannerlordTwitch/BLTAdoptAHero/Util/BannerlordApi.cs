using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;

namespace BLTAdoptAHero
{
    internal static class BannerlordApi
    {
        public static bool IsLordEquipmentTemplate(MBEquipmentRoster roster)
        {
#if BL_1_3_15
            return roster.HasEquipmentFlags(EquipmentFlags.IsNobleTemplate);
#else
            return roster.EquipmentCategories.HasFlag(EquipmentCategories.IsLordTemplate);
#endif
        }

        public static bool IsChildEquipmentTemplate(MBEquipmentRoster roster)
        {
#if BL_1_3_15
            return roster.HasEquipmentFlags(EquipmentFlags.IsChildEquipmentTemplate);
#else
            return roster.EquipmentCategories.HasFlag(EquipmentCategories.IsChildEquipmentTemplate);
#endif
        }

        public static bool IsTeenagerEquipmentTemplate(MBEquipmentRoster roster)
        {
#if BL_1_3_15
            return roster.HasEquipmentFlags(EquipmentFlags.IsTeenagerEquipmentTemplate);
#else
            return roster.EquipmentCategories.HasFlag(EquipmentCategories.IsTeenagerEquipmentTemplate);
#endif
        }

        public static bool HasTradeAgreement(
            TradeAgreementsCampaignBehavior behavior,
            Kingdom firstKingdom,
            Kingdom secondKingdom)
        {
#if BL_1_3_15
            return behavior.HasTradeAgreement(firstKingdom, secondKingdom);
#else
            return behavior.HasTradeAgreement(firstKingdom, secondKingdom, out _);
#endif
        }
    }
}
