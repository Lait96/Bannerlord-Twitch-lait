using System;
using System.Collections.Generic;
using BannerlordTwitch;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero.Actions
{
    [LocDisplayName("{=BanditWaveAction_DisplayName}Bandit Waves"),
     LocDescription("{=BanditWaveAction_Description}Starts and stops endless bandit waves"),
     UsedImplicitly]
    public class BanditWaveAction : HeroCommandHandlerBase
    {
        [CategoryOrder("General", 0),
         CategoryOrder("Looters", 1),
         CategoryOrder("Forest Bandits", 2),
         CategoryOrder("Mountain Bandits", 3),
         CategoryOrder("Sea Raiders", 4),
         CategoryOrder("Steppe Bandits", 5),
         CategoryOrder("Desert Bandits", 6)]
        private class Settings
        {
            [LocDisplayName("{=BanditWaveAction_Enabled_Name}Enabled"),
             LocCategory("General", "{=BanditWaveAction_Category_General}General"),
             LocDescription("{=BanditWaveAction_Enabled_Desc}Enables the bandit wave mode command"),
             PropertyOrder(1), UsedImplicitly]
            public bool Enabled { get; set; } = true;

            [LocDisplayName("{=BanditWaveAction_AllowEnemySide_Name}Allow BLT on Enemy Side"),
             LocCategory("General", "{=BanditWaveAction_Category_General}General"),
             LocDescription("{=BanditWaveAction_AllowEnemySide_Desc}Allows BLT heroes to join the enemy side while bandit wave mode is active"),
             PropertyOrder(2), UsedImplicitly]
            public bool AllowBLTOnEnemySide { get; set; } = true;

            [LocDisplayName("{=BanditWaveAction_RespawnThreshold_Name}Respawn Threshold"),
             LocCategory("General", "{=BanditWaveAction_Category_General}General"),
             LocDescription("{=BanditWaveAction_RespawnThreshold_Desc}Spawns the next wave when the number of alive enemies is less than or equal to this value"),
             PropertyOrder(3), UsedImplicitly]
            public int RespawnWhenEnemiesLeftAtOrBelow { get; set; } = 1;

            [LocDisplayName("{=BanditWaveAction_DelayBetweenWaves_Name}Delay Between Waves"),
             LocCategory("General", "{=BanditWaveAction_Category_General}General"),
             LocDescription("{=BanditWaveAction_DelayBetweenWaves_Desc}Delay in seconds before spawning the next wave"),
             PropertyOrder(4), UsedImplicitly]
            public float DelayBetweenWavesSeconds { get; set; } = 2f;

            [LocDisplayName("{=BanditWaveAction_Looters_Enabled_Name}Enable Looters"),
             LocCategory("Looters", "{=BanditWaveAction_Looters_Category}Looters"),
             LocDescription("{=BanditWaveAction_Looters_Enabled_Desc}Allows looters to appear in waves"),
             PropertyOrder(1), UsedImplicitly]
            public bool EnableLooters { get; set; } = true;

            [LocDisplayName("{=BanditWaveAction_Looters_Min_Name}Looters Min"),
             LocCategory("Looters", "{=BanditWaveAction_Looters_Category}Looters"),
             LocDescription("{=BanditWaveAction_Looters_Min_Desc}Minimum number of looters in a wave"),
             PropertyOrder(2), UsedImplicitly]
            public int LootersMin { get; set; } = 4;

            [LocDisplayName("{=BanditWaveAction_Looters_Max_Name}Looters Max"),
             LocCategory("Looters", "{=BanditWaveAction_Looters_Category}Looters"),
             LocDescription("{=BanditWaveAction_Looters_Max_Desc}Maximum number of looters in a wave"),
             PropertyOrder(3), UsedImplicitly]
            public int LootersMax { get; set; } = 10;

            [LocDisplayName("{=BanditWaveAction_Forest_Enabled_Name}Enable Forest Bandits"),
             LocCategory("Forest Bandits", "{=BanditWaveAction_Forest_Category}Forest Bandits"),
             LocDescription("{=BanditWaveAction_Forest_Enabled_Desc}Allows forest bandits to appear in waves"),
             PropertyOrder(1), UsedImplicitly]
            public bool EnableForestBandits { get; set; } = true;

            [LocDisplayName("{=BanditWaveAction_Forest_Min_Name}Forest Bandits Min"),
             LocCategory("Forest Bandits", "{=BanditWaveAction_Forest_Category}Forest Bandits"),
             LocDescription("{=BanditWaveAction_Forest_Min_Desc}Minimum number of forest bandits in a wave"),
             PropertyOrder(2), UsedImplicitly]
            public int ForestBanditsMin { get; set; } = 3;

            [LocDisplayName("{=BanditWaveAction_Forest_Max_Name}Forest Bandits Max"),
             LocCategory("Forest Bandits", "{=BanditWaveAction_Forest_Category}Forest Bandits"),
             LocDescription("{=BanditWaveAction_Forest_Max_Desc}Maximum number of forest bandits in a wave"),
             PropertyOrder(3), UsedImplicitly]
            public int ForestBanditsMax { get; set; } = 7;

            [LocDisplayName("{=BanditWaveAction_Mountain_Enabled_Name}Enable Mountain Bandits"),
             LocCategory("Mountain Bandits", "{=BanditWaveAction_Mountain_Category}Mountain Bandits"),
             LocDescription("{=BanditWaveAction_Mountain_Enabled_Desc}Allows mountain bandits to appear in waves"),
             PropertyOrder(1), UsedImplicitly]
            public bool EnableMountainBandits { get; set; } = true;

            [LocDisplayName("{=BanditWaveAction_Mountain_Min_Name}Mountain Bandits Min"),
             LocCategory("Mountain Bandits", "{=BanditWaveAction_Mountain_Category}Mountain Bandits"),
             LocDescription("{=BanditWaveAction_Mountain_Min_Desc}Minimum number of mountain bandits in a wave"),
             PropertyOrder(2), UsedImplicitly]
            public int MountainBanditsMin { get; set; } = 2;

            [LocDisplayName("{=BanditWaveAction_Mountain_Max_Name}Mountain Bandits Max"),
             LocCategory("Mountain Bandits", "{=BanditWaveAction_Mountain_Category}Mountain Bandits"),
             LocDescription("{=BanditWaveAction_Mountain_Max_Desc}Maximum number of mountain bandits in a wave"),
             PropertyOrder(3), UsedImplicitly]
            public int MountainBanditsMax { get; set; } = 6;

            [LocDisplayName("{=BanditWaveAction_Sea_Enabled_Name}Enable Sea Raiders"),
             LocCategory("Sea Raiders", "{=BanditWaveAction_Sea_Category}Sea Raiders"),
             LocDescription("{=BanditWaveAction_Sea_Enabled_Desc}Allows sea raiders to appear in waves"),
             PropertyOrder(1), UsedImplicitly]
            public bool EnableSeaRaiders { get; set; } = true;

            [LocDisplayName("{=BanditWaveAction_Sea_Min_Name}Sea Raiders Min"),
             LocCategory("Sea Raiders", "{=BanditWaveAction_Sea_Category}Sea Raiders"),
             LocDescription("{=BanditWaveAction_Sea_Min_Desc}Minimum number of sea raiders in a wave"),
             PropertyOrder(2), UsedImplicitly]
            public int SeaRaidersMin { get; set; } = 2;

            [LocDisplayName("{=BanditWaveAction_Sea_Max_Name}Sea Raiders Max"),
             LocCategory("Sea Raiders", "{=BanditWaveAction_Sea_Category}Sea Raiders"),
             LocDescription("{=BanditWaveAction_Sea_Max_Desc}Maximum number of sea raiders in a wave"),
             PropertyOrder(3), UsedImplicitly]
            public int SeaRaidersMax { get; set; } = 5;

            [LocDisplayName("{=BanditWaveAction_Steppe_Enabled_Name}Enable Steppe Bandits"),
             LocCategory("Steppe Bandits", "{=BanditWaveAction_Steppe_Category}Steppe Bandits"),
             LocDescription("{=BanditWaveAction_Steppe_Enabled_Desc}Allows steppe bandits to appear in waves"),
             PropertyOrder(1), UsedImplicitly]
            public bool EnableSteppeBandits { get; set; } = true;

            [LocDisplayName("{=BanditWaveAction_Steppe_Min_Name}Steppe Bandits Min"),
             LocCategory("Steppe Bandits", "{=BanditWaveAction_Steppe_Category}Steppe Bandits"),
             LocDescription("{=BanditWaveAction_Steppe_Min_Desc}Minimum number of steppe bandits in a wave"),
             PropertyOrder(2), UsedImplicitly]
            public int SteppeBanditsMin { get; set; } = 3;

            [LocDisplayName("{=BanditWaveAction_Steppe_Max_Name}Steppe Bandits Max"),
             LocCategory("Steppe Bandits", "{=BanditWaveAction_Steppe_Category}Steppe Bandits"),
             LocDescription("{=BanditWaveAction_Steppe_Max_Desc}Maximum number of steppe bandits in a wave"),
             PropertyOrder(3), UsedImplicitly]
            public int SteppeBanditsMax { get; set; } = 6;

            [LocDisplayName("{=BanditWaveAction_Desert_Enabled_Name}Enable Desert Bandits"),
             LocCategory("Desert Bandits", "{=BanditWaveAction_Desert_Category}Desert Bandits"),
             LocDescription("{=BanditWaveAction_Desert_Enabled_Desc}Allows desert bandits to appear in waves"),
             PropertyOrder(1), UsedImplicitly]
            public bool EnableDesertBandits { get; set; } = true;

            [LocDisplayName("{=BanditWaveAction_Desert_Min_Name}Desert Bandits Min"),
             LocCategory("Desert Bandits", "{=BanditWaveAction_Desert_Category}Desert Bandits"),
             LocDescription("{=BanditWaveAction_Desert_Min_Desc}Minimum number of desert bandits in a wave"),
             PropertyOrder(2), UsedImplicitly]
            public int DesertBanditsMin { get; set; } = 3;

            [LocDisplayName("{=BanditWaveAction_Desert_Max_Name}Desert Bandits Max"),
             LocCategory("Desert Bandits", "{=BanditWaveAction_Desert_Category}Desert Bandits"),
             LocDescription("{=BanditWaveAction_Desert_Max_Desc}Maximum number of desert bandits in a wave"),
             PropertyOrder(3), UsedImplicitly]
            public int DesertBanditsMax { get; set; } = 6;
        }

        public override Type HandlerConfigType => typeof(Settings);

        protected override void ExecuteInternal(
            Hero adoptedHero,
            ReplyContext context,
            object config,
            Action<string> onSuccess,
            Action<string> onFailure)
        {
            if (config is not Settings settings)
                return;

            if (!settings.Enabled)
            {
                onFailure("{=BanditWaveAction_Error_Disabled}Bandit waves are disabled.".Translate());
                return;
            }

            string args = context.Args?.Trim() ?? string.Empty;
            string mode = string.IsNullOrWhiteSpace(args)
                ? "status"
                : args.Split(' ')[0].ToLowerInvariant();

            switch (mode)
            {
                case "start":
                    Start(settings, onSuccess, onFailure);
                    break;

                case "stop":
                    Stop(onSuccess, onFailure);
                    break;

                case "status":
                    Status(onSuccess);
                    break;

                default:
                    onFailure("{=BanditWaveAction_Usage}Usage: !bandits start | stop | status".Translate());
                    break;
            }
        }

        private static List<BanditWaveCampaignBehavior.WaveTroopEntry> BuildWaveEntries(Settings settings)
        {
            var result = new List<BanditWaveCampaignBehavior.WaveTroopEntry>();

            AddWaveEntry(result, settings.EnableLooters, "looter", settings.LootersMin, settings.LootersMax);
            AddWaveEntry(result, settings.EnableForestBandits, "forest_bandits", settings.ForestBanditsMin, settings.ForestBanditsMax);
            AddWaveEntry(result, settings.EnableMountainBandits, "mountain_bandits", settings.MountainBanditsMin, settings.MountainBanditsMax);
            AddWaveEntry(result, settings.EnableSeaRaiders, "sea_raiders", settings.SeaRaidersMin, settings.SeaRaidersMax);
            AddWaveEntry(result, settings.EnableSteppeBandits, "steppe_bandits", settings.SteppeBanditsMin, settings.SteppeBanditsMax);
            AddWaveEntry(result, settings.EnableDesertBandits, "desert_bandits", settings.DesertBanditsMin, settings.DesertBanditsMax);

            return result;
        }

        private static void AddWaveEntry(
            List<BanditWaveCampaignBehavior.WaveTroopEntry> result,
            bool enabled,
            string troopId,
            int min,
            int max)
        {
            if (!enabled)
                return;

            min = Math.Max(0, min);
            max = Math.Max(min, max);

            if (max <= 0)
                return;

            result.Add(new BanditWaveCampaignBehavior.WaveTroopEntry
            {
                TroopId = troopId,
                MinCount = min,
                MaxCount = max,
            });
        }

        private void Start(Settings settings, Action<string> onSuccess, Action<string> onFailure)
        {
            var current = BanditWaveCampaignBehavior.Current;
            if (current == null)
            {
                onFailure("{=BanditWaveAction_Error_BehaviorUnavailable}Bandit wave behavior is not available.".Translate());
                return;
            }

            if (current.State?.IsEnabled == true && current.State.StopRequested == false)
            {
                onFailure("{=BanditWaveAction_Error_AlreadyActive}Bandit waves are already active.".Translate());
                return;
            }

            var waves = BuildWaveEntries(settings);
            if (waves.Count == 0)
            {
                onFailure("{=BanditWaveAction_Error_NoBanditTypesEnabled}No bandit types are enabled.".Translate());
                return;
            }

            var state = new BanditWaveCampaignBehavior.BanditWaveState
            {
                IsEnabled = true,
                StopRequested = false,
                AllowPlayersOnEnemySide = settings.AllowBLTOnEnemySide,
                RespawnWhenEnemiesLeftAtOrBelow = settings.RespawnWhenEnemiesLeftAtOrBelow,
                DelayBetweenWavesSeconds = settings.DelayBetweenWavesSeconds,
                MakeMainHeroImmortal = true,
                Waves = waves
            };

            current.Start(state);

            onSuccess("{=BanditWaveAction_Success_Armed}Bandit wave mode is armed. Start the battle entry point.".Translate());
        }

        private void Stop(Action<string> onSuccess, Action<string> onFailure)
        {
            var current = BanditWaveCampaignBehavior.Current;
            if (current == null)
            {
                onFailure("{=BanditWaveAction_Error_BehaviorUnavailable}Bandit wave behavior is not available.".Translate());
                return;
            }

            current.RequestStop();
            onSuccess("{=BanditWaveAction_Success_StopRequested}Bandit waves will stop after the current enemies are cleared.".Translate());
        }

        private void Status(Action<string> onSuccess)
        {
            var state = BanditWaveCampaignBehavior.Current?.State;
            if (state == null || !state.IsEnabled)
            {
                onSuccess("{=BanditWaveAction_Status_Inactive}Bandit waves are inactive.".Translate());
                return;
            }

            onSuccess(
                "{=BanditWaveAction_Status_Active}Bandit waves active. Wave={WAVE}, stopRequested={STOP_REQUESTED}"
                    .Translate(
                        ("WAVE", state.CurrentWave.ToString()),
                        ("STOP_REQUESTED", state.StopRequested.ToString())
                    )
            );
        }
    }
}