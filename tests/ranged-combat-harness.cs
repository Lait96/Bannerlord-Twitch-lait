using System;
using System.Collections.Generic;

namespace CombatPolicyTests
{
    public enum MissionMode { Battle, Deployment }
    public enum EquipmentIndex { None = -1, WeaponItemBeginSlot, NumAllWeaponSlots = 5 }
    public class MovementOrder
    {
        public enum MovementOrderEnum { Charge, Advance, Stop, Retreat, FallBack }
        public MovementOrderEnum OrderEnum;
    }
    public class ArrangementOrder
    {
        public enum ArrangementOrderEnum { Line }
        public ArrangementOrderEnum OrderEnum;
    }
    public class FiringOrder { public enum RangedWeaponUsageOrderEnum { FireAtWill, HoldYourFire } }
    public class RidingOrder { public enum RidingOrderEnum { Mount, Dismount } }
    public class Formation
    {
        public MovementOrder Movement = new MovementOrder();
        public ArrangementOrder ArrangementOrder = new ArrangementOrder();
        public MovementOrder GetReadonlyMovementOrderReference() => Movement;
    }
    public class WeaponUsage { public bool IsRangedWeapon; }
    public class Weapon { public WeaponUsage CurrentUsageItem = new WeaponUsage(); }
    public class Equipment
    {
        public bool EmptyFirstStack;
        public bool HasAmmo(EquipmentIndex slot, out int usage, out bool loaded, out bool empty)
        {
            usage = 0; loaded = false; empty = EmptyFirstStack && (int)slot == 0;
            return (int)slot == 0 || (EmptyFirstStack && (int)slot == 1);
        }
    }
    public class Agent
    {
        public enum WeaponWieldActionType { WithAnimation }
        public bool Active = true, IsHuman = true, IsAIControlled = true, Ammo = true, Adopted = true;
        public bool Retreating;
        public object CurrentlyUsedGameObject, MountAgent, Detachment;
        public HumanAIComponent HumanAIComponent = new HumanAIComponent();
        public Formation Formation = new Formation();
        public Equipment Equipment = new Equipment();
        public Weapon WieldedWeapon = new Weapon();
        public int Firing, WieldRequests, LastWieldSlot = -1;
        public bool IsActive() => Active;
        public bool IsRetreating() => Retreating;
        public bool HasRangedWeapon(bool check) => Ammo;
        public object GetAdoptedHero() => Adopted ? this : null;
        public int GetFiringOrder() => Firing;
        public void TryToWieldWeaponInSlot(EquipmentIndex slot, WeaponWieldActionType type, bool spawn)
        {
            WieldRequests++;
            LastWieldSlot = (int)slot;
            WieldedWeapon.CurrentUsageItem.IsRangedWeapon = true;
        }
    }
    public class HumanAIComponent
    {
        public enum BehaviorValueSet { Default, Charge, DefaultDetached }
        public enum AISimpleBehaviorKind { Ranged, RangedHorseback, Melee, ChargeHorseback, GoToPos }
        public BehaviorValueSet ValueSet;
        public Action RefreshHook;
        public readonly Dictionary<AISimpleBehaviorKind, float[]> Weights = new Dictionary<AISimpleBehaviorKind, float[]>();
        public void SetBehaviorValueSet(BehaviorValueSet set) { ValueSet = set; Weights.Clear(); }
        public void RefreshBehaviorValues(MovementOrder.MovementOrderEnum movement, ArrangementOrder.ArrangementOrderEnum arrangement)
        {
            SetBehaviorValueSet(BehaviorValueSet.DefaultDetached);
            RefreshHook?.Invoke();
        }
        public void OverrideBehaviorParams(AISimpleBehaviorKind kind, float y1, float x2, float y2, float x3, float y3)
            => Weights[kind] = new[] { y1, x2, y2, x3, y3 };
        public bool IsInImportantCombatAction() => false;
    }
    public class Mission
    {
        public static Mission Current = new Mission();
        public MissionMode Mode = MissionMode.Battle;
        public bool IsDeploymentFinished = true, IsNavalBattle;
        public List<Agent> Agents = new List<Agent>();
    }
    public static class BannerlordApi { public static bool IsNavalRaidBattle(Mission mission) => false; }
    public static class BLTAdoptAHeroModule
    {
        public static Config CommonConfig = new Config();
        public class Config { public bool PrioritizeRangedAmmo = true; }
    }
    public class Policy
    {
        private enum DetachmentOrder { None, Charge, Hold, Follow, Navigate, Siege, Advance }
        private class DetachmentState
        {
            public object Detachment;
            public DetachmentOrder Order;
            public MovementOrder.MovementOrderEnum MovementBehaviorOrder = MovementOrder.MovementOrderEnum.Stop;
            public FiringOrder.RangedWeaponUsageOrderEnum? FiringOverride;
            public RidingOrder.RidingOrderEnum? RidingOverride;
        }
        private readonly Dictionary<Agent, DetachmentState> _detachments = new Dictionary<Agent, DetachmentState>();
        private readonly HashSet<Agent> _rangedPriorityAgents = new HashSet<Agent>();
        private readonly List<Agent> _rangedPriorityBuffer = new List<Agent>();
        public void Tick() => UpdateRangedPriority();
        public bool HasPriority(Agent agent) => _rangedPriorityAgents.Contains(agent);
        public void Order(Agent agent, string order, bool holdFire = false, bool mounting = false)
        {
            agent.Detachment = new object();
            _detachments[agent] = new DetachmentState
            {
                Detachment = agent.Detachment,
                Order = (DetachmentOrder)Enum.Parse(typeof(DetachmentOrder), order),
                FiringOverride = holdFire ? (FiringOrder.RangedWeaponUsageOrderEnum?)FiringOrder.RangedWeaponUsageOrderEnum.HoldYourFire : null,
                RidingOverride = mounting ? (RidingOrder.RidingOrderEnum?)RidingOrder.RidingOrderEnum.Mount : null
            };
        }
__POLICY__
    }
    public static class Tests
    {
        private static int count;
        private static void Check(bool pass, string name)
        {
            if (!pass) throw new Exception(name);
            count++;
        }
        private static Agent Add(Policy policy)
        {
            var agent = new Agent();
            Mission.Current.Agents.Add(agent);
            agent.HumanAIComponent.RefreshHook = () => policy.ApplyCombatBehavior(agent);
            return agent;
        }
        private static float Weight(Agent agent, HumanAIComponent.AISimpleBehaviorKind kind)
            => agent.HumanAIComponent.Weights[kind][0];
        public static string Run()
        {
            var policy = new Policy();
            var friendly = Add(policy);
            var enemy = Add(policy);
            var ordinary = Add(policy);
            ordinary.Adopted = false;
            policy.Tick();
            Check(policy.HasPriority(friendly) && policy.HasPriority(enemy), "All adopted agents included");
            Check(!policy.HasPriority(ordinary), "Ordinary troops excluded");
            Check(Weight(friendly, HumanAIComponent.AISimpleBehaviorKind.Ranged) > Weight(friendly, HumanAIComponent.AISimpleBehaviorKind.Melee), "Shooting outranks melee");
            Check(Weight(friendly, HumanAIComponent.AISimpleBehaviorKind.GoToPos) == 0, "Formation movement cannot beat shooting");
            Check(friendly.WieldRequests == 1, "Ranged weapon requested");
            policy.Tick();
            Check(friendly.WieldRequests == 1, "No repeated wield while ranged weapon held");
            var thrower = Add(policy);
            thrower.Equipment.EmptyFirstStack = true;
            policy.Tick();
            Check(thrower.LastWieldSlot == 1, "Empty throwable stack skipped despite compatible ammo in another slot");
            friendly.HumanAIComponent.RefreshBehaviorValues(MovementOrder.MovementOrderEnum.Charge, ArrangementOrder.ArrangementOrderEnum.Line);
            Check(Weight(friendly, HumanAIComponent.AISimpleBehaviorKind.Ranged) > 8, "Formation refresh reapplies priority");
            friendly.Ammo = false;
            policy.Tick();
            Check(!policy.HasPriority(friendly) && friendly.HumanAIComponent.Weights.Count == 0, "Depletion restores engine weights");
            friendly.Ammo = true;
            policy.Tick();
            Check(policy.HasPriority(friendly), "Refilled ammo reactivates priority");
            policy.Order(friendly, "Hold");
            policy.Tick();
            Check(policy.HasPriority(friendly), "Hold retains shooting");
            friendly.HumanAIComponent.RefreshBehaviorValues(MovementOrder.MovementOrderEnum.Stop, ArrangementOrder.ArrangementOrderEnum.Line);
            Check(!friendly.HumanAIComponent.Weights.ContainsKey(HumanAIComponent.AISimpleBehaviorKind.GoToPos), "Explicit positioning retained");
            policy.Order(friendly, "Hold", true);
            policy.Tick();
            Check(!policy.HasPriority(friendly), "Explicit hold fire respected");
            policy.Order(friendly, "Siege");
            policy.Tick();
            Check(!policy.HasPriority(friendly), "Siege operator excluded");
            policy.Order(friendly, "Follow", false, true);
            policy.Tick();
            Check(!policy.HasPriority(friendly), "Mounting excluded");
            policy.Order(friendly, "Charge");
            policy.Tick();
            Check(policy.HasPriority(friendly), "Charge retains priority");
            BLTAdoptAHeroModule.CommonConfig.PrioritizeRangedAmmo = false;
            policy.Tick();
            Check(!policy.HasPriority(friendly) && !policy.HasPriority(enemy), "Disable cleans all agents");
            Check(friendly.HumanAIComponent.ValueSet == HumanAIComponent.BehaviorValueSet.Charge && friendly.HumanAIComponent.Weights.Count == 0, "Charge fix independent of setting");
            policy.Order(friendly, "Advance");
            friendly.HumanAIComponent.RefreshBehaviorValues(MovementOrder.MovementOrderEnum.Advance, ArrangementOrder.ArrangementOrderEnum.Line);
            Check(friendly.HumanAIComponent.ValueSet == HumanAIComponent.BehaviorValueSet.Default, "Advance restores ranged capable native weights");
            friendly.Detachment = null;
            friendly.HumanAIComponent.RefreshBehaviorValues(MovementOrder.MovementOrderEnum.Stop, ArrangementOrder.ArrangementOrderEnum.Line);
            Check(friendly.HumanAIComponent.ValueSet == HumanAIComponent.BehaviorValueSet.DefaultDetached, "Stale control cannot overwrite engine profile");
            BLTAdoptAHeroModule.CommonConfig.PrioritizeRangedAmmo = true;
            friendly.IsAIControlled = false;
            policy.Tick();
            Check(!policy.HasPriority(friendly), "Player controlled excluded");
            friendly.IsAIControlled = true;
            friendly.Retreating = true;
            policy.Tick();
            Check(!policy.HasPriority(friendly), "Retreat respected");
            friendly.Retreating = false;
            friendly.CurrentlyUsedGameObject = new object();
            policy.Tick();
            Check(!policy.HasPriority(friendly), "Using game objects excluded");
            friendly.CurrentlyUsedGameObject = null;
            enemy.Formation.Movement.OrderEnum = MovementOrder.MovementOrderEnum.FallBack;
            policy.Tick();
            Check(!policy.HasPriority(enemy), "Formation fallback respected");
            enemy.Formation.Movement.OrderEnum = MovementOrder.MovementOrderEnum.Charge;
            enemy.Firing = (int)FiringOrder.RangedWeaponUsageOrderEnum.HoldYourFire;
            policy.Tick();
            Check(!policy.HasPriority(enemy), "Formation hold fire respected");
            enemy.Firing = 0;
            policy.Tick();
            Check(policy.HasPriority(enemy), "Fire at will reactivates priority");
            enemy.Active = false;
            policy.Tick();
            Check(!policy.HasPriority(enemy), "Inactive agent removed");
            enemy.Active = true;
            policy.Tick();
            Mission.Current.Mode = MissionMode.Deployment;
            policy.Tick();
            Check(!policy.HasPriority(enemy), "Deployment clears priority");
            Mission.Current.Mode = MissionMode.Battle;
            Mission.Current.IsNavalBattle = true;
            policy.Tick();
            Check(!policy.HasPriority(enemy), "Naval battle excluded");
            return count + " combat policy assertions passed (fake engine; native movement not tested)";
        }
    }
}
