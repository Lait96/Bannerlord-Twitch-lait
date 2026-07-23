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

}
