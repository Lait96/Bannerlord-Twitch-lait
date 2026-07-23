using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.UI;
using BLTAdoptAHero.Actions;
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
    [LocDisplayName("{=TankGameRole_Name}Tank"),
     LocDescription("{=TankGameRole_Desc}Attracts nearby enemies and earns participation rewards while leading them toward allies"),
     UsedImplicitly]
    public sealed class TankGameRole : GameRoleDefBase
    {
        private const int MainActionChannel = 1;
        private const int MinimumAggroedEnemiesForMovement = 2;
        private const float MovementAllySearchRadius = 30f;
        private const float MovementDesiredAllyDistance = 8f;
        private const float MovementStepDistance = 4f;
        private const float MovementEnemyStopDistance = 4f;
        private const float MovementDestinationTolerance = 1.5f;
        private const float MovementRestartDelaySeconds = 2f;

        [LocDisplayName("{=TankGameRole_AggroRadius_Name}Aggro Radius"),
         LocCategory("Aggro", "{=TankGameRole_Aggro_Category}Aggro"),
         LocDescription("{=TankGameRole_AggroRadius_Desc}Radius in metres in which enemies can be influenced to prefer the Tank"),
         UIRange(1f, 60f, 1f), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         PropertyOrder(1), UsedImplicitly]
        public float AggroRadius { get; set; } = 20f;

        [LocDisplayName("{=TankGameRole_AggroStrength_Name}Aggro Priority Strength"),
         LocCategory("Aggro", "{=TankGameRole_Aggro_Category}Aggro"),
         LocDescription("{=TankGameRole_AggroStrength_Desc}How much farther than an enemy's current target the Tank may be while still attracting that enemy"),
         UIRange(0.25f, 3f, 0.05f), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         PropertyOrder(2), UsedImplicitly]
        public float AggroPriorityStrength { get; set; } = 1.25f;

        [LocDisplayName("{=TankGameRole_MaximumTargets_Name}Maximum Aggro Targets"),
         LocCategory("Aggro", "{=TankGameRole_Aggro_Category}Aggro"),
         LocDescription("{=TankGameRole_MaximumTargets_Desc}Maximum number of enemies simultaneously tracked and influenced by the role"),
         UIRange(0, 50, 1), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         PropertyOrder(3), UsedImplicitly]
        public int MaximumAggroTargets { get; set; } = 8;

        [LocDisplayName("{=TankGameRole_EvaluationInterval_Name}Aggro Evaluation Interval"),
         LocCategory("Aggro", "{=TankGameRole_Aggro_Category}Aggro"),
         LocDescription("{=TankGameRole_EvaluationInterval_Desc}Seconds between searches for enemies that the Tank can attract"),
         UIRange(0.25f, 5f, 0.25f), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         PropertyOrder(4), UsedImplicitly]
        public float AggroEvaluationIntervalSeconds { get; set; } = 1f;

        [LocDisplayName("{=TankGameRole_ReplacementThreshold_Name}Target Replacement Threshold"),
         LocCategory("Aggro", "{=TankGameRole_Aggro_Category}Aggro"),
         LocDescription("{=TankGameRole_ReplacementThreshold_Desc}How many metres closer a new enemy must be before replacing a tracked enemy"),
         UIRange(0f, 20f, 0.5f), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         PropertyOrder(5), UsedImplicitly]
        public float TargetReplacementThreshold { get; set; } = 3f;

        [LocDisplayName("{=TankGameRole_RewardShare_Name}Participation Reward Share"),
         LocCategory("Rewards", "{=TankGameRole_Rewards_Category}Rewards"),
         LocDescription("{=TankGameRole_RewardShare_Desc}Percentage of normal gold and experience received when an ally defeats an enemy aggroed to the Tank"),
         UIRange(0f, 100f, 1f), Editor(typeof(SliderFloatEditor), typeof(SliderFloatEditor)),
         PropertyOrder(6), UsedImplicitly]
        public float SharedRewardPercentage { get; set; } = 35f;

        [YamlIgnore, Browsable(false)]
        protected override LocString RoleName => "{=TankGameRole_Name}Tank";

        [YamlIgnore, Browsable(false)]
        public override LocString Description =>
            "{=TankGameRole_Desc}Periodically encourages nearby enemies to prefer the Tank without disabling their normal target selection. The Tank makes short moves toward nearby allies when safely able to lead aggroed enemies to the group, and receives a share of normal gold and experience when an ally defeats an enemy still focused on the Tank.";

        protected override bool HasGameplayEffects => true;

        protected override void OnHeroJoinedBattle(Hero hero, PowerHandler.Handlers handlers)
        {
            handlers.OnAgentBuild += agent =>
            {
                if (!agent.IsMount && agent.Character == hero.CharacterObject)
                    new TankState(this, hero, agent).Attach(handlers);
            };
        }

        private sealed class TankState
        {
            private sealed class AggroState
            {
                public bool WasTargetingTank;
            }

            private static readonly HashSet<Agent> RegisteredTanks = new();

            private readonly TankGameRole role;
            private readonly Hero hero;
            private readonly Agent tank;
            private readonly Dictionary<Agent, AggroState> trackedEnemies = new();
            private bool disposed;
            private bool movementActive;
            private float nextAggroEvaluationTime;
            private float nextMovementAllowedTime;
            private float pendingGold;
            private float pendingXP;
            private WorldPosition movementDestination;

            public TankState(TankGameRole role, Hero hero, Agent tank)
            {
                this.role = role;
                this.hero = hero;
                this.tank = tank;
            }

            public void Attach(PowerHandler.Handlers handlers)
            {
                if (!IsActive(tank) || Mission.Current == null || !RegisteredTanks.Add(tank))
                    return;

                nextAggroEvaluationTime = Mission.Current.CurrentTime;
                handlers.OnMissionTick += Tick;
                handlers.OnTakeDamage += (_, _, _) => StopMovement("tank received damage");
                handlers.OnAgentControllerChanged += _ => UpdateController();
                handlers.OnAgentRemoved += OnAgentRemoved;
                handlers.OnGotKilled += (_, _, _, _) => Dispose();
                handlers.OnMissionOver += Dispose;
            }

            private void Tick(float dt)
            {
                if (disposed || !IsActive(tank) || Mission.Current == null)
                {
                    Dispose();
                    return;
                }

                ObserveTrackedEnemies();
                UpdateActiveMovement();

                float now = Mission.Current.CurrentTime;
                if (now < nextAggroEvaluationTime)
                    return;

                nextAggroEvaluationTime = now + Math.Max(0.25f, role.AggroEvaluationIntervalSeconds);
                EvaluateAggro(now);
            }

            private void ObserveTrackedEnemies()
            {
                foreach (var pair in trackedEnemies.ToList())
                {
                    if (!IsValidEnemy(pair.Key))
                    {
                        trackedEnemies.Remove(pair.Key);
                        continue;
                    }

                    pair.Value.WasTargetingTank = pair.Key.GetTargetAgent() == tank;
                }
            }

            private void EvaluateAggro(float now)
            {
                int maximumTargets = Math.Max(0, role.MaximumAggroTargets);
                float radius = Math.Max(0f, role.AggroRadius);
                float radiusSquared = radius * radius;

                foreach (var enemy in trackedEnemies.Keys.ToList())
                {
                    bool valid = IsValidEnemy(enemy);
                    float distanceSquared = valid ? tank.Position.DistanceSquared(enemy.Position) : float.MaxValue;
                    if (!valid || distanceSquared > radiusSquared)
                        trackedEnemies.Remove(enemy);
                }

                TrimTrackedEnemies(maximumTargets);
                if (maximumTargets == 0 || radius <= 0f)
                {
                    StopMovement("aggro configuration disabled");
                    return;
                }

                var agents = Mission.Current.Agents;
                var untrackedEnemies = agents
                    .Where(enemy => !trackedEnemies.ContainsKey(enemy) && IsValidEnemy(enemy))
                    .Select(enemy => new
                    {
                        Enemy = enemy,
                        DistanceSquared = tank.Position.DistanceSquared(enemy.Position),
                    })
                    .ToList();
                var candidates = untrackedEnemies
                    .Where(candidate => candidate.DistanceSquared <= radiusSquared)
                    .Where(candidate => CanPreferTank(candidate.Enemy))
                    .OrderBy(candidate => candidate.DistanceSquared)
                    .ToList();

                foreach (var enemy in trackedEnemies.Keys.ToList())
                    TryNudgeTarget(enemy);

                foreach (var candidate in candidates)
                {
                    if (trackedEnemies.Count < maximumTargets)
                    {
                        if (TryNudgeTarget(candidate.Enemy))
                            trackedEnemies[candidate.Enemy] = CreateAggroState(candidate.Enemy);
                        continue;
                    }

                    Agent worstEnemy = trackedEnemies.Keys
                        .OrderByDescending(enemy => tank.Position.DistanceSquared(enemy.Position))
                        .FirstOrDefault();
                    if (worstEnemy == null)
                        break;

                    float worstDistance = (float)Math.Sqrt(tank.Position.DistanceSquared(worstEnemy.Position));
                    float candidateDistance = (float)Math.Sqrt(candidate.DistanceSquared);
                    if (candidateDistance + Math.Max(0f, role.TargetReplacementThreshold) >= worstDistance)
                        continue;

                    if (!TryNudgeTarget(candidate.Enemy))
                        continue;

                    trackedEnemies.Remove(worstEnemy);
                    trackedEnemies[candidate.Enemy] = CreateAggroState(candidate.Enemy);
                }

                UpdateTacticalMovement(now, agents);
            }

            private void TrimTrackedEnemies(int maximumTargets)
            {
                foreach (var enemy in trackedEnemies.Keys
                             .OrderBy(enemy => tank.Position.DistanceSquared(enemy.Position))
                             .Skip(maximumTargets)
                             .ToList())
                    trackedEnemies.Remove(enemy);
            }

            private AggroState CreateAggroState(Agent enemy) => new()
            {
                WasTargetingTank = enemy.GetTargetAgent() == tank,
            };

            private bool TryNudgeTarget(Agent enemy)
            {
                if (!IsValidEnemy(enemy))
                    return false;

                Agent currentTarget = enemy.GetTargetAgent();
                if (currentTarget == tank)
                    return true;
                if (IsActiveMeleeAttackAgainstAlly(enemy, currentTarget))
                    return false;

                if (IsActive(currentTarget))
                {
                    float strength = Math.Max(0f, role.AggroPriorityStrength);
                    float currentDistanceSquared = enemy.Position.DistanceSquared(currentTarget.Position);
                    float tankDistanceSquared = enemy.Position.DistanceSquared(tank.Position);
                    if (tankDistanceSquared > currentDistanceSquared * strength * strength)
                        return false;
                }

                enemy.SetTargetAgent(tank);
                return enemy.GetTargetAgent() == tank;
            }

            private bool CanPreferTank(Agent enemy)
            {
                Agent currentTarget = enemy.GetTargetAgent();
                if (currentTarget == tank)
                    return true;
                if (IsActiveMeleeAttackAgainstAlly(enemy, currentTarget))
                    return false;
                if (!IsActive(currentTarget))
                    return true;

                float strength = Math.Max(0f, role.AggroPriorityStrength);
                float tankDistanceSquared = enemy.Position.DistanceSquared(tank.Position);
                float currentDistanceSquared = enemy.Position.DistanceSquared(currentTarget.Position);
                return tankDistanceSquared <= currentDistanceSquared * strength * strength;
            }

            private bool IsActiveMeleeAttackAgainstAlly(Agent enemy, Agent target)
            {
                if (!IsActive(target) || target == tank || tank.Team == null || target.Team == null ||
                    !tank.Team.IsFriendOf(target.Team))
                    return false;

                Agent.ActionCodeType action = enemy.GetCurrentActionType(MainActionChannel);
                return action == Agent.ActionCodeType.ReadyMelee ||
                       action == Agent.ActionCodeType.ReleaseMelee ||
                       action == Agent.ActionCodeType.Kick ||
                       action == Agent.ActionCodeType.KickContinue ||
                       action == Agent.ActionCodeType.KickHit ||
                       action == Agent.ActionCodeType.WeaponBash;
            }

            private void UpdateTacticalMovement(float now, IEnumerable<Agent> agents)
            {
                if (movementActive || now < nextMovementAllowedTime || tank.Controller != AgentControllerType.AI ||
                    IsAttackAction(tank))
                    return;

                var focusedEnemies = trackedEnemies
                    .Where(pair => pair.Value.WasTargetingTank && IsValidEnemy(pair.Key))
                    .Select(pair => pair.Key)
                    .ToList();
                if (focusedEnemies.Count < MinimumAggroedEnemiesForMovement ||
                    focusedEnemies.Any(enemy =>
                        tank.Position.DistanceSquared(enemy.Position) <=
                        MovementEnemyStopDistance * MovementEnemyStopDistance))
                    return;

                float allySearchRadiusSquared = MovementAllySearchRadius * MovementAllySearchRadius;
                Agent nearestAlly = agents
                    .Where(IsValidAlly)
                    .Where(ally => tank.Position.DistanceSquared(ally.Position) <= allySearchRadiusSquared)
                    .OrderBy(ally => tank.Position.DistanceSquared(ally.Position))
                    .FirstOrDefault();
                if (nearestAlly == null)
                    return;

                float allyDistanceSquared = tank.Position.DistanceSquared(nearestAlly.Position);
                if (allyDistanceSquared <= MovementDesiredAllyDistance * MovementDesiredAllyDistance)
                    return;

                Vec2 towardAlly = nearestAlly.Position.AsVec2 - tank.Position.AsVec2;
                if (towardAlly.LengthSquared < 0.01f)
                    return;
                towardAlly.Normalize();

                movementDestination = tank.GetWorldPosition();
                movementDestination.SetVec2(tank.Position.AsVec2 + towardAlly *
                    Math.Min(MovementStepDistance, (float)Math.Sqrt(allyDistanceSquared)));
                if (!movementDestination.IsValid)
                    return;

                Vec3 destinationPoint = movementDestination.GetNavMeshVec3();
                float pathDistance = tank.GetPathDistanceToPoint(ref destinationPoint);
                if (pathDistance < 0f || float.IsNaN(pathDistance) || float.IsInfinity(pathDistance))
                {
                    movementDestination = default;
                    return;
                }

                tank.SetScriptedPosition(ref movementDestination, false, Agent.AIScriptedFrameFlags.None);
                movementActive = true;
            }

            private void UpdateActiveMovement()
            {
                if (!movementActive)
                    return;

                var focusedEnemies = trackedEnemies
                    .Where(pair => pair.Value.WasTargetingTank && IsValidEnemy(pair.Key))
                    .Select(pair => pair.Key)
                    .ToList();
                bool enemyTooClose = focusedEnemies.Any(enemy =>
                    tank.Position.DistanceSquared(enemy.Position) <=
                    MovementEnemyStopDistance * MovementEnemyStopDistance);
                bool reachedDestination = !movementDestination.IsValid ||
                    tank.Position.DistanceSquared(movementDestination.GetNavMeshVec3()) <=
                    MovementDestinationTolerance * MovementDestinationTolerance;
                if (focusedEnemies.Count < MinimumAggroedEnemiesForMovement ||
                    tank.Controller != AgentControllerType.AI || IsAttackAction(tank) || enemyTooClose ||
                    reachedDestination)
                {
                    StopMovement("movement stop condition reached");
                }
            }

            private void UpdateController()
            {
                if (tank.Controller != AgentControllerType.AI)
                    StopMovement("controller changed away from AI");
            }

            private void StopMovement(string reason)
            {
                if (!movementActive)
                    return;

                movementActive = false;
                movementDestination = default;
                nextMovementAllowedTime = (Mission.Current?.CurrentTime ?? 0f) + MovementRestartDelaySeconds;
                if (IsActive(tank))
                    tank.DisableScriptedMovement();
            }

            private void OnAgentRemoved(Agent victim, Agent attacker, AgentState state, KillingBlow blow)
            {
                if (disposed)
                    return;

                if (victim == tank)
                {
                    Dispose();
                    return;
                }

                if (!trackedEnemies.TryGetValue(victim, out AggroState aggroState))
                    return;

                bool wasFocusedOnTank = aggroState.WasTargetingTank;
                trackedEnemies.Remove(victim);
                if (!wasFocusedOnTank || state is not AgentState.Killed and not AgentState.Unconscious ||
                    attacker == null || attacker == tank || tank.Team == null || attacker.Team == null ||
                    !tank.Team.IsFriendOf(attacker.Team))
                    return;

                if (BLTAdoptAHeroModule.TournamentConfig.DisableKillRewardsInTournament &&
                    MissionHelpers.InTournament())
                    return;

                AwardParticipationReward(victim);
            }

            private void AwardParticipationReward(Agent victim)
            {
                float rewardShare = MathF.Clamp(role.SharedRewardPercentage / 100f, 0f, 1f);
                if (rewardShare <= 0f)
                    return;

                int normalGold = BLTAdoptAHeroModule.CommonConfig.GoldPerKill;
                int normalXP = BLTAdoptAHeroModule.CommonConfig.XPPerKill;
                float subBoost = Math.Max(BLTAdoptAHeroModule.CommonConfig.SubBoost, 1f);
                normalGold = (int)(normalGold * subBoost);
                normalXP = (int)(normalXP * subBoost);

                float? relativeScaling = BLTAdoptAHeroModule.CommonConfig.RelativeLevelScaling;
                if (relativeScaling.HasValue && victim?.Character != null)
                {
                    float levelBoost = BLTAdoptAHeroCommonMissionBehavior.RelativeLevelScaling(
                        hero.Level, victim.Character.Level, relativeScaling.Value,
                        BLTAdoptAHeroModule.CommonConfig.LevelScalingCap);
                    float goldLevelBoost = MathF.Max(MathF.Max(0f, levelBoost),
                        BLTAdoptAHeroModule.CommonConfig.MinimumGoldPerKill);
                    normalGold = (int)(normalGold * goldLevelBoost);
                    normalXP = (int)(normalXP * levelBoost);
                }

                pendingGold += normalGold * rewardShare;
                int gold = (int)Math.Floor(pendingGold);
                if (gold > 0 && BLTAdoptAHeroCampaignBehavior.Current != null)
                {
                    pendingGold -= gold;
                    BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(hero, gold);
                    BLTAdoptAHeroCommonMissionBehavior.Current?.RecordGoldGain(hero, gold);
                }

                pendingXP += normalXP * rewardShare;
                int xp = (int)Math.Floor(pendingXP);
                if (xp > 0)
                {
                    pendingXP -= xp;
                    SkillXP.ImproveSkill(hero, xp, SkillsEnum.All, auto: true);
                    BLTAdoptAHeroCommonMissionBehavior.Current?.RecordXPGain(hero, xp);
                }
            }

            private bool IsValidEnemy(Agent agent) =>
                IsActive(agent) && agent.IsAIControlled && !agent.IsMount && agent.IsHuman &&
                tank.Team != null && agent.Team != null && tank.Team.IsEnemyOf(agent.Team);

            private bool IsValidAlly(Agent agent) =>
                agent != tank && IsActive(agent) && !agent.IsMount && agent.IsHuman &&
                tank.Team != null && agent.Team != null && tank.Team.IsFriendOf(agent.Team);

            private static bool IsAttackAction(Agent agent)
            {
                Agent.ActionCodeType action = agent.GetCurrentActionType(MainActionChannel);
                return action == Agent.ActionCodeType.ReadyRanged ||
                       action == Agent.ActionCodeType.ReleaseRanged ||
                       action == Agent.ActionCodeType.ReleaseThrowing ||
                       action == Agent.ActionCodeType.ReadyMelee ||
                       action == Agent.ActionCodeType.ReleaseMelee ||
                       action == Agent.ActionCodeType.Kick ||
                       action == Agent.ActionCodeType.KickContinue ||
                       action == Agent.ActionCodeType.KickHit ||
                       action == Agent.ActionCodeType.WeaponBash;
            }

            private void Dispose()
            {
                if (disposed)
                    return;

                disposed = true;
                StopMovement("Tank state disposal");
                trackedEnemies.Clear();
                RegisteredTanks.Remove(tank);
            }

            private static bool IsActive(Agent agent) =>
                agent != null && agent.State == AgentState.Active && !agent.IsFadingOut();
        }
    }
}
