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
    [LocDisplayName("{=BLTBattleInfoDisplayName}BattleInfo"),
     LocDescription("{=BLTBattleInfoDescription}Shows hero battle info"),
     UsedImplicitly]
    public class BattleInfo : HeroCommandHandlerBase
    {
        private class Documentation : IDocumentable
        {
            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                generator.Value("{=BLTBattleInfoDocumentation}<strong>Description:</strong> Shows detailed information about your adopted hero's current battle status, including health, mount, weapons, kills, retinue, gold, XP, and active powers.\n".Translate());
            }
        }

        public override Type HandlerConfigType => typeof(Documentation);


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
                onFailure("{=BLTBattleInfoNoMission}No mission!".Translate());
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
            int allyTotal = 0;
            int enemyTotal = 0;
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

                // Count all healthy troops in map encounter
                allyTotal = isDefend ? mapEvent.DefenderSide.TroopCount : mapEvent.AttackerSide.TroopCount;
                enemyTotal = isDefend ? mapEvent.AttackerSide.TroopCount : mapEvent.DefenderSide.TroopCount;
            }

            var missionBehavior = BLTAdoptAHeroCommonMissionBehavior.Current;
            if (missionBehavior == null)
            {
                onFailure("{=BLTBattleInfoMissionBehaviorNotFound}Mission behavior not found!".Translate());
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
                string battlestring = "";
                try
                {
                    string playerFaction = (isDefend ? mapEvent.DefenderSide.MapFaction.Name.ToString() : mapEvent.AttackerSide.MapFaction.Name.ToString()); string enemyFaction = (isDefend ? mapEvent.AttackerSide.MapFaction.Name.ToString() : mapEvent.DefenderSide.MapFaction.Name.ToString());
                    battlestring += "{=BLTBattleInfoBattleSummary}{PlayerFaction} vs {EnemyFaction}(P/E):{AllyTotal}({AllyActive})/{EnemyTotal}({EnemyActive}) - ".Translate(
                        ("PlayerFaction", playerFaction), ("EnemyFaction", enemyFaction),
                        ("AllyTotal", allyTotal), ("AllyActive", isDefend ? defendCount : attackCount),
                        ("EnemyTotal", enemyTotal), ("EnemyActive", isDefend ? attackCount : defendCount));
                }
                catch (Exception e) { battlestring += "{=BLTBattleInfoFactionError}Error getting factions - ".Translate(); Log.Trace(e.StackTrace); }


                battlestring += "{=BLTBattleInfoHeroNotInBattleCooldown}Hero is not currently in battle! ({Cooldown}s)".Translate(("Cooldown", cd));

                if (diedInfo.killer != null)
                {                   
                    var weaponClass = (WeaponClass)diedInfo.blow.WeaponClass;
                    string weaponName = weaponClass.ToString();

                    battlestring += "{=BLTBattleInfoKilledBy} | Killed by {Killer} with {Weapon}({Damage})".Translate(
                        ("Killer", diedInfo.killer.Name), ("Weapon", weaponName), ("Damage", diedInfo.blow.InflictedDamage));

                    if (canDie)
                    {
                        float deathMod = GlobalCommonConfig.Get().DeathChance;
                        var deathChance = Campaign.Current.Models.PartyHealingModel.GetSurvivalChance(adoptedHero.PartyBelongedTo.Party, adoptedHero.CharacterObject, diedInfo.blow.DamageType, true);
                        battlestring += "{=BLTBattleInfoDeathChance} | Death chance: {Chance}%".Translate(
                            ("Chance", deathChance * deathMod * 100));
                    }
                        

                }

                onFailure(battlestring);
                return;
            }
            else if (agent == null && MissionHelpers.InTournament())
            {
                onFailure("{=BLTBattleInfoHeroNotInBattle}Hero is not currently in battle!".Translate());
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
            //float currentTime = Mission.Current.CurrentTime;
            bool hasAttacked = /*(currentTime - agent.LastMeleeAttackTime < 10f)
                || (currentTime - agent.LastRangedAttackTime < 10f)
                || (currentTime - agent.LastMeleeHitTime < 10f)
                || (currentTime - agent.LastRangedHitTime < 10f);*/false;


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
            string weaponInfo = "{=BLTBattleInfoUnarmed}Unarmed".Translate();

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
                    ammoInfo = "{=BLTBattleInfoAmmo} - Ammo: {Ammo}/{MaxAmmo}".Translate(("Ammo", ammo), ("MaxAmmo", maxAmmo));
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

                            weaponInfo += "{=BLTBattleInfoAdditionalWeaponAmmo} + {Weapon} ({WeaponType}) - Ammo: {Ammo}/{MaxAmmo}".Translate(
                                ("Weapon", item.Name), ("WeaponType", item.ItemType), ("Ammo", ammo), ("MaxAmmo", maxAmmo));
                            break;
                        }
                }
            }

            string message = "";
            if (!MissionHelpers.InTournament())
            {
                try
                {
                    string playerFaction = (isDefend ? mapEvent.DefenderSide.MapFaction.Name.ToString() : mapEvent.AttackerSide.MapFaction.Name.ToString()); string enemyFaction = (isDefend ? mapEvent.AttackerSide.MapFaction.Name.ToString() : mapEvent.DefenderSide.MapFaction.Name.ToString());
                    message += "{=BLTBattleInfoActiveBattleSummary}{PlayerFaction} vs {EnemyFaction}(P/E):{AllyActive}/{EnemyActive} - ".Translate(
                        ("PlayerFaction", playerFaction), ("EnemyFaction", enemyFaction),
                        ("AllyActive", isDefend ? defendCount : attackCount),
                        ("EnemyActive", isDefend ? attackCount : defendCount));
                }
                catch (Exception e) { message += "{=BLTBattleInfoFactionError}Error getting factions - ".Translate(); Log.Trace(e.StackTrace); }
            }
                
            message += "{=BLTBattleInfoClassAndHealth}Class: {Class}\n- HP: {Health}/{MaxHealth}\n".Translate(
                ("Class", adoptedHero.GetClass()?.Name.ToString() ?? "{=BLTBattleInfoNoClass}No class".Translate()),
                ("Health", (int)agent.Health), ("MaxHealth", (int)agent.HealthLimit));
            if (agent.MountAgent != null)
                message += "{=BLTBattleInfoMountHealth}- Mount HP: {MountHealth}\n".Translate(("MountHealth", mountInfo));

            message += "{=BLTBattleInfoCombatStats}- Weapon: {Weapon}\n- Kills: {Kills}\n- Retinue({Retinue}): {RetinueKills}\n- Gold: {Gold}\n- XP: {XP}\n- Power: {Power}% ".Translate(
                ("Weapon", weaponInfo), ("Kills", state.Kills),
                ("Retinue", state2.ActiveRetinue + state2.ActiveRetinue2), ("RetinueKills", state.RetinueKills),
                ("Gold", state.WonGold), ("XP", state.WonXP), ("Power", $"{ActivePowerFraction(adoptedHero) * 100:0}"));
            if (hasAttacked)
                message += "{=BLTBattleInfoActiveCombat}- Active combat".Translate();

            onSuccess(message);
        }
    }
}
