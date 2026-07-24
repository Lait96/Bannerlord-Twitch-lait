using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using BannerlordTwitch;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Rewards;
using BannerlordTwitch.UI;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Powers;
using JetBrains.Annotations;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero
{
    [LocDisplayName("{=GlobalHeroPowerConfig_Name}Power Config")]
    public class GlobalHeroPowerConfig : IUpdateFromDefault, ILoaded, IDocumentable
    {
        #region Static
        private const string ID = "Adopt A Hero - Power Config";
        private static readonly Guid DamageDealerRoleID = Guid.Parse("0d4a6d25-86b7-4f4d-bd7a-c38b3fb46cd2");
        private static readonly Guid RogueRoleID = Guid.Parse("ca2ed9bc-bc8a-48bf-89b7-389779fdba5c");
        private static readonly Guid HealerRoleID = Guid.Parse("d79aa782-479c-46cc-8298-722c690c06fc");
        private static readonly Guid TankRoleID = Guid.Parse("7e16bd2a-5e68-44d5-a137-2cd0fc047f42");
        internal static void Register() => ActionManager.RegisterGlobalConfigType(ID, typeof(GlobalHeroPowerConfig));
        internal static GlobalHeroPowerConfig Get() => ActionManager.GetGlobalConfig<GlobalHeroPowerConfig>(ID);
        internal static GlobalHeroPowerConfig Get(Settings fromSettings) => fromSettings.GetGlobalConfig<GlobalHeroPowerConfig>(ID);
        #endregion

        #region User Editable
        [LocDisplayName("{=GlobalHeroPowerConfig_PowerDefs_Name}Power Definitions"),
         LocDescription("{=GlobalHeroPowerConfig_PowerDefs_Desc}Defined powers"),
         Editor(typeof(HeroPowerDefinitionCollectionEditor),
             typeof(HeroPowerDefinitionCollectionEditor)),
         PropertyOrder(1), UsedImplicitly]
        public ObservableCollection<HeroPowerDefBase> PowerDefs { get; set; } = new();

        [LocDisplayName("{=GlobalHeroPowerConfig_GameRoleDefs_Name}Game Role Definitions"),
         LocDescription("{=GlobalHeroPowerConfig_GameRoleDefs_Desc}Defined game roles and their configuration"),
         Editor(typeof(GameRoleDefinitionCollectionEditor),
             typeof(GameRoleDefinitionCollectionEditor)),
         PropertyOrder(2), UsedImplicitly]
        public ObservableCollection<GameRoleDefBase> GameRoleDefs { get; set; } = new();

        [LocDisplayName("{=GlobalHeroPowerConfig_DisablePowersInTournaments_Name}Disable Powers In Tournaments"),
         LocDescription("{=GlobalHeroPowerConfig_DisablePowersInTournaments_Desc}Whether powers and game roles are disabled in a tournament"),
         PropertyOrder(3), UsedImplicitly]
        public bool DisablePowersInTournaments { get; set; } = true;

        #region Deprecated
        [Browsable(false), UsedImplicitly]
        public List<Dictionary<object, object>> SavedPowerDefs { get; set; }
        #endregion
        #endregion

        #region Public Interface
        public HeroPowerDefBase GetPower(Guid id)
            => PowerDefs?.FirstOrDefault(c => c.ID == id);

        public GameRoleDefBase GetGameRole(Guid id) => id == Guid.Empty
            ? GameRoleDefs?.OfType<DamageDealerGameRole>().FirstOrDefault()
            : GameRoleDefs?.FirstOrDefault(role => role.ID == id);
        #endregion

        #region IUpdateFromDefault
        public void OnUpdateFromDefault(Settings defaultSettings)
        {
            PowerDefs ??= new();
            GameRoleDefs ??= new();

            SettingsHelpers.MergeCollections(
                PowerDefs,
                Get(defaultSettings).PowerDefs,
                (a, b) => a.ID == b.ID
            );
            SettingsHelpers.MergeCollections(
                GameRoleDefs,
                Get(defaultSettings).GameRoleDefs,
                (a, b) => a.GetType() == b.GetType()
            );
        }
        #endregion

        #region ILoaded
        public void OnLoaded(Settings settings)
        {
            PowerDefs ??= new();
            GameRoleDefs ??= new();

            // Upgrade path
            if (SavedPowerDefs != null)
            {
                PowerDefs = new(SavedPowerDefs
                    .Select(d =>
                    {
                        if (d.TryGetValue("Type", out object o) && o is string id)
                        {
                            var t = id.ToUpper() switch
                            {
                                "E0A274DF-ADBB-4725-9EAE-59806BF9B5DC" => typeof(AbsorbHealthPower),
                                "378648B6-5586-4812-AD08-22DA6374440C" => typeof(AddDamagePower),
                                "C4213666-2176-42B4-8DBB-BFE0182BCCE1" => typeof(AddHealthPower),
                                "FFE07DA3-E977-42D8-80CA-5DFFF66123EB" => typeof(ReflectDamagePower),
                                "6DF1D8D6-02C6-4D30-8D12-CCE24077A4AA" => typeof(StatModifyPower),
                                "366C25BD-5B20-4EB1-98F5-04B5FDDD6285" => typeof(TakeDamagePower),
                                _ => throw new Exception($"Power type id {id} not found")
                            };

                            return (HeroPowerDefBase)YamlHelpers.ConvertObjectUntagged(d, t);
                        }

                        throw new Exception($"Invalid power found during load");
                    }));
                SavedPowerDefs = null;
            }

            var legacyGameRoles = PowerDefs.OfType<GameRoleDefBase>().ToList();
            foreach (var role in legacyGameRoles)
            {
                PowerDefs.Remove(role);
                if (GameRoleDefs.All(existing => existing.ID != role.ID))
                    GameRoleDefs.Add(role);
            }

            EnsureBuiltInRole(() => new DamageDealerGameRole { ID = DamageDealerRoleID });
            EnsureBuiltInRole(() => new RogueStealthPower { ID = RogueRoleID });
            EnsureBuiltInRole(() => new HealerGameRole { ID = HealerRoleID });
            EnsureBuiltInRole(() => new TankGameRole { ID = TankRoleID });

            var gameRoleIds = GameRoleDefs.Select(role => role.ID).ToHashSet();
            var classConfig = GlobalHeroClassConfig.Get(settings);
            foreach (var heroClass in classConfig.ClassDefs)
            {
                var legacySelections = heroClass.PassivePower?.Powers
                    .Where(item => gameRoleIds.Contains(item.PowerID))
                    .ToList() ?? new List<PassivePowerGroupItem>();
                if (legacySelections.Count == 0)
                    continue;

                heroClass.GameRoleID = legacySelections[0].PowerID;
                foreach (var selection in legacySelections)
                    heroClass.PassivePower.Powers.Remove(selection);
            }
        }

        private void EnsureBuiltInRole<T>(Func<T> create) where T : GameRoleDefBase
        {
            if (GameRoleDefs.All(role => role is not T))
                GameRoleDefs.Add(create());
        }
        #endregion

        #region IDocumentable
        public void GenerateDocumentation(IDocumentationGenerator generator)
        {
            generator.Div("game-role-config", () =>
            {
                generator.H1("{=GlobalHeroPowerConfig_Doc_GameRoles}Game Roles".Translate());
                foreach (var role in GameRoleDefs)
                {
                    generator.Details("role-card", () =>
                    {
                        generator.Summary("role-card__title", role.Name.ToString());
                        generator.Div("role-card__content", () => role.GenerateDocumentation(generator));
                    });
                }
            });
        }
        #endregion
    }
}
