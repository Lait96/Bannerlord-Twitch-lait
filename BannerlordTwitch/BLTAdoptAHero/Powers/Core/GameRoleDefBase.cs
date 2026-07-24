using System;
using System.ComponentModel;
using System.Linq;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.UI;
using BannerlordTwitch.Util;
using TaleWorlds.CampaignSystem;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;
using YamlIgnore = YamlDotNet.Serialization.YamlIgnoreAttribute;

namespace BLTAdoptAHero.Powers
{
    /// <summary>
    /// A permanent class-defining gameplay style applied when a hero joins a battle.
    /// </summary>
    public abstract class GameRoleDefBase : HeroPowerDefBase, ILoaded, IDocumentable
    {
        private GlobalHeroPowerConfig PowerConfig { get; set; }

        [YamlIgnore, Browsable(false)]
        public sealed override LocString Name
        {
            get => RoleName;
            set { }
        }

        [YamlIgnore, Browsable(false)]
        protected abstract LocString RoleName { get; }

        [YamlIgnore, Browsable(false)]
        public string RoleTypeLabel => "{=GameRoleDefBase_Type}Role".Translate();

        protected GameRoleDefBase()
        {
            PowerConfig = ConfigureContext.CurrentlyEditedSettings == null
                ? null
                : GlobalHeroPowerConfig.Get(ConfigureContext.CurrentlyEditedSettings);
        }

        internal void ApplyToHero(Hero hero)
        {
            if (!HasGameplayEffects || PowerConfig == null ||
                PowerConfig.DisablePowersInTournaments && MissionHelpers.InTournament())
            {
                return;
            }

            BLTHeroPowersMissionBehavior.PowerHandler.ConfigureHandlers(
                hero, this, handlers => OnHeroJoinedBattle(hero, handlers));
        }

        protected virtual bool HasGameplayEffects => false;

        protected virtual void OnHeroJoinedBattle(Hero hero, PowerHandler.Handlers handlers) { }

        public virtual void GenerateDocumentation(IDocumentationGenerator generator)
        {
            generator.P(Description.ToString());
            DocumentationHelpers.AutoDocument(generator, this);
        }

        public override string ToString() => RoleName.ToString();

        public void OnLoaded(Settings settings) => PowerConfig = GlobalHeroPowerConfig.Get(settings);

        public sealed class ItemSource : IItemsSource
        {
            public ItemCollection GetValues()
            {
                var source = GlobalHeroPowerConfig.Get(ConfigureContext.CurrentlyEditedSettings);
                var damageDealer = source?.GameRoleDefs.OfType<DamageDealerGameRole>().FirstOrDefault();
                var values = new ItemCollection
                {
                    {
                        Guid.Empty,
                        damageDealer?.Name.ToString() ??
                        "{=DamageDealerGameRole_Name}Damage Dealer".Translate()
                    }
                };

                if (source != null)
                {
                    foreach (var role in source.GameRoleDefs.Where(role => role is not DamageDealerGameRole))
                        values.Add(role.ID, role.Name.ToString());
                }

                return values;
            }
        }
    }

    public class GameRoleDefinitionCollectionEditor : DerivedClassCollectionEditor<GameRoleDefBase>
    {
        protected override bool IncludeType(Type type) => false;
    }

    public class HeroPowerDefinitionCollectionEditor : DerivedClassCollectionEditor<HeroPowerDefBase>
    {
        protected override bool IncludeType(Type type) =>
            !typeof(GameRoleDefBase).IsAssignableFrom(type);
    }
}
