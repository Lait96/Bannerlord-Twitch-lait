using BannerlordTwitch.Localization;
using BLTAdoptAHero.Annotations;
using YamlDotNet.Serialization;

namespace BLTAdoptAHero.Powers
{
    [LocDisplayName("{=DamageDealerGameRole_Name}Damage Dealer"),
     LocDescription("{=DamageDealerGameRole_Desc}A combat role without additional mechanics"),
     UsedImplicitly]
    public sealed class DamageDealerGameRole : GameRoleDefBase
    {
        [YamlIgnore]
        protected override LocString RoleName => "{=DamageDealerGameRole_Name}Damage Dealer";

        [YamlIgnore]
        public override LocString Description =>
            "{=DamageDealerGameRole_Desc}A combat role without additional mechanics";
    }

    [LocDisplayName("{=HealerGameRole_Name}Healer"),
     LocDescription("{=HealerGameRole_Desc}A placeholder for the future healer role"),
     UsedImplicitly]
    public sealed class HealerGameRole : GameRoleDefBase
    {
        [YamlIgnore]
        protected override LocString RoleName => "{=HealerGameRole_Name}Healer";

        [YamlIgnore]
        public override LocString Description =>
            "{=HealerGameRole_Desc}A placeholder for the future healer role";
    }

    [LocDisplayName("{=TankGameRole_Name}Tank"),
     LocDescription("{=TankGameRole_Desc}A placeholder for the future tank role"),
     UsedImplicitly]
    public sealed class TankGameRole : GameRoleDefBase
    {
        [YamlIgnore]
        protected override LocString RoleName => "{=TankGameRole_Name}Tank";

        [YamlIgnore]
        public override LocString Description =>
            "{=TankGameRole_Desc}A placeholder for the future tank role";
    }
}
