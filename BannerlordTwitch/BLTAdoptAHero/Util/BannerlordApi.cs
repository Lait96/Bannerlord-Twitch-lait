using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using NavalDLC.Missions;
using NavalDLC.Missions.Objects;

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

        public static bool IsNavalRaidBattle(Mission mission)
        {
#if BL_1_3_15
            return false;
#else
            return mission?.IsNavalRaidBattle == true;
#endif
        }

        public static bool CanAgentsNavigateToEachOther(Agent first, Agent second)
        {
            if (first == null || second == null)
                return false;

            var mission = Mission.Current;
            if (mission?.IsNavalBattle != true && !IsNavalRaidBattle(mission))
                return true;

            var firstNaval = first.GetComponent<AgentNavalComponent>();
            var secondNaval = second.GetComponent<AgentNavalComponent>();
            var firstShip = firstNaval?.SteppedShip;
            var secondShip = secondNaval?.SteppedShip;
            if (firstShip != null && secondShip != null)
            {
                if (firstShip == secondShip)
                    return true;

                ulong firstIsland = firstNaval.GetSteppedCombinedShipIsland();
                ulong secondIsland = secondNaval.GetSteppedCombinedShipIsland();
                return firstIsland != 0UL && firstIsland == secondIsland;
            }

            var destination = second.Position;
            float pathDistance = first.GetPathDistanceToPoint(ref destination);
            return pathDistance >= 0f && !float.IsNaN(pathDistance) && !float.IsInfinity(pathDistance);
        }

        public static bool IsAvailableForNavalSpawn(MissionShip ship)
        {
            if (ship == null
                || !ship.IsInitialized
                || !ship.IsDeployed
                || ship.IsRemoved
                || ship.IsSinking
                || ship.IsRetreating)
            {
                return false;
            }

#if BL_1_3_15
            return true;
#else
            return !ship.IsSunk
                   && !ship.BeingAbandoned
                   && !ship.IsShipNavmeshDisabled;
#endif
        }
    }
}
