using System;
using System.Linq;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Util;
using HarmonyLib;

namespace BLTAdoptAHero
{
    internal class BLTHeroDetachmentBehavior : AutoMissionBehavior<BLTHeroDetachmentBehavior>
    {
        private enum DetachmentOrder { None, Charge, Hold, Follow, Navigate, Siege, Advance }

        public enum FormationSidePreference { Default, Left, Center, Right }
        public enum FormationDepthPreference { Default, Front, Back }

        public sealed class ControlStatus
        {
            public string PrimaryOrder;
            public FormationSidePreference Side;
            public FormationDepthPreference Depth;
            public IReadOnlyCollection<FormationClass> IgnoredClasses;
            public bool Hunter;
            public Agent HunterTarget;
            public FormationClass? FocusClass;
            public FiringOrder.RangedWeaponUsageOrderEnum? Firing;
            public RidingOrder.RidingOrderEnum? Riding;
            public RangedSiegeWeapon SiegeWeapon;
            public bool IsDetached;
        }

        private sealed class ControlPreferences
        {
            public FormationSidePreference Side;
            public FormationDepthPreference Depth;
            public readonly HashSet<FormationClass> IgnoredClasses = new();
            public bool Hunter;
            public Agent HunterTarget;
            public float LastFormationPositionUpdate;
        }

        private class DetachmentState
        {
            public HeroDetachment Detachment;
            public DetachmentOrder Order = DetachmentOrder.None;
            public WorldPosition HoldPosition;
            public WorldPosition NavigationTarget;
            public float LastNavigationReissueTime;
            public MovementOrder.MovementOrderEnum MovementBehaviorOrder;
            public FormationClass? FocusClass;
            public FiringOrder.RangedWeaponUsageOrderEnum? FiringOverride;
            public RidingOrder.RidingOrderEnum? RidingOverride;
            public int SelectedMountIndex = -1;
            public RangedSiegeWeapon SiegeWeapon;
            public DetachmentOrder PreviousOrder;
            public RidingOrder.RidingOrderEnum? PreviousRidingOverride;
            public int PreviousSelectedMountIndex = -1;
        }

        private readonly Dictionary<Agent, DetachmentState> _detachments = new();
        private readonly Dictionary<Agent, ControlPreferences> _preferences = new();
        private readonly Dictionary<StandingPoint, Agent> _siegeReservations = new();
        // Reused buffer to avoid allocation on every tick
        private readonly List<KeyValuePair<Agent, DetachmentState>> _tickBuffer = new();
        private readonly List<KeyValuePair<Agent, ControlPreferences>> _preferenceTickBuffer = new();
        private readonly HashSet<Agent> _rangedPriorityAgents = new();
        private readonly List<Agent> _rangedPriorityBuffer = new();
        private float _rangedPriorityTimer;

        public bool IsDetached(Agent agent)
        {
            if (agent == null || !_detachments.TryGetValue(agent, out var state)) return false;
            if (ReferenceEquals(agent.Detachment, state.Detachment)) return true;
            ReleaseStaleDetachment(agent, state);
            return false;
        }

        private void ReleaseStaleDetachment(Agent agent, DetachmentState state)
        {
            _detachments.Remove(agent);
            ReleaseSiegeReservation(agent, state, !agent.IsDetachedFromFormation);
            state.Detachment.ForgetAgent(agent);
            DestroyOwnedDetachment(state.Detachment);
            if (agent.IsActive() && !agent.IsDetachedFromFormation)
                RestoreFormationOrders(agent);
        }

        private static void DestroyOwnedDetachment(HeroDetachment detachment)
        {
            var manager = detachment?.ParentFormation?.Team?.DetachmentManager;
            if (manager?.ContainsDetachment(detachment) == true)
                manager.DestroyDetachment(detachment);
        }

        internal static void RestoreFormationOrders(Agent agent)
        {
            var formation = agent.Formation;
            if (formation == null) return;
            agent.DisableScriptedMovement();
            agent.DisableScriptedCombatMovement();
            agent.SetScriptedFlags(Agent.AIScriptedFrameFlags.None);
            agent.SetScriptedCombatFlags(Agent.AISpecialCombatModeFlags.None);
            agent.SetTargetAgent(null);
            agent.SetTargetFormationIndex(-1);
            agent.SetAutomaticTargetSelection(true);
            agent.SetSelectedMountIndex(-1);
            agent.SetFiringOrder(formation.FiringOrder.OrderEnum);
            agent.SetRidingOrder(formation.RidingOrder.OrderEnum);
            agent.HumanAIComponent?.RefreshBehaviorValues(
                formation.GetReadonlyMovementOrderReference().OrderEnum, formation.ArrangementOrder.OrderEnum);
            agent.UpdateFormationOrders();
            agent.ForceAiBehaviorSelection();
        }

        private ControlPreferences GetPreferences(Agent agent)
        {
            if (!_preferences.TryGetValue(agent, out var preferences))
                _preferences[agent] = preferences = new ControlPreferences();
            return preferences;
        }

        public void SetFormationSide(Agent agent, FormationSidePreference preference)
            => GetPreferences(agent).Side = preference;

        public void SetFormationDepth(Agent agent, FormationDepthPreference preference)
            => GetPreferences(agent).Depth = preference;

        public bool SetIgnoredClass(Agent agent, FormationClass formationClass, bool ignored)
        {
            var preferences = GetPreferences(agent);
            if (ignored)
            {
                if (_detachments.TryGetValue(agent, out var state) && state.FocusClass == formationClass)
                    state.FocusClass = null;
                return preferences.IgnoredClasses.Add(formationClass);
            }
            return preferences.IgnoredClasses.Remove(formationClass);
        }

        public void ClearIgnoredClasses(Agent agent)
        {
            GetPreferences(agent).IgnoredClasses.Clear();
            agent.SetTargetFormationIndex(-1);
            ApplyTargetPreferences(agent);
        }

        public void SetHunter(Agent agent, bool enabled)
        {
            var preferences = GetPreferences(agent);
            preferences.Hunter = enabled;
            if (!enabled)
            {
                preferences.HunterTarget = null;
                agent.SetTargetAgent(null);
                agent.SetTargetFormationIndex(-1);
                agent.SetAutomaticTargetSelection(true);
            }
            ApplyTargetPreferences(agent);
        }

        public ControlStatus GetControlStatus(Agent agent)
        {
            IsDetached(agent);
            _preferences.TryGetValue(agent, out var preferences);
            _detachments.TryGetValue(agent, out var state);
            return new ControlStatus
            {
                PrimaryOrder = GetOrderName(state?.Order ?? DetachmentOrder.None),
                Side = preferences?.Side ?? FormationSidePreference.Default,
                Depth = preferences?.Depth ?? FormationDepthPreference.Default,
                IgnoredClasses = preferences?.IgnoredClasses.ToList() ?? new List<FormationClass>(),
                Hunter = preferences?.Hunter == true,
                HunterTarget = preferences?.HunterTarget,
                FocusClass = state?.FocusClass,
                Firing = state?.FiringOverride,
                Riding = state?.RidingOverride,
                SiegeWeapon = state?.SiegeWeapon,
                IsDetached = state != null
            };
        }

        private static string GetOrderName(DetachmentOrder order) => order switch
        {
            DetachmentOrder.Charge => "charge",
            DetachmentOrder.Advance => "advance",
            DetachmentOrder.Hold => "hold",
            DetachmentOrder.Follow => "follow",
            DetachmentOrder.Navigate => "navigate",
            DetachmentOrder.Siege => "siege",
            _ => "formation"
        };

        public bool TryGetDetachment(Agent agent, out HeroDetachment detachment)
        {
            if (IsDetached(agent) && _detachments.TryGetValue(agent, out var state))
            {
                detachment = state.Detachment;
                return true;
            }
            detachment = null;
            return false;
        }

        public string Detach(Agent agent)
        {
            if (agent == null || !agent.IsActive()) return "Invalid agent";
            if (IsDetached(agent)) return "Already detached";

            var formation = agent.Formation;
            if (formation == null) return "No formation";

            HeroDetachment detachment = null;
            try
            {
                if (agent.IsDetachedFromFormation)
                {
                    if (!agent.TryAttachToFormation())
                        return "Failed to detach";
                }
                detachment = new HeroDetachment(formation);
                formation.JoinDetachment(detachment);
                formation.DetachUnit(agent, true);
                detachment.AddAgentAtSlotIndex(agent, 0);
        
                _detachments[agent] = new DetachmentState
                {
                    Detachment = detachment,
                    MovementBehaviorOrder = formation.GetReadonlyMovementOrderReference().OrderEnum
                };
            }
            catch (Exception e)
            {
                Log.Error($"Detach failed for agent {agent?.Name ?? "unknown"}: {e}");
                detachment?.RemoveAgent(agent);
                DestroyOwnedDetachment(detachment);
                if (formation.DetachedUnits.Contains(agent)) formation.AttachUnit(agent);
                RestoreFormationOrders(agent);
                return "{=BLTFormationControlFailed}Could not apply the formation command".Translate();
            }
            return null;
        }

        public string Attach(Agent agent)
        {
            if (agent == null) return "Invalid agent";
            IsDetached(agent);
            _detachments.TryGetValue(agent, out var state);
            if (state != null)
            {
                if (agent.CurrentlyUsedGameObject != null)
                    agent.StopUsingGameObjectMT(true, Agent.StopUsingGameObjectFlags.None);
                ReleaseSiegeReservation(agent, state);
                CleanupDetachment(agent, state);
            }
            else
            {
                if (!agent.IsDetachedFromFormation) return "Not detached";
                agent.TryAttachToFormation();
                RestoreFormationOrders(agent);
            }
            return null;
        }

        public string Charge(Agent agent)
        {
            if (agent == null || !agent.IsActive()) return "Invalid agent";
            if (!_detachments.TryGetValue(agent, out var state)) return "Not detached";
            ExitSiegeModeForNewOrder(agent, state);
            try
            {
                agent.DisableScriptedMovement();
                agent.DisableScriptedCombatMovement();
                agent.SetScriptedCombatFlags(Agent.AISpecialCombatModeFlags.None);
                agent.SetScriptedFlags(Agent.AIScriptedFrameFlags.None);
                agent.SetAutomaticTargetSelection(true);
                agent.HumanAIComponent?.SetBehaviorValueSet(HumanAIComponent.BehaviorValueSet.Default);
                state.Detachment.ClearFollowTarget();
                state.MovementBehaviorOrder = MovementOrder.MovementOrderEnum.Charge;
                state.Order = DetachmentOrder.Charge;
                ApplyCharge(agent, state);
            }
            catch (Exception e)
            {
                Log.Error($"Charge failed for {agent.Name}: {e}");
                return "{=BLTFormationControlFailed}Could not apply the formation command".Translate();
            }

            return null;
        }

        public string Hold(Agent agent)
        {
            if (agent == null || !agent.IsActive()) return "Invalid agent";
            if (!_detachments.TryGetValue(agent, out var state)) return "Not detached";
            ExitSiegeModeForNewOrder(agent, state);

            state.HoldPosition = agent.GetWorldPosition();
            state.Detachment.ClearFollowTarget();
            state.MovementBehaviorOrder = MovementOrder.MovementOrderEnum.Stop;
            state.Order = DetachmentOrder.Hold;
            ApplyHold(agent, state);
            return null;
        }

        public string Advance(Agent agent)
        {
            if (agent == null || !agent.IsActive()) return "Invalid agent";
            if (!agent.HasRangedWeapon(true))
                return "{=BLTFormationAdvanceNoAmmo}A ranged weapon with ammunition is required".Translate();
            if (!_detachments.TryGetValue(agent, out var state)) return "Not detached";
            ExitSiegeModeForNewOrder(agent, state);
            agent.DisableScriptedMovement();
            agent.DisableScriptedCombatMovement();
            agent.SetScriptedCombatFlags(Agent.AISpecialCombatModeFlags.None);
            agent.SetScriptedFlags(Agent.AIScriptedFrameFlags.None);
            state.Detachment.ClearFollowTarget();
            state.MovementBehaviorOrder = MovementOrder.MovementOrderEnum.Advance;
            state.Order = DetachmentOrder.Advance;
            state.LastNavigationReissueTime = -1f;
            ApplyAdvance(agent, state);
            ApplyOverrides(agent, state);
            return null;
        }

        public string FollowMainAgent(Agent agent)
        {
            if (agent == null || !agent.IsActive()) return "Invalid agent";
            if (!_detachments.TryGetValue(agent, out var state)) return "Not detached";
            if (Mission.Current?.MainAgent == null) return "Invalid follow target";
            ExitSiegeModeForNewOrder(agent, state);

            agent.DisableScriptedMovement();
            agent.DisableScriptedCombatMovement();
            state.Detachment.FollowMainAgent();
            state.MovementBehaviorOrder = MovementOrder.MovementOrderEnum.Follow;
            state.Order = DetachmentOrder.Follow;
            agent.ForceAiBehaviorSelection();
            return null;
        }

        public string FollowAgent(Agent agent, Agent target)
        {
            if (agent == null || !agent.IsActive()) return "Invalid agent";
            if (target == null || !target.IsActive()) return "Invalid follow target";
            if (!_detachments.TryGetValue(agent, out var state)) return "Not detached";
            ExitSiegeModeForNewOrder(agent, state);

            agent.DisableScriptedMovement();
            agent.DisableScriptedCombatMovement();
            state.Detachment.FollowAgent(target);
            state.MovementBehaviorOrder = MovementOrder.MovementOrderEnum.Follow;
            state.Order = DetachmentOrder.Follow;
            agent.ForceAiBehaviorSelection();
            return null;
        }

        public string Focus(Agent agent, FormationClass targetClass)
        {
            if (agent == null || !agent.IsActive()) return "Invalid agent";
            if (!_detachments.TryGetValue(agent, out var state)) return "Not detached";
            var target = FindFocusFormation(agent, targetClass);

            if (target == null) return "No target formation";

            GetPreferences(agent).IgnoredClasses.Remove(targetClass);
            state.FocusClass = targetClass;
            ApplyFocus(agent, state);
            return null;
        }

        public string ValidateFocus(Agent agent, FormationClass targetClass)
            => FindFocusFormation(agent, targetClass) == null ? "No target formation" : null;

        private static Formation FindFocusFormation(Agent agent, FormationClass targetClass)
            => Mission.Current.Teams
                .Where(team => team != null && team.IsEnemyOf(agent.Team))
                .SelectMany(team => team.FormationsIncludingSpecialAndEmpty)
                .Where(formation => formation != null && formation.CountOfUnits > 0)
                .Where(formation => formation.PhysicalClass == targetClass)
                .OrderBy(formation => formation.CachedAveragePosition.DistanceSquared(agent.Position.AsVec2))
                .FirstOrDefault();

        public string ClearFocus(Agent agent)
        {
            if (agent == null || !agent.IsActive()) return "Invalid agent";
            if (_detachments.TryGetValue(agent, out var state))
                state.FocusClass = null;
            agent.SetTargetFormationIndex(-1);
            agent.SetAutomaticTargetSelection(true);
            agent.ForceAiBehaviorSelection();
            return null;
        }

        public string SetFiringOrder(Agent agent, FiringOrder.RangedWeaponUsageOrderEnum order)
        {
            if (agent == null || !agent.IsActive()) return "Invalid agent";
            if (!_detachments.TryGetValue(agent, out var state)) return "Not detached";
            state.FiringOverride = order;
            agent.SetFiringOrder(order);
            return null;
        }

        public string SetRidingOrder(Agent agent, RidingOrder.RidingOrderEnum order, int selectedMountIndex = -1)
        {
            if (agent == null || !agent.IsActive()) return "Invalid agent";
            if (!_detachments.TryGetValue(agent, out var state)) return "Not detached";
            if (state.Order == DetachmentOrder.Siege && order == RidingOrder.RidingOrderEnum.Mount)
                return "{=BLTFormationSiegeMountBlocked}Cannot mount while using a siege weapon".Translate();
            state.RidingOverride = order;
            state.SelectedMountIndex = selectedMountIndex;
            state.Detachment.SuppressMovement(order == RidingOrder.RidingOrderEnum.Mount && agent.MountAgent == null);
            ApplyRidingOrder(agent, state);
            return null;
        }

        public string UseSiegeWeapon(Agent agent)
        {
            if (!Mission.Current.IsSiegeBattle) return "Not a siege";
            if (agent == null || !agent.IsActive()) return "Invalid agent";
            if (!_detachments.TryGetValue(agent, out var state)) return "Not detached";

            var candidates = Mission.Current.ActiveMissionObjects
                .OfType<RangedSiegeWeapon>()
                .Where(weapon => !weapon.IsDeactivated && !weapon.IsDestroyed)
                .Where(weapon => weapon.Side == agent.Team.Side)
                .Where(weapon => weapon.PilotStandingPoint != null
                                 && !weapon.PilotStandingPoint.IsDeactivated
                                 && !weapon.PilotStandingPoint.IsDisabledForAgent(agent))
                .OrderBy(weapon => weapon.GameEntity.GlobalPosition.DistanceSquared(agent.Position))
                .ToList();

            if (candidates.Count == 0) return "No available ranged siege weapon";

            RangedSiegeWeapon selected = null;
            foreach (var weapon in candidates)
            {
                var pilot = weapon.PilotStandingPoint;
                var occupant = pilot.UserAgent ?? pilot.MovingAgent;
                if (_siegeReservations.TryGetValue(pilot, out var reservedBy)
                    && reservedBy != null && reservedBy.IsActive() && reservedBy != agent)
                    continue;
                if (occupant != null && occupant != agent && occupant.GetAdoptedHero() != null)
                    continue;
                selected = weapon;
                break;
            }

            if (selected == null) return "All ranged siege weapons are occupied by BLT heroes";

            ReleaseSiegeReservation(agent, state);
            var standingPoint = selected.PilotStandingPoint;
            var npc = standingPoint.UserAgent ?? standingPoint.MovingAgent;
            if (npc != null && npc != agent)
                npc.StopUsingGameObjectMT(true, Agent.StopUsingGameObjectFlags.AutoAttachAfterStoppingUsingGameObject);

            state.PreviousOrder = state.Order == DetachmentOrder.Siege ? state.PreviousOrder : state.Order;
            if (state.Order != DetachmentOrder.Siege)
            {
                state.PreviousRidingOverride = state.RidingOverride;
                state.PreviousSelectedMountIndex = state.SelectedMountIndex;
            }
            state.RidingOverride = RidingOrder.RidingOrderEnum.Dismount;
            state.SelectedMountIndex = -1;
            ApplyRidingOrder(agent, state);
            state.SiegeWeapon = selected;
            state.Order = DetachmentOrder.Siege;
            _siegeReservations[standingPoint] = agent;
            standingPoint.FavoredUser = agent;
            BeginUsingSiegeWeapon(agent, state);
            return null;
        }

        public string LeaveSiegeWeapon(Agent agent)
        {
            if (agent == null || !agent.IsActive()) return "Invalid agent";
            if (!_detachments.TryGetValue(agent, out var state) || state.SiegeWeapon == null)
                return "Hero is not assigned to a siege weapon";

            if (agent.CurrentlyUsedGameObject != null)
                agent.StopUsingGameObjectMT(true, Agent.StopUsingGameObjectFlags.None);
            ReleaseSiegeReservation(agent, state);
            var previousOrder = state.PreviousOrder;
            state.Order = previousOrder;
            state.PreviousOrder = DetachmentOrder.None;
            state.RidingOverride = state.PreviousRidingOverride;
            state.PreviousRidingOverride = null;
            state.SelectedMountIndex = state.PreviousSelectedMountIndex;
            state.PreviousSelectedMountIndex = -1;
            if (previousOrder == DetachmentOrder.None)
                return Attach(agent);
            ReapplyPrimaryOrder(agent, state);
            return null;
        }

        private void BeginUsingSiegeWeapon(Agent agent, DetachmentState state)
        {
            if (agent.MountAgent != null) return;
            var point = state.SiegeWeapon?.PilotStandingPoint;
            if (point == null || point.IsDeactivated) return;
            if (point.UserAgent != null && point.UserAgent != agent) return;
            if (agent.CurrentlyUsedGameObject == point) return;
            if (agent.HumanAIComponent?.GetCurrentlyMovingGameObject() != point
                || !agent.AIMoveToGameObjectIsEnabled())
                agent.AIMoveToGameObjectEnable(point, state.Detachment, Agent.AIScriptedFrameFlags.NoAttack);
        }

        private void ReleaseSiegeReservation(Agent agent, DetachmentState state, bool stopUse = true)
        {
            var point = state?.SiegeWeapon?.PilotStandingPoint;
            if (point != null && stopUse
                && (agent.CurrentlyUsedGameObject == point
                    || agent.HumanAIComponent?.GetCurrentlyMovingGameObject() == point))
                agent.StopUsingGameObjectMT(true, Agent.StopUsingGameObjectFlags.None);
            if (point != null && _siegeReservations.TryGetValue(point, out var owner) && owner == agent)
            {
                _siegeReservations.Remove(point);
                if (point.FavoredUser == agent) point.FavoredUser = null;
            }
            if (state != null) state.SiegeWeapon = null;
        }

        private void ExitSiegeModeForNewOrder(Agent agent, DetachmentState state)
        {
            if (state?.SiegeWeapon == null) return;
            if (agent.CurrentlyUsedGameObject != null)
                agent.StopUsingGameObjectMT(true, Agent.StopUsingGameObjectFlags.None);
            ReleaseSiegeReservation(agent, state);
            state.PreviousOrder = DetachmentOrder.None;
            state.RidingOverride = state.PreviousRidingOverride;
            state.PreviousRidingOverride = null;
            state.SelectedMountIndex = state.PreviousSelectedMountIndex;
            state.PreviousSelectedMountIndex = -1;
        }

        private void ReapplyPrimaryOrder(Agent agent, DetachmentState state)
        {
            switch (state.Order)
            {
                case DetachmentOrder.Charge: ApplyCharge(agent, state); break;
                case DetachmentOrder.Advance: ApplyAdvance(agent, state); break;
                case DetachmentOrder.Hold: ApplyHold(agent, state); break;
            }
        }

        public string TargetDoor(Agent agent)
        {
            if (!Mission.Current.IsSiegeBattle) return "Not a siege";
            if (agent == null || !agent.IsActive()) return "Invalid agent";
            if (!_detachments.TryGetValue(agent, out var state)) return "Not detached";

            try
            {
                CastleGate nearestGate = null;
                float nearestDist = float.MaxValue;

                foreach (var obj in Mission.Current.ActiveMissionObjects)
                {
                    if (obj is not CastleGate gate) continue;
                    if (gate.IsGateOpen && agent.Team.IsAttacker) continue;

                    float dist = gate.GameEntity.GlobalPosition.DistanceSquared(agent.Position);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestGate = gate;
                    }
                }

                if (nearestGate == null) return "No gate found";

                if (agent.Team.IsAttacker)
                {
                    ExitSiegeModeForNewOrder(agent, state);
                    state.Detachment.ClearFollowTarget();
                    agent.DisableScriptedMovement();
                    state.Order = DetachmentOrder.None;
                    agent.SetScriptedTargetEntity(
                        nearestGate.GameEntity,
                        Agent.AISpecialCombatModeFlags.AttackEntity,
                        true);
                }
                else
                {
                    var gatePosition = nearestGate.MiddlePosition ?? nearestGate.WaitPosition;
                    if (gatePosition == null) return "Gate has no valid position";
                    WorldPosition pos = gatePosition.Position;

                    if (!pos.IsValid) return "Gate has no valid position";

                    ExitSiegeModeForNewOrder(agent, state);
                    state.Detachment.ClearFollowTarget();
                    agent.DisableScriptedMovement();
                    state.NavigationTarget = pos;
                    state.MovementBehaviorOrder = MovementOrder.MovementOrderEnum.Move;
                    state.Order = DetachmentOrder.Navigate;
                    state.LastNavigationReissueTime = Mission.Current.CurrentTime;
                    SetAgentNavigatingAggressively(agent);
                    agent.SetScriptedPosition(ref pos, false, Agent.AIScriptedFrameFlags.NeverSlowDown);
                }
            }
            catch (Exception e)
            {
                Log.Error($"Gate order failed for {agent.Name}: {e}");
                return "{=BLTFormationControlFailed}Could not apply the formation command".Translate();
            }

            return null;
        }

        public string Walls(Agent agent)
        {
            if (!Mission.Current.IsSiegeBattle) return "Not a siege";
            if (agent == null || !agent.IsActive()) return "Invalid agent";
            if (!_detachments.TryGetValue(agent, out var state)) return "Not detached";
            try
            {
                float nearestDist = float.MaxValue;
                WorldPosition targetPos = default;
                bool found = false;

                // 1. Wall segments
                WallSegment bestWall = null;
                float bestWallDist = float.MaxValue;

                foreach (var obj in Mission.ActiveMissionObjects)
                {
                    if (obj is not WallSegment wall) continue;

                    if (!wall.IsBreachedWall && agent.Team.IsAttacker)
                        continue;

                    float dist = wall.GameEntity.GlobalPosition.DistanceSquared(agent.Position);

                    if (dist < bestWallDist)
                    {
                        bestWallDist = dist;
                        bestWall = wall;
                    }
                }

                if (bestWall != null)
                {

                    if (agent.Team.IsAttacker)
                    {
                        var tac = bestWall.MiddlePosition ?? bestWall.AttackerWaitPosition ?? bestWall.WaitPosition;

                        if (tac == null) return "No attacker pos";

                        targetPos = tac.Position;
                        found = true;
                    }
                    else
                    {
                        //if (bestWall.DefencePoints != null && bestWall.DefencePoints.Any())
                        //{
                        //    var point = bestWall.DefencePoints
                        //        .Select(dp =>
                        //        {
                        //            dp.PurgeInactiveDefenders();

                        //            var pos = dp.GameEntity.GlobalPosition;

                        //            float dist = pos.DistanceSquared(agent.Position);
                        //            int occupied = dp.CountOccupiedDefenderPositions();

                        //            float score = dist + occupied * 5f;

                        //            return new { dp, pos, score };
                        //        })
                        //        .OrderBy(x => x.score)
                        //        .FirstOrDefault();

                        //    if (point != null)
                        //    {
                        //        // small offset so multiple agents don't stack on exact same spot
                        //        Vec3 pos = point.pos;
                        //        Vec3 dir = (pos - agent.Position);
                        //        dir.z = 0;
                        //        dir.Normalize();

                        //        pos += dir * 1.5f;

                        //        var worldPos = new WorldPosition(Mission.Scene, pos);

                        //        if (worldPos.IsValid)
                        //        {
                        //            targetPos = worldPos;
                        //            found = true;
                        //        }
                        //    }
                        //}
                        //else
                        //{

                        if (bestWall.MiddlePosition == null) return "No defender pos";
                        targetPos = bestWall.MiddlePosition.Position;
                        found = true;

                        //Log.Trace("[Walls] No DefencePoint, fallback");
                        //}
                    }
                }

                // 2. Siege towers (attackers only)
                if (!found && agent.Team.IsAttacker)
                {
                    foreach (var obj in Mission.ActiveMissionObjects)
                    {
                        if (obj is not SiegeTower tower) continue;
                        if (tower.IsDeactivated /*|| !tower.HasArrivedAtTarget*/) continue;

                        float dist = tower.GameEntity.GlobalPosition.DistanceSquared(agent.Position);
                        if (dist < nearestDist)
                        {
                            nearestDist = dist;
                            targetPos = new WorldPosition(Mission.Scene, tower.GameEntity.GlobalPosition);
                            found = true;
                        }
                    }
                }

                // 3. Ladders (attackers only)
                if (!found && agent.Team.IsAttacker)
                {
                    foreach (var obj in Mission.ActiveMissionObjects)
                    {
                        if (obj is not SiegeLadder ladder) continue;
                        foreach (var sp in ladder.StandingPoints)
                        {
                            if (sp == null || sp.IsDeactivated || sp.HasUser) continue;
                            float dist = sp.GameEntity.GlobalPosition.DistanceSquared(agent.Position);
                            if (dist < nearestDist)
                            {
                                nearestDist = dist;
                                targetPos = new WorldPosition(sp.GameEntity.Scene, sp.GameEntity.GlobalPosition);
                                found = true;
                            }
                        }
                    }
                }

                if (!found) return "No valid target (wall/tower/ladder)";
                if (!targetPos.IsValid) return "No valid target (wall/tower/ladder)";

                ExitSiegeModeForNewOrder(agent, state);
                state.Detachment.ClearFollowTarget();
                agent.DisableScriptedMovement();
                agent.DisableScriptedCombatMovement();
                state.NavigationTarget = targetPos;
                state.MovementBehaviorOrder = MovementOrder.MovementOrderEnum.Move;
                state.Order = DetachmentOrder.Navigate;
                state.LastNavigationReissueTime = Mission.Current.CurrentTime;
                SetAgentNavigatingAggressively(agent);
                agent.SetScriptedPosition(ref targetPos, false, Agent.AIScriptedFrameFlags.NeverSlowDown);
            }
            catch (Exception e)
            {
                Log.Error($"Wall order failed for {agent.Name}: {e}");
                return "{=BLTFormationControlFailed}Could not apply the formation command".Translate();
            }
            return null;
        }

        // --- Mission callbacks ---

        public override void OnMissionTick(float dt)
        {
            // Copy to buffer to avoid crash if _detachments is modified during iteration
            // (e.g. agent dies mid-tick triggering OnAgentRemoved)
            _tickBuffer.Clear();
            _tickBuffer.AddRange(_detachments);

            foreach (var kvp in _tickBuffer)
            {
                var agent = kvp.Key;
                var state = kvp.Value;

                // Re-check still in dict — could have been removed by OnAgentRemoved during tick
                if (!_detachments.ContainsKey(agent)) continue;
                if (!agent.IsActive()) continue;
                if (!IsDetached(agent)) continue;

                bool mounting = state.RidingOverride == RidingOrder.RidingOrderEnum.Mount
                                && agent.MountAgent == null;
                if (!mounting)
                {
                    switch (state.Order)
                    {
                        case DetachmentOrder.Hold:
                            ApplyHold(agent, state);
                            break;
                        case DetachmentOrder.Charge:
                            ApplyCharge(agent, state);
                            break;
                        case DetachmentOrder.Advance:
                            ApplyAdvance(agent, state);
                            break;
                        case DetachmentOrder.Navigate:
                            ApplyNavigate(agent, state);
                            break;
                        case DetachmentOrder.Siege:
                            if (state.SiegeWeapon == null || state.SiegeWeapon.IsDestroyed || state.SiegeWeapon.IsDeactivated)
                            {
                                ReleaseSiegeReservation(agent, state);
                                var previousOrder = state.PreviousOrder;
                                state.Order = previousOrder;
                                state.PreviousOrder = DetachmentOrder.None;
                                state.RidingOverride = state.PreviousRidingOverride;
                                state.PreviousRidingOverride = null;
                                state.SelectedMountIndex = state.PreviousSelectedMountIndex;
                                state.PreviousSelectedMountIndex = -1;
                                if (previousOrder == DetachmentOrder.None)
                                {
                                    Attach(agent);
                                    continue;
                                }
                                ReapplyPrimaryOrder(agent, state);
                            }
                            else
                                BeginUsingSiegeWeapon(agent, state);
                            break;
                    }
                }

                if (state.Order == DetachmentOrder.Siege)
                    ApplyRidingOrder(agent, state);
                else
                    ApplyOverrides(agent, state);
            }

            _preferenceTickBuffer.Clear();
            _preferenceTickBuffer.AddRange(_preferences);
            foreach (var pair in _preferenceTickBuffer)
            {
                var agent = pair.Key;
                if (agent == null || !agent.IsActive()) continue;
                if (_detachments.ContainsKey(agent)) continue;
                ApplyTargetPreferences(agent);
                ApplyFormationPositionPreference(agent, pair.Value);
            }

            _rangedPriorityTimer += dt;
            if (_rangedPriorityTimer >= 0.5f)
            {
                _rangedPriorityTimer = 0f;
                UpdateRangedPriority();
            }
        }

        public override void OnAgentRemoved(Agent killedAgent, Agent killerAgent,
            AgentState agentState, KillingBlow blow)
        {
            if (killedAgent != null && _detachments.TryGetValue(killedAgent, out var state))
            {
                ReleaseSiegeReservation(killedAgent, state);
                CleanupDetachmentOnDeath(killedAgent, state);
            }
            if (killedAgent != null) _preferences.Remove(killedAgent);
            if (killedAgent != null) _rangedPriorityAgents.Remove(killedAgent);
        }

        public override void OnAgentDeleted(Agent affectedAgent)
        {
            if (affectedAgent != null)
            {
                if (_detachments.TryGetValue(affectedAgent, out var state))
                {
                    _detachments.Remove(affectedAgent);
                    ReleaseSiegeReservation(affectedAgent, state, false);
                    state.Detachment.ForgetAgent(affectedAgent);
                    DestroyOwnedDetachment(state.Detachment);
                }
                _detachments.Remove(affectedAgent);
                _preferences.Remove(affectedAgent);
                _rangedPriorityAgents.Remove(affectedAgent);
            }
        }

        protected override void OnEndMission()
        {
            _detachments.Clear();
            _tickBuffer.Clear();
            _preferences.Clear();
            _preferenceTickBuffer.Clear();
            _siegeReservations.Clear();
            _rangedPriorityAgents.Clear();
            _rangedPriorityBuffer.Clear();
            _rangedPriorityTimer = 0f;
        }

        // --- Helpers ---

        private static void ApplyHold(Agent agent, DetachmentState state)
        {
            if (!state.HoldPosition.IsValid) return;
            agent.DisableScriptedCombatMovement();
            var pos = state.HoldPosition;
            agent.SetScriptedPosition(ref pos, false, Agent.AIScriptedFrameFlags.NeverSlowDown);
        }

        private static void ApplyNavigate(Agent agent, DetachmentState state)
        {
            const float ReissueInterval = 1.5f;
            const float ArrivedDistanceSq = 9f;

            if (!state.NavigationTarget.IsValid) return;

            float now = Mission.Current.CurrentTime;
            float distSq = agent.Position.AsVec2.DistanceSquared(state.NavigationTarget.AsVec2);

            if (distSq < ArrivedDistanceSq)
            {
                state.HoldPosition = agent.GetWorldPosition();
                state.MovementBehaviorOrder = MovementOrder.MovementOrderEnum.Stop;
                state.Order = DetachmentOrder.Hold;
                ClearAgentNavigatingAggressively(agent);
                ApplyHold(agent, state);
                return;
            }

            if (now - state.LastNavigationReissueTime > ReissueInterval)
            {
                state.LastNavigationReissueTime = now;
                var pos = state.NavigationTarget;
                agent.SetScriptedPosition(ref pos, false, Agent.AIScriptedFrameFlags.NeverSlowDown);
            }
        }

        private static void SetAgentNavigatingAggressively(Agent agent)
        {
            agent.SetAutomaticTargetSelection(false);
            agent.HumanAIComponent?.SetBehaviorValueSet(HumanAIComponent.BehaviorValueSet.DefaultDetached);
            agent.SetScriptedFlags(agent.GetScriptedFlags() | Agent.AIScriptedFrameFlags.NeverSlowDown);
        }

        private static void ClearAgentNavigatingAggressively(Agent agent)
        {
            agent.SetAutomaticTargetSelection(true);
            agent.HumanAIComponent?.SetBehaviorValueSet(HumanAIComponent.BehaviorValueSet.Default);
            agent.SetScriptedFlags(agent.GetScriptedFlags() & ~Agent.AIScriptedFrameFlags.NeverSlowDown);
        }

        private static void ApplyCharge(Agent agent, DetachmentState state)
        {
            agent.DisableScriptedMovement();
            agent.DisableScriptedCombatMovement();
            agent.SetScriptedCombatFlags(Agent.AISpecialCombatModeFlags.None);
            agent.SetScriptedFlags(Agent.AIScriptedFrameFlags.None);
            agent.SetAutomaticTargetSelection(true);
            agent.HumanAIComponent?.SetBehaviorValueSet(HumanAIComponent.BehaviorValueSet.Charge);
        }

        private void ApplyAdvance(Agent agent, DetachmentState state)
        {
            if (Mission.Current.CurrentTime - state.LastNavigationReissueTime < 0.5f) return;
            state.LastNavigationReissueTime = Mission.Current.CurrentTime;
            ApplyTargetPreferences(agent);
            var preferences = GetPreferences(agent);
            var target = Mission.Current.Agents
                .Where(other => other.IsActive() && other.IsHuman && other.Team != null
                    && other.Team.IsEnemyOf(agent.Team)
                    && (other.Formation == null || !preferences.IgnoredClasses.Contains(other.Formation.PhysicalClass))
                    && (!state.FocusClass.HasValue || other.Formation?.PhysicalClass == state.FocusClass))
                .OrderBy(other => other == preferences.HunterTarget ? 0 : 1)
                .ThenBy(other => other.Position.DistanceSquared(agent.Position))
                .FirstOrDefault();
            if (target == null)
            {
                agent.DisableScriptedMovement();
                return;
            }

            // Vanilla Advance stops ranged formations at MissileRangeAdjusted from their target.
            // Use the individual agent's range so mixed formations and throwing weapons work too.
            float range = agent.HasRangedWeapon(true) ? agent.GetMissileRangeWithHeightDifferenceAux(target.Position.z) : 0f;
            var direction = (target.Position.AsVec2 - agent.Position.AsVec2).Normalized();
            var position = target.GetWorldPosition();
            position.SetVec2(position.AsVec2 - direction * Math.Max(0f, range));
            if (position.IsValid && (!Mission.Current.IsPositionInsideBoundaries(position.AsVec2)
                || position.GetNavMesh() == UIntPtr.Zero))
            {
                float penalty = 1f;
                position = Mission.Current.GetAlternatePositionForNavmeshlessOrOutOfBoundsPosition(
                    position.AsVec2 - agent.Position.AsVec2, position, ref penalty);
            }
            if (!position.IsValid || !Mission.Current.IsPositionInsideBoundaries(position.AsVec2)
                || position.GetNavMesh() == UIntPtr.Zero)
            {
                agent.DisableScriptedMovement();
                return;
            }
            if (agent.Position.AsVec2.DistanceSquared(position.AsVec2) <= 4f)
                agent.DisableScriptedMovement();
            else
                agent.SetScriptedPosition(ref position, false, Agent.AIScriptedFrameFlags.None);
        }

        private void UpdateRangedPriority()
        {
            var mission = Mission.Current;
            bool enabled = BLTAdoptAHeroModule.CommonConfig.PrioritizeRangedAmmo
                && mission?.Mode == MissionMode.Battle && mission.IsDeploymentFinished
                && !mission.IsNavalBattle && !BannerlordApi.IsNavalRaidBattle(mission);
            _rangedPriorityBuffer.Clear();
            _rangedPriorityBuffer.AddRange(_rangedPriorityAgents);
            foreach (var agent in _rangedPriorityBuffer)
            {
                if (enabled && CanPrioritizeRanged(agent)) continue;
                _rangedPriorityAgents.Remove(agent);
                if (agent.IsActive()) RefreshCombatBehavior(agent);
            }
            if (!enabled) return;
            foreach (var agent in mission.Agents)
            {
                if (!CanPrioritizeRanged(agent) || agent.GetAdoptedHero() == null) continue;
                _rangedPriorityAgents.Add(agent);
                ApplyCombatBehavior(agent);
                if (agent.GetFiringOrder() != (int)FiringOrder.RangedWeaponUsageOrderEnum.HoldYourFire
                    && agent.WieldedWeapon.CurrentUsageItem?.IsRangedWeapon != true
                    && !agent.HumanAIComponent.IsInImportantCombatAction())
                {
                    for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumAllWeaponSlots; slot++)
                    {
                        if (!agent.Equipment.HasAmmo(slot, out _, out _, out bool emptySlot) || emptySlot) continue;
                        agent.TryToWieldWeaponInSlot(slot, Agent.WeaponWieldActionType.WithAnimation, false);
                        break;
                    }
                }
            }
        }

        private bool CanPrioritizeRanged(Agent agent)
        {
            if (!agent.IsActive() || !agent.IsHuman || !agent.IsAIControlled || agent.IsRetreating()
                || agent.HumanAIComponent == null || agent.CurrentlyUsedGameObject != null
                || agent.GetFiringOrder() == (int)FiringOrder.RangedWeaponUsageOrderEnum.HoldYourFire
                || !agent.HasRangedWeapon(true)) return false;
            if (!_detachments.TryGetValue(agent, out var state)
                || !ReferenceEquals(agent.Detachment, state.Detachment))
            {
                var movement = agent.Formation?.GetReadonlyMovementOrderReference().OrderEnum;
                return movement != MovementOrder.MovementOrderEnum.Retreat
                    && movement != MovementOrder.MovementOrderEnum.FallBack;
            }
            return state.Order != DetachmentOrder.Siege
                && state.FiringOverride != FiringOrder.RangedWeaponUsageOrderEnum.HoldYourFire
                && !(state.RidingOverride == RidingOrder.RidingOrderEnum.Mount && agent.MountAgent == null);
        }

        private void RefreshCombatBehavior(Agent agent)
        {
            _detachments.TryGetValue(agent, out var state);
            if (state != null && !ReferenceEquals(agent.Detachment, state.Detachment)) state = null;
            agent.HumanAIComponent?.RefreshBehaviorValues(
                state?.MovementBehaviorOrder ?? agent.Formation?.GetReadonlyMovementOrderReference().OrderEnum
                    ?? MovementOrder.MovementOrderEnum.Stop,
                agent.Formation?.ArrangementOrder.OrderEnum ?? ArrangementOrder.ArrangementOrderEnum.Line);
        }

        internal void ApplyCombatBehavior(Agent agent)
        {
            var ai = agent?.HumanAIComponent;
            if (ai == null || !agent.IsActive() || !agent.IsAIControlled) return;
            _detachments.TryGetValue(agent, out var state);
            if (state != null && !ReferenceEquals(agent.Detachment, state.Detachment)) state = null;
            // RefreshBehaviorValues always selects DefaultDetached before inspecting the order.
            if (state?.Order == DetachmentOrder.Charge)
                ai.SetBehaviorValueSet(HumanAIComponent.BehaviorValueSet.Charge);
            else if (state?.Order == DetachmentOrder.Advance)
                ai.SetBehaviorValueSet(HumanAIComponent.BehaviorValueSet.Default);

            bool priority = BLTAdoptAHeroModule.CommonConfig.PrioritizeRangedAmmo
                && _rangedPriorityAgents.Contains(agent) && CanPrioritizeRanged(agent);
            if (!priority) return;
            ai.OverrideBehaviorParams(HumanAIComponent.AISimpleBehaviorKind.Ranged, 20f, 7f, 20f, 20f, 20f);
            ai.OverrideBehaviorParams(HumanAIComponent.AISimpleBehaviorKind.RangedHorseback, 20f, 15f, 20f, 30f, 20f);
            ai.OverrideBehaviorParams(HumanAIComponent.AISimpleBehaviorKind.Melee, 8f, 2f, 0.1f, 20f, 0.1f);
            ai.OverrideBehaviorParams(HumanAIComponent.AISimpleBehaviorKind.ChargeHorseback, 2f, 3f, 0.1f, 30f, 0.1f);
            // Ordinary formations (including Charge) must not beat the ranged combat movement.
            // Explicit individual positioning and retreat orders still retain their movement.
            if (state == null || state.Order == DetachmentOrder.Charge)
            {
                var movement = agent.Formation?.GetReadonlyMovementOrderReference().OrderEnum;
                if (movement != MovementOrder.MovementOrderEnum.Retreat && movement != MovementOrder.MovementOrderEnum.FallBack)
                    ai.OverrideBehaviorParams(HumanAIComponent.AISimpleBehaviorKind.GoToPos, 0f, 7f, 0f, 20f, 0f);
            }
        }

        private void ApplyOverrides(Agent agent, DetachmentState state)
        {
            agent.HumanAIComponent?.RefreshBehaviorValues(
                state.MovementBehaviorOrder,
                agent.Formation?.ArrangementOrder.OrderEnum ?? ArrangementOrder.ArrangementOrderEnum.Line);

            ApplyCombatBehavior(agent);

            ApplyTargetPreferences(agent);

            if (state.FiringOverride.HasValue)
                agent.SetFiringOrder(state.FiringOverride.Value);

            if (state.RidingOverride.HasValue)
                ApplyRidingOrder(agent, state);
        }

        private void ApplyFocus(Agent agent, DetachmentState state)
        {
            if (!state.FocusClass.HasValue) return;

            var preferences = GetPreferences(agent);
            if (preferences.IgnoredClasses.Contains(state.FocusClass.Value)) return;

            var target = Mission.Current.Teams
                .Where(team => team != null && team.IsEnemyOf(agent.Team))
                .SelectMany(team => team.FormationsIncludingSpecialAndEmpty)
                .Where(formation => formation != null && formation.CountOfUnits > 0)
                .Where(formation => formation.PhysicalClass == state.FocusClass.Value)
                .OrderBy(formation => formation.CachedAveragePosition.DistanceSquared(agent.Position.AsVec2))
                .FirstOrDefault();

            if (target == null) return;
            agent.SetAutomaticTargetSelection(true);
            agent.SetTargetFormationIndex(target.Index);
        }

        private void ApplyTargetPreferences(Agent agent)
        {
            if (!_preferences.TryGetValue(agent, out var preferences)) return;

            if (preferences.Hunter)
            {
                var target = Mission.Current.Agents
                    .Where(candidate => candidate != null && candidate.IsActive()
                                        && candidate.Team != null && candidate.Team.IsEnemyOf(agent.Team)
                                        && candidate.GetAdoptedHero() != null
                                        && (candidate.Formation == null
                                            || !preferences.IgnoredClasses.Contains(candidate.Formation.PhysicalClass)))
                    .OrderBy(candidate => candidate.Position.DistanceSquared(agent.Position))
                    .FirstOrDefault();
                preferences.HunterTarget = target;
                if (target != null)
                {
                    agent.SetAutomaticTargetSelection(false);
                    agent.SetTargetAgent(target);
                    if (target.Formation != null) agent.SetTargetFormationIndex(target.Formation.Index);
                    return;
                }
                agent.SetTargetAgent(null);
                agent.SetTargetFormationIndex(-1);
                agent.SetAutomaticTargetSelection(true);
            }
            else
                preferences.HunterTarget = null;

            if (_detachments.TryGetValue(agent, out var state) && state.FocusClass.HasValue
                && !preferences.IgnoredClasses.Contains(state.FocusClass.Value))
            {
                ApplyFocus(agent, state);
                return;
            }

            if (preferences.IgnoredClasses.Count > 0)
            {
                var target = Mission.Current.Teams
                    .Where(team => team != null && team.IsEnemyOf(agent.Team))
                    .SelectMany(team => team.FormationsIncludingSpecialAndEmpty)
                    .Where(formation => formation != null && formation.CountOfUnits > 0
                                        && !preferences.IgnoredClasses.Contains(formation.PhysicalClass))
                    .OrderBy(formation => formation.CachedAveragePosition.DistanceSquared(agent.Position.AsVec2))
                    .FirstOrDefault();
                agent.SetAutomaticTargetSelection(true);
                agent.SetTargetFormationIndex(target?.Index ?? -1);
            }
        }

        private static void ApplyFormationPositionPreference(Agent agent, ControlPreferences preferences)
        {
            if (preferences.Side == FormationSidePreference.Default
                && preferences.Depth == FormationDepthPreference.Default) return;
            if (Mission.Current.CurrentTime - preferences.LastFormationPositionUpdate < 2f) return;
            preferences.LastFormationPositionUpdate = Mission.Current.CurrentTime;

            var formation = agent.Formation;
            if (formation?.Arrangement == null || agent is not IFormationUnit unit) return;
            var units = formation.Arrangement.GetAllUnits();
            int width = Math.Max(1, units.Select(other => other.FormationFileIndex).DefaultIfEmpty(-1).Max() + 1);
            int currentFile = unit.FormationFileIndex;
            int currentRank = unit.FormationRankIndex;
            if (currentFile < 0 || currentRank < 0) return;

            bool Matches(IFormationUnit candidate)
            {
                bool sideMatches = preferences.Side switch
                {
                    FormationSidePreference.Left => candidate.FormationFileIndex < Math.Max(1, width / 3),
                    FormationSidePreference.Center => candidate.FormationFileIndex >= width / 3
                                                      && candidate.FormationFileIndex < Math.Max(width / 3 + 1, width * 2 / 3),
                    FormationSidePreference.Right => candidate.FormationFileIndex >= width * 2 / 3,
                    _ => true
                };
                bool depthMatches = preferences.Depth switch
                {
                    FormationDepthPreference.Front => candidate.FormationRankIndex == 0,
                    FormationDepthPreference.Back => candidate.FormationRankIndex == formation.Arrangement.RankCount - 1,
                    _ => true
                };
                return sideMatches && depthMatches;
            }

            if (Matches(unit)) return;
            var candidate = units
                .Where(other => other != unit && other.FormationFileIndex >= 0 && other.FormationRankIndex >= 0)
                .Where(Matches)
                .OrderBy(other => Math.Abs(other.FormationFileIndex - currentFile)
                                  + Math.Abs(other.FormationRankIndex - currentRank))
                .FirstOrDefault();
            if (candidate != null) formation.Arrangement.SwitchUnitLocations(candidate, unit);
        }

        private static void ApplyRidingOrder(Agent agent, DetachmentState state)
        {
            if (!state.RidingOverride.HasValue) return;
            bool mounting = state.RidingOverride == RidingOrder.RidingOrderEnum.Mount
                            && agent.MountAgent == null;
            state.Detachment.SuppressMovement(mounting);
            if (state.SelectedMountIndex >= 0)
                agent.SetSelectedMountIndex(state.SelectedMountIndex);
            agent.SetRidingOrder(state.RidingOverride.Value);
        }

        private void CleanupDetachmentOnDeath(Agent agent, DetachmentState state)
        {
            // Safe to call on dying agent — does NOT call AttachUnit
            try { ClearAgentNavigatingAggressively(agent); } catch { }

            var detachment = state.Detachment;
            try { detachment?.RemoveAgent(agent); } catch { }
            _detachments.Remove(agent);
            DestroyOwnedDetachment(detachment);
        }

        private void CleanupDetachment(Agent agent, DetachmentState state)
        {
            // Remove control before AttachUnit refreshes AI and invokes our Harmony postfix.
            _detachments.Remove(agent);
            ClearAgentNavigatingAggressively(agent);

            var detachment = state.Detachment;
            var formation = agent.Formation;

            // 1. remove from custom detachment first
            detachment?.RemoveAgent(agent);

            // 2. force engine state reset BEFORE reattach
            DestroyOwnedDetachment(detachment);
            agent.TryRemoveAllDetachmentScores();

            // 3. reattach cleanly
            if (formation != null && ReferenceEquals(agent.Detachment, detachment))
            {
                formation.AttachUnit(agent);
            }


            RestoreFormationOrders(agent);
        }
    }


    [HarmonyPatch(typeof(HumanAIComponent), nameof(HumanAIComponent.RefreshBehaviorValues))]
    internal static class BLTHeroCombatBehaviorPatch
    {
        private static void Postfix(Agent ___Agent)
            => BLTHeroDetachmentBehavior.Current?.ApplyCombatBehavior(___Agent);
    }

    internal class HeroDetachment : IDetachment
    {
        public Formation ParentFormation { get; private set; }

        private readonly MBList<Formation> _userFormations = new();
        private readonly List<Agent> _agents = new();
        private Agent _followTarget;
        private bool _followMainAgent;
        private MovementOrder? _movementOrder;
        private bool _suppressMovement;

        public MBReadOnlyList<Formation> UserFormations => _userFormations;
        public bool IsLoose => true;

        public HeroDetachment(Formation parent)
        {
            ParentFormation = parent;
            if (parent != null)
                _movementOrder = parent.GetReadonlyMovementOrderReference();
        }

        public void FollowMainAgent()
        {
            _followTarget = null;
            _followMainAgent = true;
            _movementOrder = null;
        }

        public void FollowAgent(Agent target)
        {
            _followTarget = target;
            _followMainAgent = false;
            _movementOrder = MovementOrder.MovementOrderFollow(target);
        }

        public void ClearFollowTarget()
        {
            _followTarget = null;
            _followMainAgent = false;
            _movementOrder = null;
        }

        public void SuppressMovement(bool suppress)
        {
            _suppressMovement = suppress;
        }

        public void AddAgent(Agent agent, int slotIndex = -1,
            Agent.AIScriptedFrameFlags customFlags = Agent.AIScriptedFrameFlags.None)
        {
            if (agent == null || _agents.Contains(agent)) return;
            _agents.Add(agent);
        }

        public void AddAgentAtSlotIndex(Agent agent, int slotIndex)
        {
            if (agent == null || _agents.Contains(agent)) return;
            _agents.Add(agent);

            var formation = agent.Formation;
            if (formation != null && !agent.IsDetachedFromFormation)
            {
                int fileIndex = ((IFormationUnit)agent).FormationFileIndex;
                int rankIndex = ((IFormationUnit)agent).FormationRankIndex;

                // FormationFileIndex == -1 means the agent is in the unpositioned list,
                // not in the 2D grid. DetachUnit -> LineFormation.RemoveUnit will crash
                // trying to null out _units2D[fileIndex, rankIndex] with bad indices.
                // Only call DetachUnit when indices are valid (agent is in the 2D grid).
                if (fileIndex >= 0 && rankIndex >= 0)
                {
                    try { formation.DetachUnit(agent, IsLoose); }
                    catch (Exception e)
                    {
                        Log.Error($"BLTHeroDetachment: DetachUnit failed for {agent.Name}: {e}");
                    }
                }
                // If fileIndex == -1, agent is unpositioned — skip DetachUnit entirely.
                // The agent will still get Detachment set below so our behavior works,
                // and the formation won't crash trying to remove from an invalid grid position.
            }

            agent.Detachment = this;
            agent.SetDetachmentWeight(1f);
        }

        public void RemoveAgent(Agent agent)
        {
            if (agent == null || !_agents.Contains(agent)) return;
            _agents.Remove(agent);
            // Guard: only call scripted movement disables if agent is still valid
            try { agent.DisableScriptedMovement(); } catch { }
            try { agent.DisableScriptedCombatMovement(); } catch { }
        }

        public void ForgetAgent(Agent agent)
        {
            _agents.Remove(agent);
        }

        public void FormationStartUsing(Formation formation)
        {
            if (formation != null && !_userFormations.Contains(formation))
                _userFormations.Add(formation);
        }

        public void FormationStopUsing(Formation formation)
        {
            if (formation != null)
                _userFormations.Remove(formation);
        }

        public bool IsUsedByFormation(Formation formation)
            => formation != null && _userFormations.Contains(formation);

        public void OnFormationLeave(Formation formation)
        {
            if (formation == null) return;
            for (int i = _agents.Count - 1; i >= 0; i--)
            {
                var agent = _agents[i];
                if (agent == null || agent.Formation != formation) continue;

                RemoveAgent(agent);

                // Only reattach if agent is still alive and active
                if (agent.IsActive())
                {
                    try { formation.AttachUnit(agent); } catch { }
                }
            }
        }

        public WorldFrame? GetAgentFrame(Agent agent)
        {
            if (_suppressMovement || ParentFormation == null)
                return null;

            var target = _followMainAgent ? Mission.Current?.MainAgent : _followTarget;
            if ((_followMainAgent || _followTarget != null) && (target == null || !target.IsActive()))
                return null;

            var movementOrder = target != null
                ? MovementOrder.MovementOrderFollow(target)
                : _movementOrder;
            if (!movementOrder.HasValue)
                return null;

            var order = movementOrder.Value;
            var position = order.CreateNewOrderWorldPositionMT(
                ParentFormation,
                WorldPosition.WorldPositionEnforcedCache.NavMeshVec3);
            if (!position.IsValid)
                return null;

            var direction = target?.LookDirection
                            ?? new Vec3(ParentFormation.Direction.x, ParentFormation.Direction.y, 0f);
            var rotation = Mat3.CreateMat3WithForward(in direction);
            return new WorldFrame(rotation, position);
        }

        public bool IsAgentUsingOrInterested(Agent agent)
            => agent != null && _agents.Contains(agent);

        public bool IsAgentEligible(Agent agent)
            => agent != null && _agents.Contains(agent);

        public bool IsStandingPointAvailableForAgent(Agent agent) => false;
        public int GetNumberOfUsableSlots() => int.MaxValue;

        public Agent GetMovingAgentAtSlotIndex(int slotIndex)
            => slotIndex >= 0 && slotIndex < _agents.Count ? _agents[slotIndex] : null;

        public float GetDetachmentWeight(BattleSideEnum side) => float.MinValue;
        public float ComputeAndCacheDetachmentWeight(BattleSideEnum side) => float.MinValue;
        public float GetDetachmentWeightFromCache() => float.MinValue;
        public float? GetWeightOfNextSlot(BattleSideEnum side) => null;
        public float GetWeightOfOccupiedSlot(Agent agent) => float.MinValue;
        public float? GetWeightOfAgentAtOccupiedSlot(Agent detachedAgent, List<Agent> candidates, out Agent match)
        { match = null; return float.MaxValue; }
        public float? GetWeightOfAgentAtNextSlot(List<Agent> candidates, out Agent match)
        { match = null; return null; }
        public float? GetWeightOfAgentAtNextSlot(List<ValueTuple<Agent, float>> agentTemplateScores, out Agent match)
        { match = null; return null; }
        public float GetTemplateWeightOfAgent(Agent candidate) => float.MaxValue;
        public List<float> GetTemplateCostsOfAgent(Agent candidate, List<float> oldValue) => oldValue ?? new List<float>();
        public float GetExactCostOfAgentAtSlot(Agent candidate, int slotIndex) => float.MaxValue;

        public void GetSlotIndexWeightTuples(List<ValueTuple<int, float>> slotIndexWeightTuples) { }
        public bool IsSlotAtIndexAvailableForAgent(int slotIndex, Agent agent) => false;
        public void MarkSlotAtIndex(int slotIndex) { }
        public void UnmarkDetachment() { }
        public bool IsDetachmentRecentlyEvaluated() => true;
        public void ResetEvaluation() { }
        public bool IsEvaluated() => true;
        public void SetAsEvaluated() { }
    }
}
