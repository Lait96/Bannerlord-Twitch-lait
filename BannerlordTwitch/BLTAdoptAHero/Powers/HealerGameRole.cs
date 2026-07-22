using System;
using System.ComponentModel;
using System.Linq;
using BannerlordTwitch;
using BannerlordTwitch.Localization;
using BannerlordTwitch.UI;
using BLTAdoptAHero.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;
using YamlDotNet.Serialization;

namespace BLTAdoptAHero.Powers
{
    [LocDisplayName("{=HealerGameRole_Name}Healer"),
     LocDescription("{=HealerGameRole_Desc}Heals nearby allies and stays close enough to support the group"),
     UsedImplicitly]
    public sealed class HealerGameRole : GameRoleDefBase
    {
        private const float MinimumLeashHysteresis = 1f;
        private const float LeashRepathIntervalSeconds = 1f;
        private const float LeashDestinationChangeThreshold = 2f;
        private const float LeashAnchorSwitchDistanceRatio = 0.75f;

        [LocDisplayName("{=HealerGameRole_AuraRadius_Name}Aura Radius"),
         LocCategory("Healing Aura", "{=HealerGameRole_Aura_Category}Healing Aura"),
         LocDescription("{=HealerGameRole_AuraRadius_Desc}Radius in metres in which active allies can be healed"),
         UIRange(1f, 30f, 0.5f), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         PropertyOrder(1), UsedImplicitly]
        public float AuraRadius { get; set; } = 10f;

        [LocDisplayName("{=HealerGameRole_HealingPerAlly_Name}Healing Strength"),
         LocCategory("Healing Aura", "{=HealerGameRole_Aura_Category}Healing Aura"),
         LocDescription("{=HealerGameRole_HealingPerAlly_Desc}Maximum health restored to one ally by each healing pulse"),
         UIRange(0f, 100f, 1f), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         PropertyOrder(2), UsedImplicitly]
        public float HealingPerAlly { get; set; } = 8f;

        [LocDisplayName("{=HealerGameRole_HealingBudget_Name}Health Per Pulse"),
         LocCategory("Healing Aura", "{=HealerGameRole_Aura_Category}Healing Aura"),
         LocDescription("{=HealerGameRole_HealingBudget_Desc}Maximum total health restored across all allies by one pulse"),
         UIRange(0f, 1000f, 5f), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         PropertyOrder(3), UsedImplicitly]
        public float MaximumHealingPerPulse { get; set; } = 75f;

        [LocDisplayName("{=HealerGameRole_HealingPeriod_Name}Healing Period"),
         LocCategory("Healing Aura", "{=HealerGameRole_Aura_Category}Healing Aura"),
         LocDescription("{=HealerGameRole_HealingPeriod_Desc}Seconds between healing pulses"),
         UIRange(0.25f, 10f, 0.25f), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         PropertyOrder(4), UsedImplicitly]
        public float HealingPeriodSeconds { get; set; } = 2f;

        [LocDisplayName("{=HealerGameRole_LeashDistance_Name}Leash Distance"),
         LocCategory("Leash", "{=HealerGameRole_Leash_Category}Leash"),
         LocDescription("{=HealerGameRole_LeashDistance_Desc}Distance from the nearest ally at which the Healer temporarily moves back toward the group"),
         UIRange(5f, 100f, 1f), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         PropertyOrder(5), UsedImplicitly]
        public float LeashDistance { get; set; } = 20f;

        [LocDisplayName("{=HealerGameRole_LeashReleaseDistance_Name}Leash Release Distance"),
         LocCategory("Leash", "{=HealerGameRole_Leash_Category}Leash"),
         LocDescription("{=HealerGameRole_LeashReleaseDistance_Desc}Distance from an ally at which normal AI orders resume"),
         UIRange(2f, 50f, 1f), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         PropertyOrder(6), UsedImplicitly]
        public float LeashReleaseDistance { get; set; } = 10f;

        [LocDisplayName("{=HealerGameRole_LeashCheckInterval_Name}Leash Check Interval"),
         LocCategory("Leash", "{=HealerGameRole_Leash_Category}Leash"),
         LocDescription("{=HealerGameRole_LeashCheckInterval_Desc}Seconds between checks of the Healer's distance from allies"),
         UIRange(0.25f, 5f, 0.25f), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         PropertyOrder(7), UsedImplicitly]
        public float LeashCheckIntervalSeconds { get; set; } = 0.5f;

        [LocDisplayName("{=HealerGameRole_GoldPerHealth_Name}Gold Per Health Restored"),
         LocCategory("Rewards", "{=HealerGameRole_Rewards_Category}Rewards"),
         LocDescription("{=HealerGameRole_GoldPerHealth_Desc}Gold earned per point of health actually restored; overhealing earns nothing"),
         UIRange(0f, 100f, 0.5f), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         PropertyOrder(8), UsedImplicitly]
        public float GoldPerHealthPoint { get; set; } = 1f;

        [YamlIgnore, Browsable(false)]
        protected override LocString RoleName => "{=HealerGameRole_Name}Healer";

        [YamlIgnore, Browsable(false)]
        public override LocString Description =>
            "{=HealerGameRole_Desc}Periodically heals wounded allies in an aura and earns gold only for health actually restored. If separated from the group, the Healer temporarily moves toward the nearest ally and releases the override after rejoining them.";

        protected override bool HasGameplayEffects => true;

        protected override void OnHeroJoinedBattle(Hero hero, PowerHandler.Handlers handlers)
        {
            handlers.OnAgentBuild += agent =>
            {
                if (!agent.IsMount && agent.Character == hero.CharacterObject)
                    new HealerState(this, hero, agent).Attach(handlers);
            };
        }

        private sealed class HealerState
        {
            private readonly HealerGameRole power;
            private readonly Hero hero;
            private readonly Agent healer;
            private Agent leashAnchor;
            private bool leashActive;
            private bool disposed;
            private float nextHealingTime;
            private float nextLeashCheckTime;
            private float nextLeashRepathTime;
            private float pendingGold;
            private WorldPosition leashDestination;

            public HealerState(HealerGameRole power, Hero hero, Agent healer)
            {
                this.power = power;
                this.hero = hero;
                this.healer = healer;
            }

            public void Attach(PowerHandler.Handlers handlers)
            {
                if (!IsActive(healer) || Mission.Current == null)
                    return;

                float now = Mission.Current.CurrentTime;
                nextHealingTime = now + Math.Max(0.25f, power.HealingPeriodSeconds);
                nextLeashCheckTime = now;
                handlers.OnMissionTick += Tick;
                handlers.OnGotKilled += (_, _, _, _) => Dispose();
                handlers.OnAgentControllerChanged += _ => UpdateController();
                handlers.OnMissionOver += Dispose;
            }

            private void Tick(float dt)
            {
                if (disposed || !IsActive(healer) || Mission.Current == null)
                {
                    Dispose();
                    return;
                }

                float now = Mission.Current.CurrentTime;
                if (now >= nextHealingTime)
                {
                    nextHealingTime = now + Math.Max(0.25f, power.HealingPeriodSeconds);
                    ApplyHealingPulse();
                }

                if (now >= nextLeashCheckTime)
                {
                    nextLeashCheckTime = now + Math.Max(0.25f, power.LeashCheckIntervalSeconds);
                    UpdateLeash(now);
                }
            }

            private void ApplyHealingPulse()
            {
                float healingPerAlly = Math.Max(0f, power.HealingPerAlly);
                float remainingHealing = Math.Max(0f, power.MaximumHealingPerPulse);
                if (healingPerAlly <= 0f || remainingHealing <= 0f)
                    return;

                float radiusSquared = Math.Max(0f, power.AuraRadius) * Math.Max(0f, power.AuraRadius);
                var woundedAllies = Mission.Current.Agents
                    .Where(IsValidHealingTarget)
                    .Where(agent => healer.Position.DistanceSquared(agent.Position) <= radiusSquared)
                    .Select(agent => new
                    {
                        Agent = agent,
                        MissingHealth = Math.Max(0f, agent.HealthLimit - agent.Health),
                    })
                    .Where(candidate => candidate.MissingHealth > 0f)
                    .OrderByDescending(candidate => candidate.MissingHealth)
                    .ToList();

                float restoredHealth = 0f;
                foreach (var candidate in woundedAllies)
                {
                    if (remainingHealing <= 0f)
                        break;

                    float plannedRestore = Math.Min(candidate.MissingHealth,
                        Math.Min(healingPerAlly, remainingHealing));
                    if (plannedRestore <= 0f)
                        continue;

                    float healthBefore = candidate.Agent.Health;
                    candidate.Agent.Health = Math.Min(candidate.Agent.HealthLimit,
                        candidate.Agent.Health + plannedRestore);
                    float actualRestore = Math.Max(0f, candidate.Agent.Health - healthBefore);
                    restoredHealth += actualRestore;
                    remainingHealing -= actualRestore;
                }

                AwardHealingGold(restoredHealth);
            }

            private void AwardHealingGold(float restoredHealth)
            {
                if (restoredHealth <= 0f || power.GoldPerHealthPoint <= 0f)
                    return;

                pendingGold += restoredHealth * power.GoldPerHealthPoint;
                int gold = (int)Math.Floor(pendingGold);
                if (gold <= 0)
                    return;

                var campaignBehavior = BLTAdoptAHeroCampaignBehavior.Current;
                if (campaignBehavior == null)
                    return;

                pendingGold -= gold;
                campaignBehavior.ChangeHeroGold(hero, gold);
                BLTAdoptAHeroCommonMissionBehavior.Current?.RecordGoldGain(hero, gold);
            }

            private void UpdateLeash(float now)
            {
                if (healer.Controller != AgentControllerType.AI)
                {
                    EndLeash();
                    return;
                }

                Agent nearestAlly = null;
                float nearestDistanceSquared = float.MaxValue;
                foreach (var ally in Mission.Current.Agents)
                {
                    if (!IsValidHealingTarget(ally))
                        continue;

                    float distanceSquared = healer.Position.DistanceSquared(ally.Position);
                    if (distanceSquared < nearestDistanceSquared)
                    {
                        nearestAlly = ally;
                        nearestDistanceSquared = distanceSquared;
                    }
                }

                if (nearestAlly == null)
                {
                    EndLeash();
                    return;
                }
                float engageDistance = Math.Max(MinimumLeashHysteresis, power.LeashDistance);
                float releaseDistance = Math.Min(Math.Max(0f, power.LeashReleaseDistance),
                    Math.Max(0f, engageDistance - MinimumLeashHysteresis));
                if (!leashActive)
                {
                    if (nearestDistanceSquared <= engageDistance * engageDistance)
                        return;

                    leashActive = true;
                    leashAnchor = nearestAlly;
                    nextLeashRepathTime = 0f;
                }
                else if (nearestDistanceSquared <= releaseDistance * releaseDistance)
                {
                    EndLeash();
                    return;
                }

                float anchorDistanceSquared = IsValidHealingTarget(leashAnchor)
                    ? healer.Position.DistanceSquared(leashAnchor.Position)
                    : float.MaxValue;
                if (nearestAlly != leashAnchor &&
                    nearestDistanceSquared < anchorDistanceSquared *
                    LeashAnchorSwitchDistanceRatio * LeashAnchorSwitchDistanceRatio)
                {
                    leashAnchor = nearestAlly;
                    leashDestination = default;
                    nextLeashRepathTime = 0f;
                }

                if (now < nextLeashRepathTime)
                    return;

                nextLeashRepathTime = now + LeashRepathIntervalSeconds;
                TryUpdateLeashDestination(releaseDistance);
            }

            private void TryUpdateLeashDestination(float releaseDistance)
            {
                if (!IsValidHealingTarget(leashAnchor))
                    return;

                Vec2 towardHealer = healer.Position.AsVec2 - leashAnchor.Position.AsVec2;
                if (towardHealer.LengthSquared < 0.01f)
                {
                    towardHealer = -leashAnchor.LookDirection.AsVec2;
                }
                towardHealer.Normalize();

                var destination = leashAnchor.GetWorldPosition();
                destination.SetVec2(leashAnchor.Position.AsVec2 + towardHealer * releaseDistance * 0.5f);
                if (!destination.IsValid)
                    return;

                Vec3 destinationPoint = destination.GetNavMeshVec3();
                float pathDistance = healer.GetPathDistanceToPoint(ref destinationPoint);
                if (pathDistance < 0f || float.IsNaN(pathDistance) || float.IsInfinity(pathDistance))
                    return;

                if (leashDestination.IsValid &&
                    leashDestination.GetNavMeshVec3().DistanceSquared(destinationPoint) <
                    LeashDestinationChangeThreshold * LeashDestinationChangeThreshold)
                    return;

                leashDestination = destination;
                healer.SetScriptedPosition(ref destination, false, Agent.AIScriptedFrameFlags.None);
            }

            private void UpdateController()
            {
                if (healer.Controller != AgentControllerType.AI)
                    EndLeash();
            }

            private bool IsValidHealingTarget(Agent agent) =>
                agent != healer && IsActive(agent) && !agent.IsMount &&
                healer.Team != null && agent.Team != null && healer.Team.IsFriendOf(agent.Team);

            private void EndLeash()
            {
                if (!leashActive)
                    return;

                bool healerActive = IsActive(healer);
                leashActive = false;
                leashAnchor = null;
                leashDestination = default;
                if (healerActive)
                    healer.DisableScriptedMovement();
            }

            private void Dispose()
            {
                if (disposed)
                    return;

                disposed = true;
                EndLeash();
            }

            private static bool IsActive(Agent agent) =>
                agent != null && agent.State == AgentState.Active && !agent.IsFadingOut();
        }
    }
}
