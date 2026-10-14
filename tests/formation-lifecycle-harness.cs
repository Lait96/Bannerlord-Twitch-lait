using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FormationLifecycleTests
{
    public enum FormationClass { Infantry, Ranged, Cavalry, HorseArcher, NumberOfRegularFormations }
    public struct Vec2
    {
        public float x, y;
        public Vec2(float px, float py) { x = px; y = py; }
        public Vec2 Normalized() { float length = (float)Math.Sqrt(x * x + y * y); return length > 0 ? new Vec2(x / length, y / length) : new Vec2(); }
        public float DistanceSquared(Vec2 b) => (x - b.x) * (x - b.x) + (y - b.y) * (y - b.y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.x - b.x, a.y - b.y);
        public static Vec2 operator *(Vec2 a, float value) => new Vec2(a.x * value, a.y * value);
    }
    public struct Vec3
    {
        public float x, y, z;
        public Vec3(float px, float py) { x = px; y = py; z = 0; }
        public Vec2 AsVec2 => new Vec2(x, y);
        public float DistanceSquared(Vec3 b) => AsVec2.DistanceSquared(b.AsVec2);
    }
    public struct WorldPosition
    {
        public Vec2 AsVec2;
        public bool IsValid;
        public WorldPosition(Vec2 position) { AsVec2 = position; IsValid = true; }
        public void SetVec2(Vec2 value) => AsVec2 = value;
        public UIntPtr GetNavMesh() => Mission.Current.Navmesh(AsVec2) ? new UIntPtr(1) : UIntPtr.Zero;
    }
    public class MovementOrder
    {
        public enum MovementOrderEnum { Stop, Charge, Advance }
        public MovementOrderEnum OrderEnum;
    }
    public class FiringOrder
    {
        public enum RangedWeaponUsageOrderEnum { FireAtWill, HoldYourFire }
        public RangedWeaponUsageOrderEnum OrderEnum;
    }
    public class RidingOrder
    {
        public enum RidingOrderEnum { Free, Mount, Dismount }
        public RidingOrderEnum OrderEnum;
    }
    public class HumanAIComponent
    {
        public enum BehaviorValueSet { Default, Charge }
        public Action RefreshHook;
        public StandingPoint MovingPoint;
        public StandingPoint GetCurrentlyMovingGameObject() => MovingPoint;
        public BehaviorValueSet Profile;
        public void SetBehaviorValueSet(BehaviorValueSet value) => Profile = value;
        public void RefreshBehaviorValues(MovementOrder.MovementOrderEnum movement, ArrangementOrder.ArrangementOrderEnum arrangement)
        {
            Profile = movement == MovementOrder.MovementOrderEnum.Charge ? BehaviorValueSet.Charge : BehaviorValueSet.Default;
            RefreshHook?.Invoke();
        }
    }
    public class ArrangementOrder
    {
        public enum ArrangementOrderEnum { Line }
        public ArrangementOrderEnum OrderEnum;
    }
    public interface IFormationUnit { int FormationFileIndex { get; set; } int FormationRankIndex { get; set; } }
    public class Agent : IFormationUnit
    {
        public enum AIScriptedFrameFlags { None, NeverSlowDown, NoAttack }
        public enum AISpecialCombatModeFlags { None }
        public enum StopUsingGameObjectFlags { None }
        public string Name = "fixture";
        public bool Active = true, ThrowOnWeight;
        public bool Deleted;
        public bool IsHuman = true, HasAmmo = true, ScriptedMovement;
        public float MissileRange = 10f;
        public Vec3 Position;
        public Vec2 ScriptedDestination;
        public Team Team => Formation?.Team;
        public Formation Formation;
        public HeroDetachment Detachment;
        public object CurrentlyUsedGameObject;
        public object MountAgent;
        public bool IsMount;
        public Agent RiderAgent;
        public int Index;
        public bool MovingToPoint;
        public int MoveRequests, StopRequests;
        public HumanAIComponent HumanAIComponent = new HumanAIComponent();
        public int FormationFileIndex { get; set; }
        public int FormationRankIndex { get; set; }
        public bool IsDetachedFromFormation => Detachment != null;
        public bool IsActive() { if (Deleted) throw new Exception("Native access after deletion"); return Active; }
        public int TargetFormationIndex = -1, MountIndex = -1;
        public FiringOrder.RangedWeaponUsageOrderEnum Firing;
        public RidingOrder.RidingOrderEnum Riding;
        public void DisableScriptedMovement()
        {
            if (Deleted) throw new Exception("Native access after deletion");
            ScriptedMovement = false;
        }
        public bool HasRangedWeapon(bool check) => HasAmmo;
        public float GetMissileRangeWithHeightDifferenceAux(float z) => MissileRange;
        public WorldPosition GetWorldPosition() => new WorldPosition(Position.AsVec2);
        public void SetScriptedPosition(ref WorldPosition position, bool delay, AIScriptedFrameFlags flags)
        { ScriptedMovement = true; ScriptedDestination = position.AsVec2; }
        public void DisableScriptedCombatMovement() { }
        public AIScriptedFrameFlags GetScriptedFlags() => AIScriptedFrameFlags.None;
        public void SetScriptedFlags(AIScriptedFrameFlags value) { }
        public void SetScriptedCombatFlags(AISpecialCombatModeFlags value) { }
        public void SetAutomaticTargetSelection(bool value) { }
        public void SetTargetAgent(Agent value) { }
        public void SetTargetFormationIndex(int value) => TargetFormationIndex = value;
        public void SetSelectedMountIndex(int value) => MountIndex = value;
        public void SetFiringOrder(FiringOrder.RangedWeaponUsageOrderEnum value) => Firing = value;
        public void SetRidingOrder(RidingOrder.RidingOrderEnum value) => Riding = value;
        public void SetDetachmentWeight(float value)
        {
            if (ThrowOnWeight) { ThrowOnWeight = false; throw new Exception("Injected failure after detachment"); }
        }
        public void UpdateFormationOrders() { /* DLL method changes shield use, not firing or riding. */ }
        public void ForceAiBehaviorSelection() { }
        public void TryRemoveAllDetachmentScores() { }
        public void StopUsingGameObjectMT(bool success, StopUsingGameObjectFlags flags)
        {
            StopRequests++;
            if (CurrentlyUsedGameObject is StandingPoint used) used.UserAgent = null;
            if (HumanAIComponent.MovingPoint != null) HumanAIComponent.MovingPoint.MovingAgent = null;
            HumanAIComponent.MovingPoint = null;
            MovingToPoint = false;
            CurrentlyUsedGameObject = null;
        }
        public bool AIMoveToGameObjectIsEnabled() => MovingToPoint;
        public void AIMoveToGameObjectEnable(StandingPoint point, HeroDetachment detachment, AIScriptedFrameFlags flags)
        {
            MoveRequests++; MovingToPoint = true; HumanAIComponent.MovingPoint = point; point.MovingAgent = this;
        }
        public bool TryAttachToFormation()
        {
            if (Detachment == null) return false;
            Detachment.RemoveAgent(this);
            Formation.AttachUnit(this);
            return true;
        }
    }
    public class Arrangement
    {
        public float Width = 28f;
        public int RankCount = 1;
        public readonly List<IFormationUnit> Units = new List<IFormationUnit>();
        public IEnumerable<IFormationUnit> GetAllUnits() => Units;
        public void SwitchUnitLocations(IFormationUnit a, IFormationUnit b)
        {
            int file = a.FormationFileIndex, rank = a.FormationRankIndex;
            a.FormationFileIndex = b.FormationFileIndex; a.FormationRankIndex = b.FormationRankIndex;
            b.FormationFileIndex = file; b.FormationRankIndex = rank;
        }
    }
    public class Team
    {
        public Manager DetachmentManager = new Manager();
        public List<Formation> FormationsIncludingSpecialAndEmpty = new List<Formation>();
        public bool IsEnemyOf(Team other) => other != this;
    }
    public class StandingPoint
    {
        public bool IsDeactivated;
        public Agent UserAgent, MovingAgent, FavoredUser;
    }
    public class RangedSiegeWeapon { public StandingPoint PilotStandingPoint = new StandingPoint(); }
    public class Manager
    {
        public readonly HashSet<HeroDetachment> Registered = new HashSet<HeroDetachment>();
        public bool ContainsDetachment(HeroDetachment d) => Registered.Contains(d);
        public void DestroyDetachment(HeroDetachment d)
        {
            d.ParentFormation.LeaveDetachment(d);
            Registered.Remove(d);
        }
    }
    public class Formation
    {
        public Team Team = new Team();
        public FormationClass PhysicalClass;
        public int Index;
        public Vec2 CachedAveragePosition;
        public class Query
        {
            public bool IsInfantryFormationReadOnly = true;
            public bool IsRangedFormationReadOnly, IsCavalryFormationReadOnly, IsRangedCavalryFormationReadOnly;
        }
        public Query QuerySystem = new Query();
        public int CountOfUnits => Arrangement.Units.Count + DetachedUnits.Count;
        public Arrangement Arrangement = new Arrangement();
        public ArrangementOrder ArrangementOrder = new ArrangementOrder();
        public FiringOrder FiringOrder = new FiringOrder();
        public RidingOrder RidingOrder = new RidingOrder();
        public MovementOrder Movement = new MovementOrder();
        public bool ThrowOnDetach;
        public int AttachCount;
        public readonly HashSet<Agent> DetachedUnits = new HashSet<Agent>();
        public readonly HashSet<HeroDetachment> Joined = new HashSet<HeroDetachment>();
        public MovementOrder GetReadonlyMovementOrderReference() => Movement;
        public void JoinDetachment(HeroDetachment d) { Joined.Add(d); Team.DetachmentManager.Registered.Add(d); }
        public void LeaveDetachment(HeroDetachment d) { d.OnFormationLeave(this); Joined.Remove(d); }
        public void DetachUnit(Agent a, bool loose)
        {
            if (ThrowOnDetach) throw new Exception("Injected failure during detachment");
            if (!Arrangement.Units.Remove(a)) throw new Exception("Double detach");
            DetachedUnits.Add(a); a.FormationFileIndex = -1; a.FormationRankIndex = -1;
        }
        public void AttachUnit(Agent a)
        {
            if (Arrangement.Units.Contains(a)) throw new Exception("Double attach");
            DetachedUnits.Remove(a); Arrangement.Units.Add(a); a.Detachment = null;
            a.FormationFileIndex = Arrangement.Units.Count - 1; a.FormationRankIndex = 0; AttachCount++;
            a.HumanAIComponent.RefreshBehaviorValues(Movement.OrderEnum, ArrangementOrder.OrderEnum);
        }
    }
    public static class Mission
    {
        public static Clock Current = new Clock();
        public class Clock
        {
            public float CurrentTime = 10f;
            public List<Agent> Agents = new List<Agent>();
            public List<Team> Teams = new List<Team>();
            public bool IsDeploymentFinished = true;
            public Func<Vec2, bool> Navmesh = position => true;
            public WorldPosition Alternative;
            public int AlternativeRequests;
            public bool IsPositionInsideBoundaries(Vec2 position) => position.x >= -100f && position.x <= 100f;
            public WorldPosition GetAlternatePositionForNavmeshlessOrOutOfBoundsPosition(Vec2 direction, WorldPosition original, ref float penalty)
            { AlternativeRequests++; return Alternative; }
        }
    }
    public static class Log { public static void Error(string value) { } }
    public static class Localization { public static string Translate(this string value, params object[] args) => value; }
    public class HeroDetachment
    {
        public Formation ParentFormation { get; private set; }
        private readonly List<Agent> _agents = new List<Agent>();
        public bool IsLoose => true;
        public void SuppressMovement(bool value) { }
        public HeroDetachment(Formation parent) => ParentFormation = parent;
__DETACHMENT__
    }
    public class Behavior
    {
        public enum FormationSidePreference { Default, Left, Center, Right }
        public enum FormationDepthPreference { Default, Front, Back }
        private class ControlPreferences
        {
            public FormationSidePreference Side;
            public FormationDepthPreference Depth;
            public float LastFormationPositionUpdate;
            public HashSet<FormationClass> IgnoredClasses = new HashSet<FormationClass>();
            public Agent HunterTarget = null;
        }
        private class DetachmentState
        {
            public DetachmentOrder Order = DetachmentOrder.None;
            public FiringOrder.RangedWeaponUsageOrderEnum? FiringOverride;
            public RidingOrder.RidingOrderEnum? RidingOverride;
            public int SelectedMountIndex = -1;
            public HeroDetachment Detachment;
            public MovementOrder.MovementOrderEnum MovementBehaviorOrder;
            public RangedSiegeWeapon SiegeWeapon;
            public float LastNavigationReissueTime = -1f;
            public FormationClass? FocusClass = null;
        }
        private enum DetachmentOrder { None, Siege }
        private readonly Dictionary<Agent, DetachmentState> _detachments = new Dictionary<Agent, DetachmentState>();
        private readonly Dictionary<Agent, ControlPreferences> _preferences = new Dictionary<Agent, ControlPreferences>();
        private readonly Dictionary<StandingPoint, Agent> _siegeReservations = new Dictionary<StandingPoint, Agent>();
        private readonly HashSet<Agent> _rangedPriorityAgents = new HashSet<Agent>();
        private ControlPreferences GetPreferences(Agent a)
        {
            if (!_preferences.TryGetValue(a, out var p)) _preferences[a] = p = new ControlPreferences();
            return p;
        }
        private void ApplyTargetPreferences(Agent a) { }
        private void ApplyFocus(Agent a, DetachmentState state) { }
        public bool HasRecord(Agent a) => _detachments.ContainsKey(a);
        public void RemoveDead(Agent a) => CleanupDetachmentOnDeath(a, _detachments[a]);
        public void Layout(Agent a, FormationSidePreference side, FormationDepthPreference depth)
            => ApplyFormationPositionPreference(a, new ControlPreferences { Side = side, Depth = depth });
        public StandingPoint AssignSiege(Agent a)
        {
            var state = _detachments[a]; state.SiegeWeapon = new RangedSiegeWeapon();
            var point = state.SiegeWeapon.PilotStandingPoint; point.FavoredUser = a; _siegeReservations[point] = a;
            BeginUsingSiegeWeapon(a, state); return point;
        }
        public void SiegeTick(Agent a) => BeginUsingSiegeWeapon(a, _detachments[a]);
        public int ReservationCount => _siegeReservations.Count;
        public void Advance(Agent a) => ApplyAdvance(a, new DetachmentState());
__BEHAVIOR__
    }
    public class Commands
    {
        public class Settings { public bool Detach = true, Filter = true; }
        private bool MatchesCommand(string value, string translated, string original)
            => value.Equals(original, StringComparison.OrdinalIgnoreCase) || value.Equals(translated, StringComparison.OrdinalIgnoreCase);
        private string BuildCompact(Formation f) => "fixture";
        private string GetFormationClassDisplayName(FormationClass value) => value.ToString();
        private void TransferHeroToFormation(Agent a, Formation target) => a.Formation = target;
        public string Advanced(Behavior b, Agent a, string command, string argument)
        {
            string failure = null;
            ExecuteAdvancedCommand(a, command, new[] { command, argument }, new Settings(), b, text => { }, text => failure = text);
            return failure;
        }
        public string Numeric(Behavior behavior, Agent agent, string num, bool filter)
        {
            string failure = null;
            NumericInternal(behavior, agent, num, new Settings { Filter = filter }, text => { }, text => failure = text);
            return failure;
        }
        private void NumericInternal(Behavior behavior, Agent agent, string num, Settings settings, Action<string> onSuccess, Action<string> onFailure)
        {
            var currentFormation = agent.Formation;
            var formType = currentFormation.PhysicalClass;
__NUMERIC__
        }
        public static string Reset(Behavior b, Agent a) => ResetHeroControl(b, a);
__COMMANDS__
    }
    public static class Tests
    {
        private static int count;
        private static void Check(bool pass, string name) { if (!pass) throw new Exception(name); count++; }
        private static Agent AgentIn(Formation f)
        {
            var a = new Agent { Formation = f, FormationFileIndex = f.Arrangement.Units.Count };
            f.Arrangement.Units.Add(a);
            if (!f.Team.FormationsIncludingSpecialAndEmpty.Contains(f)) f.Team.FormationsIncludingSpecialAndEmpty.Add(f);
            return a;
        }
        public static string Run()
        {
            var b = new Behavior(); var f = new Formation(); var a = AgentIn(f);
            Check(b.Detach(a) == null && b.IsDetached(a), "Detach succeeds");
            Check(f.Arrangement.Units.Count == 0 && f.DetachedUnits.Count == 1, "Exactly one unit detached");
            a.Firing = FiringOrder.RangedWeaponUsageOrderEnum.HoldYourFire; a.Riding = RidingOrder.RidingOrderEnum.Dismount; a.MountIndex = 77;
            a.HumanAIComponent.RefreshHook = () => { if (b.HasRecord(a)) a.HumanAIComponent.Profile = HumanAIComponent.BehaviorValueSet.Charge; };
            Check(b.Attach(a) == null, "Attach succeeds");
            Check(a.Firing == f.FiringOrder.OrderEnum && a.Riding == f.RidingOrder.OrderEnum && a.MountIndex == -1, "Native orders restored");
            Check(a.HumanAIComponent.Profile == HumanAIComponent.BehaviorValueSet.Default, "Old control cannot override AttachUnit refresh");
            Check(f.AttachCount == 1 && f.Arrangement.Units.Count == 1, "No duplicate attachment");
            Check(f.Team.DetachmentManager.Registered.Count == 0 && f.Joined.Count == 0, "Manager registration destroyed");
            for (int i = 0; i < 5; i++) { b.Detach(a); b.Attach(a); }
            Check(f.Team.DetachmentManager.Registered.Count == 0, "Repeated orders do not leak detachments");
            b.Detach(a); f.LeaveDetachment(a.Detachment);
            Check(!b.IsDetached(a) && !b.HasRecord(a), "External reattachment reconciled");
            Check(f.Team.DetachmentManager.Registered.Count == 0, "External leave registration destroyed");
            b.Detach(a); a.Detachment.RemoveAgent(a); f.DetachedUnits.Remove(a); a.Detachment = null;
            var next = new Formation(); a.Formation = next; next.Arrangement.Units.Add(a);
            Check(!b.IsDetached(a) && f.Team.DetachmentManager.Registered.Count == 0, "Formation transfer reconciled using original manager");
            var foreignFormation = new Formation(); var foreignAgent = AgentIn(foreignFormation); b.Detach(foreignAgent);
            var foreignControl = new HeroDetachment(foreignFormation); foreignAgent.Detachment = foreignControl; foreignAgent.ScriptedMovement = true;
            Check(!b.IsDetached(foreignAgent) && foreignAgent.Detachment == foreignControl && foreignAgent.ScriptedMovement, "External controller retains its scripted movement");
            Check(Commands.Reset(b, foreignAgent) == null && !foreignAgent.IsDetachedFromFormation && !foreignAgent.ScriptedMovement, "Explicit return releases external detachment through native API");
            var failed = new Formation { ThrowOnDetach = true }; var failureAgent = AgentIn(failed);
            Check(b.Detach(failureAgent) != null, "Native failure returned as error");
            Check(failed.Team.DetachmentManager.Registered.Count == 0 && failed.Arrangement.Units.Count == 1 && !b.HasRecord(failureAgent), "Early failure rolls back registration");
            var late = new Formation(); var lateAgent = AgentIn(late); lateAgent.ThrowOnWeight = true;
            Check(b.Detach(lateAgent) != null, "Late failure returned as error");
            Check(late.Team.DetachmentManager.Registered.Count == 0 && late.Arrangement.Units.Count == 1 && lateAgent.Detachment == null, "Late failure restores unit");
            var dead = new Formation(); var deadAgent = AgentIn(dead); b.Detach(deadAgent); deadAgent.Active = false; b.RemoveDead(deadAgent);
            Check(!b.HasRecord(deadAgent) && dead.Team.DetachmentManager.Registered.Count == 0 && dead.AttachCount == 0, "Dead agent cleanup never reattaches");
            var layout = new Formation(); var group = Enumerable.Range(0, 10).Select(i => AgentIn(layout)).ToArray();
            b.Layout(group[4], Behavior.FormationSidePreference.Right, Behavior.FormationDepthPreference.Default);
            Check(group[4].FormationFileIndex >= 6 && group[4].FormationFileIndex <= 9, "Column indices used despite width 28 metres");
            Check(group.Select(x => x.FormationFileIndex).Distinct().Count() == 10, "Hero-only formation layout preserves slots");
            a.TargetFormationIndex = 99; b.ClearIgnoredClasses(a);
            Check(a.TargetFormationIndex == -1, "Ignore reset clears native formation target");
            var siege = new Formation(); var operatorAgent = AgentIn(siege); b.Detach(operatorAgent);
            var point = b.AssignSiege(operatorAgent);
            Check(operatorAgent.MoveRequests == 1 && operatorAgent.CurrentlyUsedGameObject == null && point.UserAgent == null, "Approach requested before occupying siege point");
            b.SiegeTick(operatorAgent); b.SiegeTick(operatorAgent);
            Check(operatorAgent.MoveRequests == 1, "Approach not restarted each tick");
            b.Attach(operatorAgent);
            Check(!operatorAgent.MovingToPoint && point.MovingAgent == null && point.FavoredUser == null && b.ReservationCount == 0, "Return cancels approach and reservation");
            b.Detach(operatorAgent); operatorAgent.MountAgent = new object(); point = b.AssignSiege(operatorAgent);
            Check(operatorAgent.MoveRequests == 1, "Mounted operator waits for dismount");
            operatorAgent.MountAgent = null; b.SiegeTick(operatorAgent);
            Check(operatorAgent.MoveRequests == 2, "Approach begins after dismount");
            operatorAgent.MovingToPoint = false; operatorAgent.HumanAIComponent.MovingPoint = null;
            point.MovingAgent = null; point.UserAgent = operatorAgent; operatorAgent.CurrentlyUsedGameObject = point;
            b.SiegeTick(operatorAgent);
            Check(operatorAgent.MoveRequests == 2, "Occupied point does not restart approach");
            b.Attach(operatorAgent);
            Check(point.UserAgent == null && operatorAgent.CurrentlyUsedGameObject == null && b.ReservationCount == 0, "Return releases active siege use");
            var deletedFormation = new Formation(); var deletedAgent = AgentIn(deletedFormation); b.Detach(deletedAgent);
            var deletedPoint = b.AssignSiege(deletedAgent); deletedAgent.Deleted = true;
            b.OnAgentDeleted(deletedAgent);
            Check(!b.HasRecord(deletedAgent) && deletedFormation.Team.DetachmentManager.Registered.Count == 0 && deletedPoint.FavoredUser == null, "Deleted agent cleanup avoids native access");
            var archer = AgentIn(new Formation()); var enemy = AgentIn(new Formation()); enemy.Position = new Vec3(30f, 0f);
            Mission.Current.Agents.Add(archer); Mission.Current.Agents.Add(enemy);
            b.Advance(archer);
            Check(archer.ScriptedMovement && archer.ScriptedDestination.x == 20f, "Advance approaches native missile range");
            Mission.Current.Navmesh = position => position.x <= 10f;
            Mission.Current.Alternative = new WorldPosition(new Vec2(5f, 0f)); b.Advance(archer);
            Check(Mission.Current.AlternativeRequests == 1 && archer.ScriptedDestination.x == 5f, "Native alternative used for navmeshless destination");
            Mission.Current.Alternative = new WorldPosition(); b.Advance(archer);
            Check(!archer.ScriptedMovement, "Invalid alternative never issued as movement");
            Mission.Current.Navmesh = position => true; archer.HasAmmo = false; b.Advance(archer);
            Check(archer.ScriptedDestination.x == 30f, "Depleted ammunition switches advance to melee distance");
            Mission.Current.Agents.Clear(); b.Advance(archer);
            Check(!archer.ScriptedMovement, "Missing target cancels scripted approach");
            var commands = new Commands(); var cf = new Formation(); var ca = AgentIn(cf);
            Check(commands.Advanced(b, ca, "mount", "on") != null && !b.HasRecord(ca), "Missing mount does not detach hero");
            ca.MountAgent = new object();
            Check(commands.Advanced(b, ca, "mount", "on") != null && !b.HasRecord(ca), "Already mounted rejection preserves control");
            ca.MountAgent = null;
            Check(commands.Advanced(b, ca, "focus", "infantry") != null && !b.HasRecord(ca), "Missing focus target does not detach hero");
            Check(commands.Advanced(b, ca, "fire", "invalid") != null && !b.HasRecord(ca), "Invalid fire argument preserves control");
            Check(commands.Advanced(b, ca, "fire", "hold") == null && b.IsDetached(ca) && ca.Firing == FiringOrder.RangedWeaponUsageOrderEnum.HoldYourFire, "Valid fire command still works");
            foreach (bool filter in new[] { true, false })
            {
                Check(commands.Numeric(b, ca, "999", filter) != null && b.HasRecord(ca) && ca.Firing == FiringOrder.RangedWeaponUsageOrderEnum.HoldYourFire, "Invalid formation number retains individual control");
            }
            Check(Commands.Reset(b, ca) == null && !b.HasRecord(ca) && ca.Firing == cf.FiringOrder.OrderEnum, "Return command restores actual firing order");
            return count + " production lifecycle/layout assertions passed (fake engine contracts)";
        }
    }
}
