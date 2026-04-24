using System.Collections.Generic;
using BannerlordTwitch.SaveSystem;
using TaleWorlds.CampaignSystem;

namespace BLTAdoptAHero
{
    public class BanditWaveCampaignBehavior : CampaignBehaviorBase
    {
        public static BanditWaveCampaignBehavior Current =>
            Campaign.Current?.GetCampaignBehavior<BanditWaveCampaignBehavior>();

        public class WaveTroopEntry
        {
            /// <summary>
            /// Семейство бандитов:
            /// looter
            /// forest_bandits
            /// mountain_bandits
            /// sea_raiders
            /// steppe_bandits
            /// desert_bandits
            /// </summary>
            public string TroopId { get; set; }

            public int MinCount { get; set; }
            public int MaxCount { get; set; }
        }

        public class BanditWaveState
        {
            public bool IsEnabled { get; set; }
            public bool StopRequested { get; set; }

            /// <summary>
            /// Пока просто сохраняем настройку.
            /// Реальное применение запрета enemy-side join нужно делать в точке входа BLT на сторону врага.
            /// </summary>
            public bool AllowPlayersOnEnemySide { get; set; }

            public bool MakeMainHeroImmortal { get; set; } = true;

            /// <summary>
            /// Когда живых заспавненных врагов <= этого числа, спавним следующую волну.
            /// </summary>
            public int RespawnWhenEnemiesLeftAtOrBelow { get; set; } = 1;

            public float DelayBetweenWavesSeconds { get; set; } = 2f;

            public int CurrentWave { get; set; }

            public List<WaveTroopEntry> Waves { get; set; } = new();
        }

        private BanditWaveState _state = new();

        public BanditWaveState State => _state;

        public override void RegisterEvents()
        {
        }

        public override void SyncData(IDataStore dataStore)
        {
            using var scopedJsonSync = new ScopedJsonSync(dataStore, nameof(BanditWaveCampaignBehavior));
            scopedJsonSync.SyncDataAsJson("BanditWaveState", ref _state);

            _state ??= new BanditWaveState();
            _state.Waves ??= new List<WaveTroopEntry>();
        }

        public void Start(BanditWaveState newState)
        {
            _state = newState ?? new BanditWaveState();
            _state.IsEnabled = true;
            _state.StopRequested = false;
            _state.CurrentWave = 0;
            _state.Waves ??= new List<WaveTroopEntry>();
        }

        public void RequestStop()
        {
            _state ??= new BanditWaveState();
            _state.StopRequested = true;
        }

        public void ResetAfterMission()
        {
            _state ??= new BanditWaveState();
            _state.CurrentWave = 0;

            if (_state.StopRequested)
            {
                _state.IsEnabled = false;
            }
        }
    }
}