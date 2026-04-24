using System.Collections.Generic;
using System.Linq;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace BLTAdoptAHero.Behaviors
{
    internal class BanditWaveBehavior : AutoMissionBehavior<BanditWaveBehavior>
    {
        private readonly HashSet<Agent> _spawnedWaveAgents = new();

        private bool _initialized;
        private bool _spawnScheduled;
        private float _nextAllowedSpawnAt;
        private PartyBase _enemyParty;

        public override void OnMissionTick(float dt)
        {
            SafeCall(() =>
            {
                var state = BanditWaveCampaignBehavior.Current?.State;
                if (state == null || !state.IsEnabled)
                    return;

                if (Mission.Current == null || !Mission.Current.IsLoadingFinished)
                    return;

                if (Mission.Current.CurrentState != Mission.State.Continuing)
                    return;

                if (Mission.Current.IsMissionEnding || Mission.Current.MissionResult?.BattleResolved == true)
                    return;

                if (!_initialized)
                {
                    TryInitialize();
                }

                if (!_initialized)
                    return;

                KeepMainHeroSafe(state);
                PreventEnemyRouting();

                if (state.StopRequested)
                    return;

                if (_spawnScheduled)
                    return;

                if (CampaignHelpers.GetApplicationTime() < _nextAllowedSpawnAt)
                    return;

                int aliveEnemies = CountAliveWaveEnemies();
                if (aliveEnemies <= state.RespawnWhenEnemiesLeftAtOrBelow)
                {
                    ScheduleNextWave();
                }
            });
        }

        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
        {
            SafeCall(() =>
            {
                if (affectedAgent != null)
                {
                    _spawnedWaveAgents.Remove(affectedAgent);
                }
            });
        }

        protected override void OnEndMission()
        {
            SafeCall(() =>
            {
                BanditWaveCampaignBehavior.Current?.ResetAfterMission();
                _spawnedWaveAgents.Clear();
                _initialized = false;
                _spawnScheduled = false;
                _nextAllowedSpawnAt = 0f;
                _enemyParty = null;
            });
        }

        private void TryInitialize()
        {
            if (Mission.Current?.PlayerEnemyTeam == null)
                return;

            _enemyParty = ResolveEnemyParty();
            if (_enemyParty == null)
            {
                Log.Trace("[BanditWave] Failed to resolve enemy party.");
                return;
            }

            _initialized = true;
            Log.Trace("[BanditWave] Initialized.");
        }

        private void ScheduleNextWave()
        {
            if (BLTSummonBehavior.Current == null)
            {
                Log.Trace("[BanditWave] BLTSummonBehavior.Current is null.");
                return;
            }

            _spawnScheduled = true;

            BLTSummonBehavior.Current.DoNextTick(() =>
            {
                try
                {
                    SpawnNextWave();
                }
                finally
                {
                    _spawnScheduled = false;

                    var state = BanditWaveCampaignBehavior.Current?.State;
                    if (state != null)
                    {
                        _nextAllowedSpawnAt = CampaignHelpers.GetApplicationTime() + state.DelayBetweenWavesSeconds;
                    }
                }
            });
        }

        private void SpawnNextWave()
        {
            var state = BanditWaveCampaignBehavior.Current?.State;
            if (state == null || state.Waves == null || state.Waves.Count == 0)
            {
                Log.Trace("[BanditWave] No wave entries configured.");
                return;
            }

            if (_enemyParty == null)
            {
                Log.Trace("[BanditWave] Enemy party is null.");
                return;
            }

            state.CurrentWave++;

            foreach (var entry in state.Waves.Where(IsValidWaveEntry))
            {
                int count = MBRandom.RandomInt(entry.MinCount, entry.MaxCount + 1);
                if (count <= 0)
                    continue;

                Log.Trace($"[BanditWave] Spawning family: {entry.TroopId}, count={count}");

                for (int i = 0; i < count; i++)
                {
                    CharacterObject troop = ResolveRandomBanditTroop(entry.TroopId);
                    if (troop == null)
                    {
                        Log.Trace($"[BanditWave] Could not resolve troop for family: {entry.TroopId}");
                        continue;
                    }

                    bool spawnWithHorse = troop.IsMounted;

                    Agent agent = BLTSummonBehavior.SpawnAgent(
                        onPlayerSide: false,
                        troop: troop,
                        party: _enemyParty,
                        spawnWithHorse: spawnWithHorse,
                        isReinforcement: true,
                        isAlarmed: true
                    );

                    if (agent != null)
                    {
                        _spawnedWaveAgents.Add(agent);
                    }
                }
            }
        }

        private static bool IsValidWaveEntry(BanditWaveCampaignBehavior.WaveTroopEntry entry)
        {
            return entry != null
                   && !string.IsNullOrWhiteSpace(entry.TroopId)
                   && entry.MaxCount > 0
                   && entry.MaxCount >= entry.MinCount;
        }

        private CharacterObject ResolveRandomBanditTroop(string troopFamilyId)
        {
            if (troopFamilyId == "looter")
            {
                return GetTroop("looter");
            }

            List<(string troopId, int weight)> candidates = BuildBanditCandidates(troopFamilyId);
            string selectedTroopId = SelectWeightedTroopId(candidates);

            if (string.IsNullOrWhiteSpace(selectedTroopId))
                return null;

            CharacterObject troop = GetTroop(selectedTroopId);
            if (troop == null)
            {
                Log.Trace($"[BanditWave] Troop not found: {selectedTroopId}");
            }

            return troop;
        }

        private static List<(string troopId, int weight)> BuildBanditCandidates(string troopFamilyId)
        {
            // У морских бандитов нижний тир называется sea_raider_bandit, а не sea_raiders_bandit
            if (troopFamilyId == "sea_raiders")
            {
                return new List<(string troopId, int weight)>
                {
                    ("sea_raider_bandit", 50),
                    ("sea_raiders_raider", 28),
                    ("sea_raiders_chief", 14),
                    ("sea_raiders_boss", 8),
                };
            }

            return new List<(string troopId, int weight)>
            {
                ($"{troopFamilyId}_bandit", 50),
                ($"{troopFamilyId}_raider", 28),
                ($"{troopFamilyId}_chief", 14),
                ($"{troopFamilyId}_boss", 8),
            };
        }

        private string SelectWeightedTroopId(List<(string troopId, int weight)> candidates)
        {
            List<(string troopId, int weight)> available = candidates
                .Where(c => c.weight > 0)
                .Where(c => GetTroop(c.troopId) != null)
                .ToList();

            if (available.Count == 0)
                return null;

            int totalWeight = available.Sum(c => c.weight);
            int roll = MBRandom.RandomInt(totalWeight);

            foreach (var candidate in available)
            {
                roll -= candidate.weight;
                if (roll < 0)
                    return candidate.troopId;
            }

            return available[0].troopId;
        }

        private static CharacterObject GetTroop(string troopId)
        {
            return MBObjectManager.Instance.GetObject<CharacterObject>(troopId);
        }

        private int CountAliveWaveEnemies()
        {
            return _spawnedWaveAgents.Count(a =>
                a != null &&
                a.IsActive() &&
                a.Team != null &&
                Mission.Current?.PlayerEnemyTeam != null &&
                a.Team == Mission.Current.PlayerEnemyTeam);
        }

        private PartyBase ResolveEnemyParty()
        {
            return Mission.Current?.PlayerEnemyTeam?.TeamAgents?
                .Select(a => a.Origin?.BattleCombatant as PartyBase)
                .Where(p => p != null)
                .SelectRandom();
        }

        private void KeepMainHeroSafe(BanditWaveCampaignBehavior.BanditWaveState state)
        {
            if (!state.MakeMainHeroImmortal)
                return;

            Agent mainAgent = Mission.Current?.MainAgent;
            if (mainAgent?.Character == null)
                return;

            Hero playerHero = Hero.MainHero;
            if (mainAgent.Character != playerHero.CharacterObject)
                return;

            mainAgent.Health = mainAgent.HealthLimit;
        }

        private void PreventEnemyRouting()
        {
            Team enemyTeam = Mission.Current?.PlayerEnemyTeam;
            if (enemyTeam == null)
                return;

            foreach (Agent agent in enemyTeam.ActiveAgents)
            {
                if (agent == null || !agent.IsActive())
                    continue;

                agent.SetMorale(100f);

                if (agent.IsRunningAway)
                {
                    agent.StopRetreating();
                }
            }
        }
    }
}