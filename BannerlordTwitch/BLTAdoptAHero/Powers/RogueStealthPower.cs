using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using BannerlordTwitch;
using BannerlordTwitch.Localization;
using BannerlordTwitch.UI;
using BLTAdoptAHero.Annotations;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;
using YamlDotNet.Serialization;

namespace BLTAdoptAHero.Powers
{
    [LocDisplayName("{=RogueStealthPower_Name}Rogue"),
     LocDescription("{=RogueStealthPower_Desc}Hides the hero from enemy targeting while approaching isolated enemies"),
     UsedImplicitly]
    public class RogueStealthPower : GameRoleDefBase
    {
        private const float TargetProtectionIntervalSeconds = 0.15f;
        private const float DisengagementCheckIntervalSeconds = 0.25f;
        private const float DisengagementRepathIntervalSeconds = 0.75f;
        private const int MainActionChannel = 1;

        [YamlIgnore]
        protected override LocString RoleName => "{=RogueStealthPower_Name}Rogue";

        [Document, LocDisplayName("{=RogueStealthPower_IsolationRadius_Name}Isolation Radius"),
         LocCategory("Power Config", "{=75UOuDM}Power Config"),
         LocDescription("{=RogueStealthPower_IsolationRadius_Desc}Minimum distance in metres from a target to its nearest active ally"),
         UIRange(1, 30, 1), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         PropertyOrder(1), UsedImplicitly]
        public float IsolationRadius { get; set; } = 8f;

        [Document, LocDisplayName("{=RogueStealthPower_MaxTargetDistance_Name}Maximum Target Distance"),
         LocCategory("Power Config", "{=75UOuDM}Power Config"),
         LocDescription("{=RogueStealthPower_MaxTargetDistance_Desc}Maximum straight-line distance in metres for priority targets"),
         UIRange(5, 100, 5), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         PropertyOrder(2), UsedImplicitly]
        public float MaximumTargetDistance { get; set; } = 45f;

        [Document, LocDisplayName("{=RogueStealthPower_ReevaluationInterval_Name}Target Reevaluation Interval"),
         LocCategory("Power Config", "{=75UOuDM}Power Config"),
         LocDescription("{=RogueStealthPower_ReevaluationInterval_Desc}Seconds between searches for an isolated target"),
         UIRange(0.25f, 5f, 0.25f), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         PropertyOrder(3), UsedImplicitly]
        public float TargetReevaluationIntervalSeconds { get; set; } = 1f;

        [Document, LocDisplayName("{=RogueStealthPower_SwitchAdvantage_Name}Target Switch Advantage"),
         LocCategory("Power Config", "{=75UOuDM}Power Config"),
         LocDescription("{=RogueStealthPower_SwitchAdvantage_Desc}Extra nearest-ally distance required before switching priority targets"),
         UIRange(0f, 15f, 0.5f), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         PropertyOrder(4), UsedImplicitly]
        public float TargetSwitchAdvantage { get; set; } = 3f;

        [Document, LocDisplayName("{=RogueStealthPower_RecoveryDelay_Name}Stealth Recovery Delay"),
         LocCategory("Power Config", "{=75UOuDM}Power Config"),
         LocDescription("{=RogueStealthPower_RecoveryDelay_Desc}Seconds without receiving damage before stealth can return"),
         UIRange(0.5f, 10f, 0.5f), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         PropertyOrder(5), UsedImplicitly]
        public float StealthRecoveryDelaySeconds { get; set; } = 3f;

        [Document, LocDisplayName("{=RogueStealthPower_DisengageEnemyCount_Name}Disengagement Enemy Count"),
         LocCategory("Power Config", "{=75UOuDM}Power Config"),
         LocDescription("{=RogueStealthPower_DisengageEnemyCount_Desc}Nearby enemy count that makes a visible Rogue create distance"),
         UIRange(2, 10, 1),
         PropertyOrder(6), UsedImplicitly]
        public int DisengagementEnemyCount { get; set; } = 3;

        [Document, LocDisplayName("{=RogueStealthPower_DisengageRadius_Name}Disengagement Radius"),
         LocCategory("Power Config", "{=75UOuDM}Power Config"),
         LocDescription("{=RogueStealthPower_DisengageRadius_Desc}Radius in metres used to detect enemies surrounding a visible Rogue"),
         UIRange(2f, 12f, 0.5f), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         PropertyOrder(7), UsedImplicitly]
        public float DisengagementRadius { get; set; } = 5f;

        [Document, LocDisplayName("{=RogueStealthPower_DisengageDistance_Name}Disengagement Distance"),
         LocCategory("Power Config", "{=75UOuDM}Power Config"),
         LocDescription("{=RogueStealthPower_DisengageDistance_Desc}Distance in metres the Rogue tries to move away from an encirclement"),
         UIRange(3f, 20f, 1f), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         PropertyOrder(8), UsedImplicitly]
        public float DisengagementDistance { get; set; } = 10f;

        protected override bool HasGameplayEffects => true;

        protected override void OnHeroJoinedBattle(Hero hero, PowerHandler.Handlers handlers)
        {
            handlers.OnAgentBuild += agent =>
            {
                if (!agent.IsMount && agent.Character == hero.CharacterObject)
                {
                    var state = new RogueState(this, agent);
                    state.Attach(handlers);
                }
            };
        }

        [YamlIgnore, Browsable(false)]
        public override LocString Description =>
            "{=RogueStealthPower_PublicDesc}While hidden, the Rogue cannot be selected as a target and stalks isolated enemies, preferring fighters on foot. Dealing or receiving damage reveals the Rogue; when surrounded, the Rogue tries to create distance, and returns to stealth after avoiding damage for the configured recovery time.";

        private enum RogueAbilityState
        {
            Stealth,
            ApproachingTarget,
            ActiveAttack,
            ReturningToStealth,
        }

        private sealed class RogueState
        {
            private readonly RogueStealthPower power;
            private readonly Agent rogue;
            private RogueAbilityState state;
            private Agent target;
            private bool usingPriorityTarget;
            private float nextTargetEvaluationTime;
            private float nextTargetProtectionTime;
            private float nextDisengagementCheckTime;
            private float nextDisengagementRepathTime;
            private float lastDamageOrRevealTime;
            private bool isDisengaging;
            private WorldPosition disengagementDestination;
            private bool disposed;

            public RogueState(RogueStealthPower power, Agent rogue)
            {
                this.power = power;
                this.rogue = rogue;
                state = RogueAbilityState.Stealth;
            }

            public void Attach(PowerHandler.Handlers handlers)
            {
                if (!IsActive(rogue))
                    return;

                if (!RogueStealthRegistry.TryRegister(rogue))
                    return;

                RogueStealthRegistry.Hide(rogue);
                handlers.OnMissionTick += Tick;
                handlers.OnDoDamage += OnDoDamage;
                handlers.OnTakeDamage += OnTakeDamage;
                handlers.OnGotKilled += (_, _, _, _) => Dispose();
                handlers.OnMissionOver += Dispose;
            }

            private void Tick(float dt)
            {
                if (disposed || !IsActive(rogue) || Mission.Current == null)
                {
                    Dispose();
                    return;
                }

                float now = Mission.Current.CurrentTime;
                UpdateDisengagement(now);

                if (now >= nextTargetProtectionTime)
                {
                    nextTargetProtectionTime = now + TargetProtectionIntervalSeconds;
                    RogueStealthRegistry.ClearEnemyTargets(rogue);
                }

                if (target != null && !IsValidEnemy(target))
                {
                    target = null;
                    usingPriorityTarget = false;
                    rogue.InvalidateTargetAgent();
                    rogue.SetAutomaticTargetSelection(true);
                    BeginReturnToStealth();
                }

                bool attacking = IsAttackAction(rogue);
                bool hidden = RogueStealthRegistry.IsHidden(rogue);

                if (attacking && !hidden)
                {
                    if (state != RogueAbilityState.ActiveAttack)
                        state = RogueAbilityState.ActiveAttack;
                }
                else if (state == RogueAbilityState.ActiveAttack)
                {
                    state = RogueAbilityState.ReturningToStealth;
                }

                if (state == RogueAbilityState.ActiveAttack)
                    return;

                if (state == RogueAbilityState.ReturningToStealth)
                {
                    if (now - lastDamageOrRevealTime < power.StealthRecoveryDelaySeconds)
                        return;

                    EnterStealth();
                }

                if (now >= nextTargetEvaluationTime)
                {
                    nextTargetEvaluationTime = now + Math.Max(0.25f, power.TargetReevaluationIntervalSeconds);
                    if (RogueStealthRegistry.IsHidden(rogue))
                        EvaluateTarget();
                }

                if (target == null)
                {
                    var automaticTarget = rogue.GetTargetAgent();
                    if (IsValidEnemy(automaticTarget))
                    {
                        target = automaticTarget;
                        state = RogueAbilityState.ApproachingTarget;
                    }
                }
            }

            private void OnDoDamage(Agent attacker, Agent victim,
                BLTHeroPowersMissionBehavior.RegisterBlowParams blowParams)
            {
                if (disposed || attacker != rogue || Mission.Current == null ||
                    blowParams.blow.InflictedDamage <= 0)
                    return;

                bool wasHidden = RogueStealthRegistry.IsHidden(rogue);
                if (wasHidden)
                {
                    lastDamageOrRevealTime = Mission.Current.CurrentTime;
                    RogueStealthRegistry.Reveal(rogue);
                    UpdateDisengagement(Mission.Current.CurrentTime, true);
                }

                state = RogueAbilityState.ActiveAttack;
            }

            private void OnTakeDamage(Agent victim, Agent attacker,
                BLTHeroPowersMissionBehavior.RegisterBlowParams blowParams)
            {
                if (disposed || victim != rogue || Mission.Current == null ||
                    blowParams.blow.InflictedDamage <= 0)
                {
                    return;
                }

                bool wasHidden = RogueStealthRegistry.IsHidden(rogue);
                lastDamageOrRevealTime = Mission.Current.CurrentTime;
                if (wasHidden)
                {
                    state = RogueAbilityState.ReturningToStealth;
                    RogueStealthRegistry.Reveal(rogue);
                }

                UpdateDisengagement(Mission.Current.CurrentTime, true);
            }

            private void EvaluateTarget()
            {
                var allEnemies = Mission.Current.Agents
                    .Where(IsValidEnemy)
                    .Where(a => !a.IsMount)
                    .ToList();
                var footEnemies = allEnemies
                    .Where(enemy => enemy.MountAgent == null)
                    .ToList();
                bool prioritizingFootEnemies = footEnemies.Count > 0;
                var priorityPool = (prioritizingFootEnemies
                        ? footEnemies
                        : allEnemies.Where(enemy => enemy.MountAgent != null))
                    .Where(HasPathTo)
                    .ToList();
                var candidates = priorityPool
                    .Where(a => rogue.Position.DistanceSquared(a.Position) <=
                                power.MaximumTargetDistance * power.MaximumTargetDistance)
                    .ToList();

                var isolated = candidates
                    .Select(candidate => new
                    {
                        Agent = candidate,
                        Isolation = NearestAllyDistance(candidate, allEnemies),
                    })
                    .Where(candidate => candidate.Isolation >= power.IsolationRadius)
                    .OrderByDescending(candidate => candidate.Isolation)
                    .ThenBy(candidate => rogue.Position.DistanceSquared(candidate.Agent.Position))
                    .FirstOrDefault();

                float currentIsolation = candidates.Contains(target)
                    ? NearestAllyDistance(target, allEnemies)
                    : 0f;

                if (isolated == null)
                {
                    float retentionThreshold = Math.Max(0f,
                        power.IsolationRadius - power.TargetSwitchAdvantage);
                    if (usingPriorityTarget && currentIsolation >= retentionThreshold)
                        return;

                    if (usingPriorityTarget)
                    {
                        rogue.InvalidateTargetAgent();
                        target = null;
                        usingPriorityTarget = false;
                    }

                    rogue.SetAutomaticTargetSelection(true);
                    if (target == null && state == RogueAbilityState.ApproachingTarget)
                        state = RogueAbilityState.Stealth;
                    return;
                }

                if (usingPriorityTarget && currentIsolation >= power.IsolationRadius &&
                    isolated.Agent != target && isolated.Isolation < currentIsolation + power.TargetSwitchAdvantage)
                    return;

                target = isolated.Agent;
                usingPriorityTarget = true;
                rogue.SetAutomaticTargetSelection(false);
                rogue.SetTargetAgent(target);
                state = RogueAbilityState.ApproachingTarget;
            }

            private float NearestAllyDistance(Agent candidate, List<Agent> enemies)
            {
                float nearestSquared = float.MaxValue;
                foreach (var other in enemies)
                {
                    if (other == candidate || candidate.Team == null || other.Team == null ||
                        !candidate.Team.IsFriendOf(other.Team) ||
                        !BannerlordApi.CanAgentsNavigateToEachOther(candidate, other))
                        continue;

                    nearestSquared = Math.Min(nearestSquared,
                        candidate.Position.DistanceSquared(other.Position));
                }

                return nearestSquared == float.MaxValue
                    ? power.MaximumTargetDistance
                    : (float)Math.Sqrt(nearestSquared);
            }

            private bool HasPathTo(Agent candidate)
            {
                var position = candidate.Position;
                float pathDistance = rogue.GetPathDistanceToPoint(ref position);
                return pathDistance >= 0f && !float.IsNaN(pathDistance) && !float.IsInfinity(pathDistance);
            }

            private bool IsValidEnemy(Agent candidate) =>
                IsActive(candidate) && candidate.Team != null && rogue.Team != null && rogue.Team.IsEnemyOf(candidate.Team);

            private void UpdateDisengagement(float now, bool force = false)
            {
                if (RogueStealthRegistry.IsHidden(rogue))
                {
                    EndDisengagement();
                    return;
                }

                if (!force && now < nextDisengagementCheckTime)
                    return;

                nextDisengagementCheckTime = now + DisengagementCheckIntervalSeconds;
                var nearbyEnemies = Mission.Current.Agents
                    .Where(IsValidEnemy)
                    .Where(enemy => !enemy.IsMount)
                    .Where(enemy => BannerlordApi.CanAgentsNavigateToEachOther(rogue, enemy))
                    .Where(enemy => rogue.Position.DistanceSquared(enemy.Position) <=
                                    power.DisengagementRadius * power.DisengagementRadius)
                    .ToList();
                if (!isDisengaging && nearbyEnemies.Count < power.DisengagementEnemyCount)
                    return;

                if (!isDisengaging)
                {
                    isDisengaging = true;
                    nextDisengagementRepathTime = 0f;
                }

                if (now < nextDisengagementRepathTime || nearbyEnemies.Count == 0)
                    return;

                nextDisengagementRepathTime = now + DisengagementRepathIntervalSeconds;
                Vec2 enemyCenter = new Vec2(
                    nearbyEnemies.Average(enemy => enemy.Position.x),
                    nearbyEnemies.Average(enemy => enemy.Position.y));
                Vec2 awayDirection = rogue.Position.AsVec2 - enemyCenter;
                if (awayDirection.LengthSquared < 0.01f)
                    awayDirection = -rogue.LookDirection.AsVec2;
                awayDirection.Normalize();

                if (!TrySetDisengagementDestination(awayDirection, power.DisengagementDistance) &&
                    !TrySetDisengagementDestination(awayDirection, power.DisengagementDistance * 0.5f))
                    return;

                var destination = disengagementDestination;
                rogue.SetScriptedPosition(ref destination, false, Agent.AIScriptedFrameFlags.NeverSlowDown);
            }

            private bool TrySetDisengagementDestination(Vec2 direction, float distance)
            {
                var destination = rogue.GetWorldPosition();
                destination.SetVec2(rogue.Position.AsVec2 + direction * distance);
                if (!destination.IsValid)
                    return false;

                Vec3 destinationPoint = destination.GetNavMeshVec3();
                float pathDistance = rogue.GetPathDistanceToPoint(ref destinationPoint);
                if (pathDistance < 0f || float.IsNaN(pathDistance) || float.IsInfinity(pathDistance))
                    return false;

                disengagementDestination = destination;
                return true;
            }

            private void EndDisengagement()
            {
                if (!isDisengaging)
                    return;

                isDisengaging = false;
                disengagementDestination = default;
                if (IsActive(rogue))
                    rogue.DisableScriptedMovement();
            }

            private static bool IsAttackAction(Agent agent)
            {
                var actionType = agent.GetCurrentActionType(MainActionChannel);
                return actionType == Agent.ActionCodeType.ReadyRanged ||
                       actionType == Agent.ActionCodeType.ReleaseRanged ||
                       actionType == Agent.ActionCodeType.ReleaseThrowing ||
                       actionType == Agent.ActionCodeType.ReadyMelee ||
                       actionType == Agent.ActionCodeType.ReleaseMelee ||
                       actionType == Agent.ActionCodeType.Kick ||
                       actionType == Agent.ActionCodeType.KickContinue ||
                       actionType == Agent.ActionCodeType.KickHit ||
                       actionType == Agent.ActionCodeType.WeaponBash;
            }

            private void BeginReturnToStealth()
            {
                if (state == RogueAbilityState.ActiveAttack)
                {
                    state = RogueAbilityState.ReturningToStealth;
                }
                else
                {
                    EnterStealth();
                }
            }

            private void EnterStealth()
            {
                EndDisengagement();
                state = target == null ? RogueAbilityState.Stealth : RogueAbilityState.ApproachingTarget;
                RogueStealthRegistry.Hide(rogue);
            }

            private void Dispose()
            {
                if (disposed)
                    return;

                disposed = true;
                EndDisengagement();
                RogueStealthRegistry.Reveal(rogue);
                RogueStealthRegistry.Unregister(rogue);
                if (rogue != null && rogue.State == AgentState.Active)
                    rogue.SetAutomaticTargetSelection(true);
            }

            private static bool IsActive(Agent agent) =>
                agent != null && agent.State == AgentState.Active && !agent.IsFadingOut();

        }

        internal static class RogueStealthRegistry
        {
            private static readonly HashSet<Agent> HiddenAgents = new();
            private static readonly HashSet<Agent> RegisteredAgents = new();
            private static readonly Dictionary<Agent, RedirectedEnemy> RedirectedEnemies = new();

            private sealed class RedirectedEnemy
            {
                public Agent HiddenAgent;
                public Agent ReplacementTarget;
                public float NextReplacementSearchTime;
            }

            public static bool TryRegister(Agent agent) => agent != null && RegisteredAgents.Add(agent);

            public static void Unregister(Agent agent)
            {
                if (agent != null)
                {
                    RegisteredAgents.Remove(agent);
                    if (RegisteredAgents.Count == 0)
                    {
                        RestoreAllRedirectedEnemies();
                    }
                }
            }

            public static void Hide(Agent agent)
            {
                if (agent != null)
                    HiddenAgents.Add(agent);
            }

            public static void Reveal(Agent agent)
            {
                if (agent != null)
                {
                    HiddenAgents.Remove(agent);
                    RestoreRedirectedEnemies(agent);
                }
            }

            public static bool IsHidden(Agent agent) => agent != null && HiddenAgents.Contains(agent);

            public static bool IsHiddenFrom(Agent observer, Agent target) =>
                observer != null && target != null && HiddenAgents.Contains(target) &&
                observer.Team != null && target.Team != null && observer.Team.IsEnemyOf(target.Team);

            public static void ClearEnemyTargets(Agent hiddenAgent)
            {
                if (!HiddenAgents.Contains(hiddenAgent) || Mission.Current == null)
                    return;

                foreach (var pair in RedirectedEnemies.ToList())
                {
                    if (pair.Value.HiddenAgent != hiddenAgent)
                        continue;

                    if (!IsActive(pair.Key))
                    {
                        RedirectedEnemies.Remove(pair.Key);
                        continue;
                    }

                    bool needsReplacement = pair.Value.ReplacementTarget == null
                        ? Mission.Current.CurrentTime >= pair.Value.NextReplacementSearchTime
                        : !IsValidVisibleTarget(pair.Key, pair.Value.ReplacementTarget) ||
                          pair.Key.GetTargetAgent() != pair.Value.ReplacementTarget;
                    if (needsReplacement)
                        RedirectEnemy(pair.Key, hiddenAgent);
                }

                foreach (var enemy in Mission.Current.Agents)
                {
                    if (IsActive(enemy) && enemy.IsAIControlled &&
                        IsHiddenFrom(enemy, hiddenAgent) && enemy.GetTargetAgent() == hiddenAgent)
                    {
                        RedirectEnemy(enemy, hiddenAgent);
                    }
                }
            }

            public static void RedirectBlockedTargetAttempt(Agent observer, Agent hiddenAgent)
            {
                RedirectEnemy(observer, hiddenAgent);
            }

            private static bool RedirectEnemy(Agent enemy, Agent hiddenAgent)
            {
                if (!IsActive(enemy) || !enemy.IsAIControlled)
                    return false;

                RedirectedEnemies.TryGetValue(enemy, out var previousState);
                Agent replacement = FindReplacementTarget(enemy);
                enemy.SetAutomaticTargetSelection(false);
                if (replacement == null)
                    enemy.InvalidateTargetAgent();
                else
                    enemy.SetTargetAgent(replacement);

                RedirectedEnemies[enemy] = new RedirectedEnemy
                {
                    HiddenAgent = hiddenAgent,
                    ReplacementTarget = replacement,
                    NextReplacementSearchTime = (Mission.Current?.CurrentTime ?? 0f) + 1f,
                };
                return previousState == null || previousState.ReplacementTarget != replacement;
            }

            private static Agent FindReplacementTarget(Agent enemy) => Mission.Current?.Agents
                .Where(candidate => IsValidVisibleTarget(enemy, candidate))
                .OrderByDescending(candidate => BannerlordApi.CanAgentsNavigateToEachOther(enemy, candidate))
                .ThenBy(candidate => enemy.Position.DistanceSquared(candidate.Position))
                .FirstOrDefault();

            private static bool IsValidVisibleTarget(Agent observer, Agent candidate) =>
                IsActive(candidate) && !candidate.IsMount && !HiddenAgents.Contains(candidate) &&
                observer != null && observer.Team != null && candidate.Team != null &&
                observer.Team.IsEnemyOf(candidate.Team);

            private static bool IsActive(Agent agent) =>
                agent != null && agent.State == AgentState.Active && !agent.IsFadingOut();

            private static void RestoreRedirectedEnemies(Agent revealedAgent)
            {
                foreach (var pair in RedirectedEnemies.Where(pair => pair.Value.HiddenAgent == revealedAgent).ToList())
                    RestoreEnemy(pair.Key);
            }

            private static void RestoreAllRedirectedEnemies()
            {
                foreach (var enemy in RedirectedEnemies.Keys.ToList())
                    RestoreEnemy(enemy);
            }

            private static void RestoreEnemy(Agent enemy)
            {
                RedirectedEnemies.Remove(enemy);
                if (!IsActive(enemy))
                    return;

                enemy.SetAutomaticTargetSelection(true);
                enemy.ForceAiBehaviorSelection();
            }

        }
    }

    [HarmonyPatch(typeof(Agent), nameof(Agent.SetTargetAgent))]
    internal static class RogueStealthTargetPatch
    {
        [HarmonyPrefix]
        [UsedImplicitly]
        private static bool PreventHiddenRogueTarget(Agent __instance, Agent agent)
        {
            if (!RogueStealthPower.RogueStealthRegistry.IsHiddenFrom(__instance, agent))
                return true;

            RogueStealthPower.RogueStealthRegistry.RedirectBlockedTargetAttempt(__instance, agent);
            return false;
        }
    }
}
