using System;
using BannerlordTwitch;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Rewards;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;


namespace BLTAdoptAHero.Actions
{
    [LocDisplayName("{=BLTLeaveBattle_DisplayName}Leave Battle"),
     LocDescription("{=BLTLeaveBattle_Description}Removes your adopted hero and retinue from the current mission"),
     UsedImplicitly]
    public class LeaveBattle : ActionHandlerBase
    {
        protected override Type ConfigType => null;

        protected override void ExecuteInternal(
            ReplyContext context,
            object config,
            Action<string> onSuccess,
            Action<string> onFailure)
        {
            var hero = BLTAdoptAHeroCampaignBehavior.Current?.GetAdoptedHero(context.UserName);
            if (hero == null)
            {
                onFailure(AdoptAHero.NoHeroMessage);
                return;
            }

            if (Mission.Current == null)
            {
                onFailure("You are not in a mission.");
                return;
            }

            var summonBehavior = BLTSummonBehavior.Current;
            if (summonBehavior == null)
            {
                onFailure("Summon behavior is not available.");
                return;
            }

            var state = summonBehavior.GetHeroSummonState(hero);
            if (state == null || state.CurrentAgent == null)
            {
                onFailure("You are not currently in battle.");
                return;
            }
            
            RemoveAgent(state.CurrentAgent);

            foreach (var r in state.Retinue)
            {
                RemoveAgent(r.Agent);
            }
            
            foreach (var r in state.Retinue2)
            {
                RemoveAgent(r.Agent);
            }
            
            summonBehavior.RemoveRetinueFromParty(hero);
            
            summonBehavior.ResetHeroSummonState(hero);

            onSuccess("You have left the battle.");
        }
        
        private static void RemoveAgent(Agent agent)
        {
            if (agent == null || !agent.IsActive())
                return;

            var blow = new Blow(agent.Index)
            {
                DamageType = DamageTypes.Blunt,
                BaseMagnitude = 0,
                InflictedDamage = (int)agent.HealthLimit,
                SwingDirection = Vec3.Zero,
                GlobalPosition = agent.Position,
                BlowFlag = BlowFlags.None
            };

            agent.FadeOut(true, true);
            agent.Die(blow);
        }
    }
}