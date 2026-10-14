using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Actions;
using JetBrains.Annotations;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace BLTAdoptAHero
{
    internal sealed class BLTFormationTestBehavior : AutoMissionBehavior<BLTFormationTestBehavior>
    {
        private readonly List<string> report = new();
        private Agent subject;
        private string mode;
        private float elapsed;
        private float duration;
        private int step;
        private int shots;
        private int initialAmmo;
        private int meleeWithAmmo;
        private float nextSample;
        private bool reachedSiege;
        private RangedSiegeWeapon siegeWeapon;
        private int holdFireShots;
        private HeroDetachment ownedDetachment;
        private Formation originalFormation;
        private string reportPath;
        private bool Running => subject != null;
        private BLTHeroDetachmentBehavior Controls => BLTHeroDetachmentBehavior.Current;

        [UsedImplicitly, CommandLineFunctionality.CommandLineArgumentFunction("formation_test", "blt")]
        public static string ConsoleCommand(List<string> args)
        {
            var runner = Current;
            if (runner == null) return "No active mission";
            try { return runner.Handle(args); }
            catch (Exception e)
            {
                runner.Record("FAIL console: " + e.Message);
                runner.Finish("Interrupted by exception");
                return runner.Status();
            }
        }

        private string Handle(List<string> args)
        {
            if (args.Count == 0) return "blt.formation_test list | quick/ranged/advance/siege/watch <agentIndex> [10..180 seconds] | order <agentIndex> <formation args> | status | stop";
            string action = args[0].ToLowerInvariant();
            if (action == "list") return string.Join("\n", Mission.Agents.Where(a => a.IsActive() && a.IsAdopted())
                .Select(a => $"{a.Index}: {a.Name}, team={a.Team?.TeamIndex}, AI={a.IsAIControlled}, ammo={Ammo(a)}, mounted={a.MountAgent != null}"));
            if (action == "status") return Status();
            if (action == "stop") { Finish("Stopped by user; observation incomplete"); return Status(); }
            if (Running) return "Test running; use status or stop first";
            if (action != "quick" && action != "ranged" && action != "advance" && action != "siege" && action != "watch" && action != "order") return "Unknown test; use blt.formation_test";
            if (args.Count < 2 || !int.TryParse(args[1], out int index)) return "Expected agentIndex from list";
            var agent = Mission.Agents.FirstOrDefault(a => a.Index == index && a.IsActive() && a.IsAdopted());
            if (agent == null || !agent.IsAIControlled || agent.Team == null || agent.Formation == null) return "Select an active adopted AI hero with a formation";
            if (agent.GetHero().GetAgent() != agent) return "Ambiguous hero: multiple agents share this hero";
            if (action == "order")
            {
                if (args.Count < 3) return "Expected formation command arguments";
                bool ok = new FormationCommand().ExecuteDiagnostic(agent, string.Join(" ", args.Skip(2)), out string reply);
                return (ok ? "OK: " : "ERROR: ") + reply;
            }
            float seconds = 30;
            if (args.Count > 3 || (args.Count == 3 && (!int.TryParse(args[2], out int parsed) || parsed < 10 || parsed > 180))) return "Duration must be an integer between 10 and 180";
            if (args.Count == 3) seconds = int.Parse(args[2]);
            var state = Controls.GetControlStatus(agent);
            if (action != "watch" && (agent.IsDetachedFromFormation || state.IsDetached || state.Hunter || state.IgnoredClasses.Count != 0
                || state.Side != BLTHeroDetachmentBehavior.FormationSidePreference.Default
                || state.Depth != BLTHeroDetachmentBehavior.FormationDepthPreference.Default)) return "Use order <index> return first; test requires default formation control";
            if ((action == "ranged" || action == "advance") && (Ammo(agent) == 0 || !agent.HasRangedWeapon(true))) return "SKIP: no usable ranged weapon/ammunition";
            if ((action == "ranged" || action == "advance") && !BLTAdoptAHeroModule.CommonConfig.PrioritizeRangedAmmo) return "Enable Battle/Prioritize ranged ammunition first; watch works with either setting";
            report.Clear();
            reportPath = null;
            subject = agent;
            mode = action;
            originalFormation = agent.Formation;
            elapsed = nextSample = 0;
            duration = seconds;
            step = shots = meleeWithAmmo = 0;
            initialAmmo = Ammo(agent);
            reachedSiege = false;
            siegeWeapon = null;
            ownedDetachment = null;
            Record($"START {DateTime.UtcNow:O} mode={mode}, agent={agent.Index}, team={agent.Team.TeamIndex}, ammo={initialAmmo}, priority={BLTAdoptAHeroModule.CommonConfig.PrioritizeRangedAmmo}");
            Record($"VERSION {GameVersion.GameVersionString}; expected branch v1.4.* compatible={GameVersion.IsVersion("v1.4.*")}");
            if (mode != "quick" && mode != "watch")
            {
                string command = mode == "siege" ? "siege enter" : mode == "advance" ? "advance" : "charge";
                if (!Run(command)) { Finish("SKIP: command prerequisites unavailable; see response"); return Status(); }
                Controls.TryGetDetachment(subject, out ownedDetachment);
                siegeWeapon = Controls.GetControlStatus(subject).SiegeWeapon;
            }
            return "Started. Close console and let the battle run; then blt.formation_test status. stop returns hero to formation. Settings are unchanged.";
        }

        private bool Run(string command)
        {
            bool ok = new FormationCommand().ExecuteDiagnostic(subject, command, out string reply);
            Record($"COMMAND {command}: {(ok ? "OK" : "ERROR")} {reply}");
            return ok;
        }

        private void Check(bool condition, string name) => Record((condition ? "PASS " : "FAIL ") + name);

        public override void OnMissionTick(float dt)
        {
            if (!Running) return;
            try
            {
                if (!subject.IsActive()) { Finish("INCONCLUSIVE: hero removed from combat"); return; }
                elapsed += dt;
                if (mode == "quick")
                {
                    if (elapsed >= step + 1) QuickStep();
                    return;
                }
                if (elapsed >= nextSample)
                {
                    nextSample = elapsed + 1;
                    int ammo = Ammo(subject);
                    string animation = subject.GetCurrentActionType(1).ToString();
                    if (ammo > 0 && animation == "ReleaseMelee") meleeWithAmmo++;
                    if (siegeWeapon != null && siegeWeapon.StandingPoints.Any(p => p.UserAgent == subject)) reachedSiege = true;
                    var nearest = Mission.Agents.Where(a => a.IsActive() && a.IsHuman && a.Team != null && subject.Team.IsEnemyOf(a.Team))
                        .OrderBy(a => (a.Position - subject.Position).LengthSquared).FirstOrDefault();
                    Record($"SAMPLE t={elapsed:F1}, ammo={ammo}, shots={shots}, action={animation}, order={Controls.GetControlStatus(subject).PrimaryOrder}, using={subject.IsUsingGameObject}, approaching={subject.AIMoveToGameObjectIsEnabled()}, enemyDistance={(nearest == null ? "none" : (nearest.Position - subject.Position).Length.ToString("F1"))}");
                }
                if (elapsed < duration) return;
                if (mode == "siege")
                {
                    Record(reachedSiege ? "PASS: native game object use observed; inspect samples for approach" : "INCONCLUSIVE: no game object use; inspect route/availability in game");
                    Run("siege leave");
                    Check(!subject.IsUsingGameObject && !subject.AIMoveToGameObjectIsEnabled(), "siege leave cancels use and approach");
                }
                else
                {
                    Record(shots > 0 ? $"PASS: {shots} actual missile launches observed" : "INCONCLUSIVE: no missile launch; inspect target, range, line of sight and movement");
                    Record($"OBSERVATION ammo={initialAmmo}->{Ammo(subject)}, melee-with-ammo samples={meleeWithAmmo}; this alone does not establish a tactical AI defect or complete ammunition expenditure");
                }
                Finish("Observation complete");
            }
            catch (Exception e) { Record("FAIL tick: " + e); Finish("Interrupted by exception"); }
        }

        private void QuickStep()
        {
            switch (step++)
            {
                case 0:
                    Check(!Run("999") && !subject.IsDetachedFromFormation, "invalid formation preserves attachment");
                    Check(Run("hold") && Controls.IsDetached(subject), "hold detaches through command parser");
                    Controls.TryGetDetachment(subject, out ownedDetachment);
                    break;
                case 1:
                    Check(Run("fire hold") && subject.GetFiringOrder() == (int)FiringOrder.RangedWeaponUsageOrderEnum.HoldYourFire, "native hold fire");
                    holdFireShots = shots;
                    break;
                case 2:
                    Check(shots == holdFireShots, "no missile launch during hold fire interval");
                    Check(Run("return") && !subject.IsDetachedFromFormation && !Controls.IsDetached(subject), "return reattaches");
                    Check(subject.Formation == originalFormation, "return preserves formation");
                    Check(subject.GetFiringOrder() == (int)subject.Formation.FiringOrder.OrderEnum, "return restores native firing order");
                    Check(subject.GetRidingOrder() == (int)subject.Formation.RidingOrder.OrderEnum, "return restores native riding order");
                    Check(ownedDetachment != null && !subject.Team.DetachmentManager.ContainsDetachment(ownedDetachment), "return removes manager registration");
                    break;
                case 3:
                    Check(Run("hold") && Controls.IsDetached(subject), "repeated hold detaches");
                    Controls.TryGetDetachment(subject, out ownedDetachment);
                    break;
                default:
                    Finish("Quick checks complete");
                    break;
            }
        }

        public override void OnAgentShootMissile(Agent shooterAgent, EquipmentIndex weaponIndex,
            Vec3 position, Vec3 velocity, Mat3 orientation, bool hasRigidBody, int forcedMissileIndex)
        {
            if (Running && shooterAgent == subject) shots++;
        }

        public override void OnAgentDeleted(Agent affectedAgent)
        {
            if (affectedAgent != subject) return;
            // The deleted actor's native pointer is already invalid.
            subject = null;
            Record("INCONCLUSIVE: actor deleted; native cleanup belongs to detachment behavior");
            SaveReport();
        }

        protected override void OnEndMission() => Finish("Mission ended; observation incomplete");
        public override void OnRemoveBehavior() => Finish("Behavior removed; observation incomplete");

        private void Finish(string reason)
        {
            if (!Running) return;
            try
            {
                if (mode != "watch" && subject.IsActive())
                {
                    Check(Run("return") && !subject.IsDetachedFromFormation, "cleanup returns hero to formation");
                    if (ownedDetachment != null) Check(!originalFormation.Team.DetachmentManager.ContainsDetachment(ownedDetachment), "cleanup removes manager registration");
                }
            }
            catch (Exception e) { Record("FAIL cleanup: " + e.Message); }
            finally { subject = null; Record(reason); SaveReport(); }
        }

        private void Record(string message) => report.Add(message);
        private string Status() => $"{(Running ? $"RUNNING {mode} {elapsed:F1}/{duration:F1}s" : "IDLE")}\n{string.Join("\n", report.Skip(Math.Max(0, report.Count - 16)))}\nReport: {reportPath ?? "available after completion"}";

        private void SaveReport()
        {
            try
            {
                string folder = Path.Combine(Path.GetTempPath(), "BLTFormationTests");
                Directory.CreateDirectory(folder);
                reportPath = Path.Combine(folder, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + ".txt");
                File.WriteAllLines(reportPath, report);
            }
            catch (Exception e) { Record("Report write failed: " + e.Message); Log.Exception("Formation test report", e); }
        }

        private static int Ammo(Agent agent)
        {
            int total = 0;
            for (var slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumAllWeaponSlots; slot++)
            {
                var weapon = agent.Equipment[slot];
                if (weapon.IsEmpty) continue;
                if (weapon.IsAnyConsumable()) total += weapon.Amount;
                else if (weapon.GetRangedUsageIndex() >= 0) total += weapon.Ammo;
            }
            return total;
        }
    }
}
