using System;
using System.Linq;
using System.Collections.Generic;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using BLTAdoptAHero;

namespace BLTAdoptAHero
{
    [LocDisplayName("{=battle_info}Battle Info"),
     LocDescription("{=action_battle_info_desc}Shows hero battle info"),
     UsedImplicitly]
    public class BattleInfo : HeroCommandHandlerBase
    {
        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config,
            Action<string> onSuccess, Action<string> onFailure)
        {
            if (adoptedHero == null)
            {
                onFailure(AdoptAHero.NoHeroMessage);
                return;
            }

            if (Mission.Current == null)
            {
                onFailure("{=battle_info_no_mission}No mission!".Translate());
                return;
            }

            var mapEvent = PlayerEncounter.Battle;
            Mission mission = Mission.Current;
            bool isDefend = false;
            int attackCount = 0;
            int defendCount = 0;
            Team playerTeam = null;
            Team allyTeam = null;
            Team enemyTeam = null;
            if (!MissionHelpers.InTournament())
            {
                // Count alive agents by side
                attackCount = mission.GetMemberCountOfSide(BattleSideEnum.Attacker);
                defendCount = mission.GetMemberCountOfSide(BattleSideEnum.Defender);

                playerTeam = mission.PlayerTeam;
                allyTeam = mission.PlayerAllyTeam;
                enemyTeam = mission.PlayerEnemyTeam;
                
                if (playerTeam != null && playerTeam.Side == BattleSideEnum.Defender || (allyTeam != null && allyTeam.Side == BattleSideEnum.Defender))
                    isDefend = true;
            }

            var missionBehavior = BLTAdoptAHeroCommonMissionBehavior.Current;
            if (missionBehavior == null)
            {
                onFailure("{=battle_info_no_behavior}Mission behavior not found!".Translate());
                return;
            }

            var agent = adoptedHero.GetAgent();
            var state = BLTAdoptAHeroCommonMissionBehavior.Current.GetMissionState(adoptedHero);
            var state2 = BLTSummonBehavior.Current.GetHeroSummonState(adoptedHero);
            int cd = 0;
            if (state2 != null)
                cd = (int)state2.CooldownRemaining;
            if (!BLTSummonBehavior.Current.HeroDeathSpecifics.TryGetValue(adoptedHero, out var diedInfo))
            {
                diedInfo = (null, default); // fallback if no record exists
            }

            // Calculate death chance
            bool canDie = GlobalCommonConfig.Get().AllowDeath;

            if (agent == null && !MissionHelpers.InTournament())
            {
                string playerFaction = playerTeam.Leader.GetHero().MapFaction.Name.ToString() ?? "{=battle_info_unknown}unknown".Translate(); string enemyFaction = (isDefend ? mapEvent.AttackerSide.MapFaction.Name.ToString() : mapEvent.DefenderSide.MapFaction.Name.ToString());
                string battlestring = "{=battle_info_player_enemy_counts}{PlayerFaction} vs {EnemyFaction}(P/E): {PlayerCount}/{EnemyCount} - "
                    .Translate(
                        ("PlayerFaction", playerFaction),
                        ("EnemyFaction", enemyFaction),
                        ("PlayerCount", isDefend ? defendCount : attackCount),
                        ("EnemyCount", isDefend ? attackCount : defendCount));
                battlestring += " | " + "{=battle_info_not_in_battle}Hero is not currently in battle!".Translate() + $" ({cd}s)";

                if (diedInfo.killer != null)
                {                   
                    var weaponClass = (WeaponClass)diedInfo.blow.WeaponClass;
                    string weaponName = weaponClass.ToString();

                    battlestring += " | " + "{=battle_info_killed_by}Killed by {Killer} with {Weapon} ({Damage})"
                        .Translate(
                            ("Killer", diedInfo.killer.Name),
                            ("Weapon", weaponName),
                            ("Damage", diedInfo.blow.InflictedDamage));

                    if (canDie)
                    {
                        float deathMod = GlobalCommonConfig.Get().DeathChance;
                        var deathChance = Campaign.Current.Models.PartyHealingModel.GetSurvivalChance(adoptedHero.PartyBelongedTo.Party, adoptedHero.CharacterObject, diedInfo.blow.DamageType, true);
                        battlestring += " | " + "{=battle_info_death_chance}Death chance: {Chance}%"
                            .Translate(("Chance", deathChance * deathMod * 100));
                    }
                        

                }

                onFailure(battlestring);
                return;
            }
            else if (agent == null && MissionHelpers.InTournament())
            {
                onFailure("{=battle_info_not_in_battle}Hero is not currently in battle!".Translate());
                return;
            }


            static float ActivePowerFraction(Hero hero)
            {
                var classDef = BLTAdoptAHeroCampaignBehavior.Current?.GetClass(hero);
                if (classDef?.ActivePower == null)
                    return 0f;

                // Check if power is active
                if (!classDef.ActivePower.IsActive(hero))
                    return 0f;

                var (duration, remaining) = classDef.ActivePower.DurationRemaining(hero);

                return duration > 0 ? remaining / duration : 0f;
            }

            // Active combat
            float currentTime = Mission.Current.CurrentTime;
            bool hasAttacked = (currentTime - agent.LastMeleeAttackTime < 10f)
                || (currentTime - agent.LastRangedAttackTime < 10f)
                || (currentTime - agent.LastMeleeHitTime < 10f)
                || (currentTime - agent.LastRangedHitTime < 10f);


            // Mounted info
            string mountInfo = "";
            if (agent.MountAgent != null)
            {
                mountInfo = $"{agent.MountAgent.Health}/{agent.MountAgent.HealthLimit}";
            }


            var equipment = agent.Equipment;
            // --- Main hand ---
            var mainIndex = agent.GetPrimaryWieldedItemIndex();
            var mainItemObj = mainIndex != EquipmentIndex.None ? equipment[mainIndex].Item : null;
            string weaponInfo = "{=battle_info_unarmed}Unarmed".Translate();

            if (mainItemObj != null)
            {
                string ammoInfo = "";
                if (mainItemObj.ItemType == ItemObject.ItemTypeEnum.Bow
                    || mainItemObj.ItemType == ItemObject.ItemTypeEnum.Crossbow
                    || mainItemObj.ItemType == ItemObject.ItemTypeEnum.Pistol
                    || mainItemObj.ItemType == ItemObject.ItemTypeEnum.Musket
                    || mainItemObj.ItemType == ItemObject.ItemTypeEnum.Thrown
                    || mainItemObj.ItemType == ItemObject.ItemTypeEnum.Sling)
                {
                    int ammo = equipment.GetAmmoAmount(mainIndex);
                    int maxAmmo = equipment.GetMaxAmmo(mainIndex);
                    ammoInfo = " - " + "{=battle_info_ammo}Ammo: {Current}/{Max}".Translate(("Current", ammo), ("Max", maxAmmo));
                }

                weaponInfo = $"{mainItemObj.Name} ({mainItemObj.ItemType}){ammoInfo}";
            }

            // --- Off-hand ---
            var offIndex = agent.GetOffhandWieldedItemIndex();
            var offItemObj = offIndex != EquipmentIndex.None ? equipment[offIndex].Item : null;

            if (offItemObj != null)
                //if (offItemObj.ItemType == ItemObject.ItemTypeEnum.Shield)
                //{
                //    int shp = offItemObj.ItemComponent..
                //}
                weaponInfo += $" + {offItemObj.Name} ({offItemObj.ItemType})";
            var weaponSlots = new[]
            {
                EquipmentIndex.Weapon0,
                EquipmentIndex.Weapon1,
                EquipmentIndex.Weapon2,
                EquipmentIndex.Weapon3,
                EquipmentIndex.ExtraWeaponSlot
            };
            // --- Other ranged/thrown weapons not in main-hand ---
            var addedThrownNames = new HashSet<string>();
            foreach (EquipmentIndex slot in weaponSlots)
            {
                if (slot == mainIndex || slot == offIndex)
                    continue;

                var element = equipment[slot];
                if (element.Item == null)
                    continue;

                var item = element.Item;

                // Only consider ranged or thrown weapons
                switch (item.ItemType)
                {
                    case ItemObject.ItemTypeEnum.Bow:
                    case ItemObject.ItemTypeEnum.Crossbow:
                    case ItemObject.ItemTypeEnum.Sling:
                    case ItemObject.ItemTypeEnum.Pistol:
                    case ItemObject.ItemTypeEnum.Musket:
                    case ItemObject.ItemTypeEnum.Thrown:
                        {
                            string nameKey = item.Name.ToString();

                            // If thrown and same name already added → skip
                            if (item.ItemType == ItemObject.ItemTypeEnum.Thrown &&
                                addedThrownNames.Contains(nameKey))
                                break;

                            if (item.ItemType == ItemObject.ItemTypeEnum.Thrown)
                                addedThrownNames.Add(nameKey);

                            int ammo = equipment.GetAmmoAmount(slot);
                            int maxAmmo = equipment.GetMaxAmmo(slot);

                            weaponInfo += $" + {item.Name} ({item.ItemType}) - Ammo: {ammo}/{maxAmmo}";
                            break;
                        }
                }
            }

            string message = "";
            if (!MissionHelpers.InTournament())
            {
                var leader = enemyTeam?.Leader ?? enemyTeam?.GeneralAgent;
                string playerFaction = playerTeam.Leader.GetHero().MapFaction.Name.ToString() ?? "{=battle_info_unknown}unknown".Translate(); string enemyFaction = (isDefend ? mapEvent.AttackerSide.MapFaction.Name.ToString() : mapEvent.DefenderSide.MapFaction.Name.ToString());
                message += $"{playerFaction} vs {enemyFaction}(P/E):" + (isDefend ? $"{defendCount}/{attackCount} - " : $"{attackCount}/{defendCount} - ");
            }
                
            message += "{=battle_info_class_line}Class: {Class}".Translate(("Class", adoptedHero.GetClass()?.Name.ToString() ?? "{=battle_info_class_none}No class".Translate())) + "\n"
                + "{=battle_info_hp_line}- HP: {Current}/{Max}".Translate(("Current", (int)agent.Health), ("Max", (int)agent.HealthLimit)) + "\n";
            if (agent.MountAgent != null)
                message += "{=battle_info_mount_hp_line}- Mount HP: {Current}/{Max}".Translate(("Current", agent.MountAgent.Health), ("Max", agent.MountAgent.HealthLimit)) + "\n";
            message += "{=battle_info_weapon_line}- Weapon: {Weapon}".Translate(("Weapon", weaponInfo)) + "\n"
                + "{=battle_info_kills_line}- Kills: {Kills}".Translate(("Kills", state.Kills)) + "\n"
                + "{=battle_info_retinue_line}- Retinue({Count}): {Kills}".Translate(("Count", state2.ActiveRetinue + state2.ActiveRetinue2), ("Kills", state.RetinueKills)) + "\n"
                + "{=battle_info_gold_line}- Gold: {Gold}".Translate(("Gold", state.WonGold)) + "\n"
                + "{=battle_info_xp_line}- XP: {XP}".Translate(("XP", state.WonXP)) + "\n"
                + "{=battle_info_power_line}- Power: {Power}%".Translate(("Power", $"{ActivePowerFraction(adoptedHero) * 100:0}"));
            if (hasAttacked)
                message += " " + "{=battle_info_active_combat}- Active combat".Translate();

            onSuccess(message);
        }
    }
}