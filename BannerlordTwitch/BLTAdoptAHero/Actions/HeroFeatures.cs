using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using BLTAdoptAHero.Annotations;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Localization;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;


namespace BLTAdoptAHero.Actions
{
    [LocDisplayName("{=OoGyH5cw}Hero Features"),
     LocDescription("{=Ia7ACrTK}Allow viewer to adjust characteristics about their Hero"),
     UsedImplicitly]
    public class HeroFeatures : HeroCommandHandlerBase
    {
        [CategoryOrder("General", 0)]
        private class Settings : IDocumentable
        {
            [LocDisplayName("{=KjuNxda1}Change Hero Gender Enabled"),
             LocCategory("Gender", "{=1lZ8Vcbc}Gender"),
             LocDescription("{=puULf6Ca}Enable ability to change gender"),
             PropertyOrder(1), UsedImplicitly]
            public bool GenderEnabled { get; set; } = true;

            [LocDisplayName("{=Tt3LPI6w}Change Hero Gender Gold Cost"),
             LocCategory("Gender", "{=1lZ8Vcbc}Gender"),
             LocDescription("{=oyxJVLCx}Cost of changing gender"),
             PropertyOrder(2), UsedImplicitly]
            public int GenderCost { get; set; } = 50000;

            [LocDisplayName("{=jW4WABm2}Only on created heroes?"),
             LocCategory("Gender", "{=1lZ8Vcbc}Gender"),
             LocDescription("{=guSdSDEy}Only allow changing gender for heroes that are created, instead of adopted"),
             PropertyOrder(3), UsedImplicitly]
            public bool GenderDisabledonNative { get; set; } = true;

            [LocDisplayName("{=tlrdxhlh}Hero appearance enabled"),
             LocCategory("Appearance", "{=rnaya1kT}Appearance"),
             LocDescription("{=f1kdzuzz}Allow applying bodyproperty string to your character"),
             PropertyOrder(4), UsedImplicitly]
            public bool AppearanceEnabled { get; set; } = true;

            [LocDisplayName("{=BKial3sf}Hero marriage enabled"),
             LocCategory("Marriage", "{=PUP4VDH3}Marriage"),
             LocDescription("{=SSJOUTxk}Enable ability for heroes to marry"),
             PropertyOrder(5), UsedImplicitly]
            public bool MarriageEnabled { get; set; } = true;

            [LocDisplayName("{=R0Nn6K7Q}Hero marriage gold cost"),
             LocCategory("Marriage", "{=PUP4VDH3}Marriage"),
             LocDescription("{=ySzfRTTr}Cost of marry action"),
             PropertyOrder(6), UsedImplicitly]
            public int MarriageCost { get; set; } = 50000;

            [LocDisplayName("{=AHxka3EX}Only create spouse"),
             LocCategory("Marriage", "{=PUP4VDH3}Marriage"),
             LocDescription("{=n7fZ5jrr}Spawn spouse instead of choosing existing hero"),
             PropertyOrder(7), UsedImplicitly]
            public bool OnlySpawnSpouse { get; set; } = true;

            [LocDisplayName("{=gMbchUTO}Allow clan or name selection"),
             LocCategory("Marriage", "{=PUP4VDH3}Marriage"),
             LocDescription("{=GYzk7nZB}Allow selecting by clan or hero name"),
             PropertyOrder(8), UsedImplicitly]
            public bool ClanorName { get; set; } = false;

            [LocDisplayName("{=BLTHeroFeaturesRaceEnabled}Race command enabled"),
             LocCategory("Race", "{=BLTHeroFeaturesRaceCategory}Race"),
             LocDescription("{=BLTHeroFeaturesRaceEnabledDescription}Enable ability to change race"),
             PropertyOrder(9)]
            public bool RaceEnabled { get; set; } = true;

            [LocDisplayName("{=BLTHeroFeaturesForbiddenRaces}Forbidden races"),
             LocCategory("Race", "{=BLTHeroFeaturesRaceCategory}Race"),
             LocDescription("{=BLTHeroFeaturesForbiddenRacesDescription}List of race IDs that are forbidden. Usage: 0,1,2"),
             PropertyOrder(10)]
            public string ForbiddenRaces { get; set; } = "";

            [LocDisplayName("{=BLTHeroFeaturesCultureEnabled}Culture command enabled"),
             LocCategory("Culture", "{=BLTHeroFeaturesCultureCategory}Culture"),
             LocDescription("{=BLTHeroFeaturesCultureEnabledDescription}Enable ability to change culture"),
             PropertyOrder(11)]
            public bool CultureEnabled { get; set; } = true;

            [LocDisplayName("{=BLTHeroFeaturesForbiddenCultures}Forbidden cultures"),
             LocCategory("Culture", "{=BLTHeroFeaturesCultureCategory}Culture"),
             LocDescription("{=BLTHeroFeaturesForbiddenCulturesDescription}List of cultures that are forbidden. Usage: Vlandia,Battania"),
             PropertyOrder(12)]
            public string ForbiddenCultures { get; set; } = "";

            //[locdisplayname("{=testing}allow viewer marriage"),
            // loccategory("marriage", "{=testing}marriage"),
            // locdescription("{=testing}allow marriage between viewers"),
            // propertyorder(9), usedimplicitly]
            //public bool ViewerAllowed { get; set; } = true;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                var EnabledCommands = new List<string>();
                if (GenderEnabled)
                    EnabledCommands.Add("{=BLTHeroFeaturesGenderCommand}Change Hero Gender".Translate());
                if (AppearanceEnabled)
                    EnabledCommands.Add("{=BLTHeroFeaturesAppearanceCommand}Change Hero Appearance".Translate());
                if (MarriageEnabled)
                    EnabledCommands.Add("{=BLTHeroFeaturesMarriageCommand}Marriage".Translate());
                if (EnabledCommands.Count > 0)
                    generator.Value("{=BLTHeroFeaturesEnabledCommands}<strong>Enabled Commands:</strong> {commands}".Translate(("commands", string.Join(", ", EnabledCommands))));

                if (GenderEnabled)
                    generator.Value("{=BLTHeroFeaturesGenderConfig}<strong>Gender Change Config:</strong> Price={price}{icon}, Only on created heroes?={createdOnly}"
                        .Translate(("price", GenderCost.ToString()), ("icon", Naming.Gold), ("createdOnly", GetLocalizedBoolean(GenderDisabledonNative))));
                if (MarriageEnabled)
                    generator.Value("{=BLTHeroFeaturesMarriageConfig}<strong>Marriage Config:</strong> Price={price}{icon}, Only create spouse?={createOnly}, Allow choose by clan or name?={clanOrName}"
                        .Translate(("price", MarriageCost.ToString()), ("icon", Naming.Gold), ("createOnly", GetLocalizedBoolean(OnlySpawnSpouse)), ("clanOrName", GetLocalizedBoolean(ClanorName))));
            }

            private static string GetLocalizedBoolean(bool value)
                => value
                    ? "{=BLTHeroFeaturesEnabledValue}Enabled".Translate()
                    : "{=BLTHeroFeaturesDisabledValue}Disabled".Translate();

        }

        public override Type HandlerConfigType => typeof(Settings);

        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config, Action<string> onSuccess, Action<string> onFailure)
        {
            if (config is not Settings settings) return;
            //var adoptedHero = BLTAdoptAHeroCampaignBehavior.Current.GetAdoptedHero(context.UserName);
            if (adoptedHero == null)
            {
                onFailure(AdoptAHero.NoHeroMessage);
                return;
            }
            if (Mission.Current != null)
            {
                onFailure("{=EyBgfdPz}You cannot manage your hero, as a mission is active!".Translate());
                return;
            }
            if (adoptedHero.HeroState == Hero.CharacterStates.Prisoner)
            {
                onFailure("{=KIaeC6OH}You cannot manage your hero, as you are a prisoner!".Translate());
                return;
            }
            var splitArgs = context.Args.Split(' ');
            var rawCommand = splitArgs[0];
            var command = GetHeroFeaturesCommand(rawCommand);
            switch (command)
            {
                case ("gender"):
                    if (!settings.GenderEnabled)
                    {
                        onFailure("{=rS4Ykysf}Changing heroes gender is not enabled".Translate());
                        return;
                    }
                    if (splitArgs.Length < 2 || string.IsNullOrWhiteSpace(splitArgs[1]))
                    {
                        onFailure("{=BLTHeroFeaturesInvalidGender}Invalid entry ({male}/{female})"
                            .Translate(("male", "{=BLTHeroFeaturesArgMale}male".Translate()), ("female", "{=BLTHeroFeaturesArgFemale}female".Translate())));
                        return;
                    }
                    if (!BLTAdoptAHeroCampaignBehavior.Current.GetIsCreatedHero(adoptedHero) && settings.GenderDisabledonNative)
                    {
                        onFailure("{=XfKeCtCR}Changing heroes gender is only enabled for created heroes".Translate());
                        return;
                    }
                    if (settings.GenderCost > BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero))
                    {
                        onFailure(Naming.NotEnoughGold(settings.GenderCost, BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero)));
                        return;
                    }
                    if (MatchesCommand(splitArgs[1], "{=BLTHeroFeaturesArgFemale}female".Translate(), "female"))
                    {
                        if (adoptedHero.IsFemale)
                        {
                            onFailure("{=BE1uGwVi}Your hero is already female".Translate());
                            return;
                        }
                        onSuccess("{=kANu9D6d}Your hero has changed their gender to female".Translate());
                        BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.GenderCost);
                        adoptedHero.IsFemale= true;
                        if (adoptedHero.Spouse != null && adoptedHero.Spouse.IsFemale) 
                            adoptedHero.Spouse.IsFemale = false;
                        Log.ShowInformation(
                            "{=byvm3h6C}{Name} has changed their gender to female!".Translate(("Name", adoptedHero.Name)),
                            adoptedHero.CharacterObject);
                        return;
                    }
                    else if (MatchesCommand(splitArgs[1], "{=BLTHeroFeaturesArgMale}male".Translate(), "male"))
                    {
                        if (!adoptedHero.IsFemale)
                        {
                            onFailure("{=aG7rIjnV}Your hero is already male".Translate());
                            return;
                        }
                        if (adoptedHero.IsPregnant)
                        {
                            onFailure("{=BLTHeroFeaturesHeroPregnant}Your hero is pregnant!".Translate());
                            return;
                        }
                        onSuccess("{=FlGjts5K}Your hero has changed their gender to male".Translate());
                        BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.GenderCost);
                        adoptedHero.IsFemale = false;
                        if (adoptedHero.Spouse != null && !adoptedHero.Spouse.IsFemale) 
                            adoptedHero.Spouse.IsFemale = true;
                        Log.ShowInformation(
                            "{=MgcrSo56}{Name} has changed their gender to male!".Translate(("Name", adoptedHero.Name)),
                            adoptedHero.CharacterObject);
                        return;
                    }
                    onFailure("{=BLTHeroFeaturesInvalidGender}Invalid entry ({male}/{female})"
                        .Translate(("male", "{=BLTHeroFeaturesArgMale}male".Translate()), ("female", "{=BLTHeroFeaturesArgFemale}female".Translate())));
                    return;

                case ("looks"):
                    {
                        if (!settings.AppearanceEnabled)
                        {
                            onFailure("{=uaiHAfGa}Changing appearance is disabled".Translate());
                            return;
                        }

                        string appearanceArg = context.Args.Length > rawCommand.Length
                            ? context.Args.Substring(rawCommand.Length).Trim()
                            : null;

                        if (string.IsNullOrEmpty(appearanceArg))
                        {
                            onFailure("{=VuhqWvIK}Please provide an appearance string".Translate());
                            return;
                        }
                        // Validate general format (allow any age, as we'll override it)
                        bool IsValidBodyProperties(string input)
                        {
                            var pattern = @"^<BodyProperties\s+" +
                                          @"version=""4""\s+" +
                                          @"age=""[^""]+""\s+" +
                                          @"weight=""(0\.(0*[1-9]\d*|[1-9]\d*)|1(\.0*)?)""\s+" +
                                          @"build=""(0\.(0*[1-9]\d*|[1-9]\d*)|1(\.0*)?)""\s+" +
                                          @"key=""[0-9A-Fa-f]+""\s*/>$";

                            return Regex.IsMatch(input.Trim(), pattern);
                        }
                        if (!IsValidBodyProperties(appearanceArg))
                        {
                            onFailure("{=CW9BdhhR}Invalid appearance string format".Translate());
                            return;
                        }

                        string ReplaceAge(string input, float age)
                        {
                            return Regex.Replace(
                                input,
                                @"age=""[^""]+""",
                                $"age=\"{age.ToString(System.Globalization.CultureInfo.InvariantCulture)}\""
                            );
                        }

                        string updatedAppearance = ReplaceAge(appearanceArg, adoptedHero.Age);

                        BodyProperties updatedBodyProperties = BodyProperties.Default;
                        BodyProperties.FromString(updatedAppearance, out updatedBodyProperties);

                        bool isFemale = adoptedHero.IsFemale;
                        int race = adoptedHero.CharacterObject?.Race ?? 0;

                        adoptedHero.CharacterObject.UpdatePlayerCharacterBodyProperties(updatedBodyProperties, race, isFemale);

                        Log.ShowInformation("{=GjKCaf8c}Appearance updated successfully!".Translate(), adoptedHero.CharacterObject);
                        return;
                    }

                case ("marry"):
                    {
                        if (!settings.MarriageEnabled)
                        {
                            onFailure("{=5LxKguGE}Hero marriage is not enabled".Translate());
                            return;
                        }
                        if (adoptedHero.Occupation != Occupation.Lord)
                        {
                            onFailure("{=BLTHeroFeaturesNotNoble}Your hero is not a noble".Translate());
                            return;
                        }
                        if (adoptedHero.Spouse != null)
                        {
                            onFailure("{=42LsY2qb}You are already married".Translate());
                            return;
                        }
                        if (adoptedHero.Clan == null)
                        {
                            onFailure("{=zXegzi8c}You are not in a clan".Translate());
                            return;
                        }
                        if (settings.MarriageCost > BLTAdoptAHeroCampaignBehavior.Current.GetHeroGold(adoptedHero))
                        {
                            onFailure("{=avHpPxTN}You do not have enough gold ({price}) to marry".Translate(("price", settings.MarriageCost.ToString())));
                            return;
                        }

                        string spouseArg = context.Args.Length > 1 ? context.Args.Substring(rawCommand.Length).Trim() : "";

                        string CleanName(string name)
                        {
                            return name.StartsWith("{=") ? name.Substring(name.IndexOf("}") + 1) : name;
                        }

                        //Hero SpawnSpouse(string cultureArg = null)
                        //{
                        //    bool fallback = false;
                        //    var cultureToSpawn = !string.IsNullOrEmpty(cultureArg)
                        //        ? CampaignHelpers.MainCultures.FirstOrDefault(c => c.Name.ToString().StartsWith(cultureArg, StringComparison.CurrentCultureIgnoreCase))
                        //        : adoptedHero.Culture;

                        //    if (cultureToSpawn == null)
                        //        cultureToSpawn = CampaignHelpers.MainCultures.FirstOrDefault();

                        //    var character = CampaignHelpers.GetWandererTemplates(cultureToSpawn).SelectRandom();
                        //    if (character == null)
                        //    {
                        //        character = CampaignHelpers.AllWandererTemplates.SelectRandom();
                        //        fallback = true;
                        //    }                                

                        //    if (character == null)
                        //    {
                        //        onFailure("{=cfkeeEB0}Failed to find a character template to spawn".Translate());
                        //        return null;
                        //    }

                        //    var newHero = HeroCreator.CreateSpecialHero(character);
                        //    newHero.ChangeState(Hero.CharacterStates.Active);
                        //    BLTAdoptAHeroCampaignBehavior.Current.SetIsCreatedHero(newHero, true);

                        //    var towns = Settlement.All.Where(s => s.IsTown).ToList();

                        //    Settlement heroSettlement = adoptedHero.LastKnownClosestSettlement;
                        //    Settlement targetSettlement = null;

                        //    if (heroSettlement != null)
                        //    {
                        //        var heroPos = heroSettlement.Position;
                        //        targetSettlement = towns.OrderBy(town => town.Position.DistanceSquared(heroPos)).FirstOrDefault();
                        //    }
                        //    targetSettlement ??= towns.SelectRandom();

                        //    if (targetSettlement != null)
                        //        EnterSettlementAction.ApplyForCharacterOnly(newHero, targetSettlement);
                        //    else
                        //        Log.Error("No suitable settlement found to place new hero");

                        //    newHero.SetNewOccupation(Occupation.Lord);
                        //    newHero.Clan = adoptedHero.Clan;

                        //    var randAge = new Random();
                        //    newHero.SetBirthDay(CampaignTime.YearsFromNow(-Math.Max(Campaign.Current.Models.AgeModel.HeroComesOfAge, adoptedHero.Age + randAge.Next(-3, +3))));

                        //    if (adoptedHero.IsFemale == newHero.IsFemale)
                        //    {
                        //        newHero.IsFemale = !newHero.IsFemale;
                        //    }
                        //    // Now randomize and assign the name based on new gender
                        //    bool isFemale = newHero.IsFemale;

                        //    TextObject objectName = isFemale
                        //    ? cultureToSpawn.FemaleNameList.SelectRandom()
                        //    : cultureToSpawn.MaleNameList.SelectRandom();

                        //    string rawName = objectName.ToString();
                        //    string oldName = newHero.Name.ToString();

                        //    CampaignHelpers.SetHeroName(newHero, objectName, objectName);
                        //    if (fallback)
                        //        newHero.Culture = cultureToSpawn;
                        //    return newHero;
                        //}

                        if (settings.OnlySpawnSpouse)
                        {
                            if (string.IsNullOrEmpty(spouseArg))
                            {
                                onFailure("{=OwlH8hKI}Please specify a culture to spawn spouse from".Translate());
                                return;
                            }
                            var cultureSpouse = !string.IsNullOrEmpty(spouseArg)
                                ? CampaignHelpers.MainCultures
                                .FirstOrDefault(c => c.Name.ToString().IndexOf(spouseArg, StringComparison.OrdinalIgnoreCase) >= 0)
                                : adoptedHero.Culture;

                            var newHero = SpawnSpouse(adoptedHero, cultureSpouse);
                            if (newHero == null)
                                return;

                            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.MarriageCost);
                            onSuccess("{=JW5L4lvt}Marriage successful with spawned spouse".Translate());
                            Log.ShowInformation("{=h6AHfoVx}{heroName} has married {spouseName}!".Translate(("heroName", adoptedHero.Name.ToString()), ("spouseName", CleanName(newHero.Name.ToString()))),
                                adoptedHero.Spouse.CharacterObject, Log.Sound.Horns2);
                            return;
                        }
                        else
                        {
                            string StripTranslationKey(string str)
                            {
                                if (string.IsNullOrEmpty(str)) return str;

                                var match = System.Text.RegularExpressions.Regex.Match(str, @"^\{=.*?\}(.*)$");
                                if (match.Success)
                                    return match.Groups[1].Value.Trim();

                                return str;
                            }

                            IEnumerable<Hero> candidates = CampaignHelpers.AliveHeroes.Where(n =>
                                (n.Name != null && (!StripTranslationKey(n.Name.ToString()).Contains(BLTAdoptAHeroModule.Tag) || !StripTranslationKey(n.Name.ToString()).Contains(BLTAdoptAHeroModule.DevTag))) &&
                                (n.Spouse == null) &&
                                (adoptedHero.IsFemale != n.IsFemale));

                            Func<Hero, bool> universalFilters = n =>
                                n.Occupation == Occupation.Lord &&
                                !n.IsHumanPlayerCharacter &&
                                !n.IsPlayerCompanion &&
                                n.Age >= Campaign.Current.Models.AgeModel.HeroComesOfAge &&
                                !n.Clan.Name.ToString().Contains("[BLT Clan]");

                            candidates = candidates.Where(universalFilters);
                            bool ClanorName = false;

                            if (!string.IsNullOrEmpty(spouseArg))
                            {
                                string argLower = spouseArg.ToLowerInvariant().Trim();
                                

                                if (settings.ClanorName)
                                {
                                    var clanOrNameMatches = candidates.Where(n =>
                                    {
                                        string clanName = n.Clan?.Name.ToString() ?? "";
                                        string heroName = StripTranslationKey(n.Name.ToString());

                                        bool clanMatch = clanName.StartsWith(spouseArg, StringComparison.CurrentCultureIgnoreCase) && !clanName.Contains("[BLT Clan]");
                                        bool nameMatch = heroName.StartsWith(spouseArg, StringComparison.CurrentCultureIgnoreCase) && (n.Clan != null || !n.Clan.Name.ToString().Contains("[BLT Clan]"));

                                        ClanorName = true;
                                        return clanMatch || nameMatch;
                                    });

                                    if (!clanOrNameMatches.Any())
                                    {
                                        var cultureMatch = CampaignHelpers.MainCultures.FirstOrDefault(c =>
                                            c.Name.ToString().StartsWith(spouseArg, StringComparison.CurrentCultureIgnoreCase));
                                        if (cultureMatch == null)
                                        {
                                            onFailure("{=Hh87fxvK}No clan, name or culture starting with '{Text}' found".Translate(("Text", spouseArg)));
                                            return;
                                        }

                                        candidates = candidates.Where(n => n.Culture == cultureMatch);

                                        if (adoptedHero.Clan?.Kingdom != null)
                                        {
                                            var sameKingdom = candidates.Where(n => n.Clan?.Kingdom == adoptedHero.Clan.Kingdom);
                                            candidates = sameKingdom.Any() ? sameKingdom : candidates;
                                        }
                                    }
                                    else
                                    {
                                        candidates = clanOrNameMatches;
                                    }
                                }
                                else
                                {
                                    var spouseCulture = CampaignHelpers.MainCultures.FirstOrDefault(c =>
                                        c.Name.ToString().StartsWith(spouseArg, StringComparison.CurrentCultureIgnoreCase));
                                    if (spouseCulture == null)
                                    {
                                        onFailure("{=CCYS1Sb0}No culture starting with '{Text}' found".Translate(("Text", spouseArg)));
                                        return;
                                    }
                                    candidates = candidates.Where(n => n.Culture == spouseCulture);

                                    if (adoptedHero.Clan?.Kingdom != null)
                                    {
                                        var sameKingdom = candidates.Where(n => n.Clan?.Kingdom == adoptedHero.Clan.Kingdom);
                                        candidates = sameKingdom.Any() ? sameKingdom : candidates;
                                    }
                                }
                            }

                            var spouse = candidates.SelectRandom();

                            if (spouse == null && ClanorName == false)
                            {
                                var newHero = SpawnSpouse(adoptedHero, adoptedHero.Culture);
                                if (newHero == null) return;

                                BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.MarriageCost);
                                onSuccess("{=AMBu22hn}No valid spouse found, spawned fallback spouse successfully".Translate());
                                Log.ShowInformation("{=h6AHfoVx}{heroName} has married {spouseName}!".Translate(("heroName", adoptedHero.Name.ToString()), ("spouseName", CleanName(newHero.Name.ToString()))),
                                    adoptedHero.CharacterObject, Log.Sound.Horns2);
                                return;
                            }
                            if (spouse == null && ClanorName == true)
                            {
                                onFailure("{=GlvwEhly}No available hero with '{Text}' clan or name found".Translate(("Text", spouseArg)));
                                return;
                            }

                            adoptedHero.Spouse = spouse;
                            spouse.Spouse = adoptedHero;
                            spouse.Clan = adoptedHero.Clan;
                            var randAge2 = new Random();
                            spouse.SetBirthDay(CampaignTime.YearsFromNow(-Math.Max(Campaign.Current.Models.AgeModel.HeroComesOfAge, adoptedHero.Age + randAge2.Next(-3, +3))));

                            BLTAdoptAHeroCampaignBehavior.Current.ChangeHeroGold(adoptedHero, -settings.MarriageCost);
                            onSuccess("{=asmoKHI6}Marriage successful".Translate());
                            Log.ShowInformation("{=h6AHfoVx}{heroName} has married {spouseName}!".Translate(("heroName", adoptedHero.Name.ToString()), ("spouseName", CleanName(spouse.Name.ToString()))),
                                adoptedHero.Spouse.CharacterObject, Log.Sound.Horns2);

                            return;
                        }
                    }
                case "race":
                    {
                        if (!settings.RaceEnabled)
                        {
                            onFailure("{=BLTHeroFeaturesRaceDisabled}Race command is disabled".Translate());
                            return;
                        }
                        var forbiddenRaces = settings.ForbiddenRaces
                            .Split(',')
                            .Select(s => s.Trim())
                            .Where(s => int.TryParse(s, out _))
                            .Select(int.Parse)
                            .ToHashSet();

                        var validRaces = Enumerable.Range(0, 32)
                            .Select(r => (RaceId: r, Monster: TaleWorlds.Core.FaceGen.GetBaseMonsterFromRace(r)))
                            .Where(x => x.Monster != null && !forbiddenRaces.Contains(x.RaceId))
                            .ToList();

                        if (splitArgs.Length < 2 || !int.TryParse(splitArgs[1], out int race) || !validRaces.Any(x => x.RaceId == race))
                        {
                            string list = string.Join(", ", validRaces.Select(x => $"{x.RaceId} ({x.Monster.StringId})"));
                            onFailure("{=BLTHeroFeaturesValidRaces}Valid races: {races}".Translate(("races", list)));
                            return;
                        }

                        BodyProperties newBody = adoptedHero.CharacterObject.GetBodyPropertiesMin();
                        adoptedHero.CharacterObject.UpdatePlayerCharacterBodyProperties(newBody, race, adoptedHero.IsFemale);
                        onSuccess("{=BLTHeroFeaturesRaceChanged}Hero race set to {race} ({raceName})".Translate(("race", race.ToString()), ("raceName", TaleWorlds.Core.FaceGen.GetBaseMonsterFromRace(race)?.StringId)));
                        return;
                    }
                case "culture":
                    {
                        if (!settings.CultureEnabled)
                        {
                            onFailure("{=BLTHeroFeaturesCultureDisabled}Culture command is disabled".Translate());
                            return;
                        }
                        var forbidden = settings.ForbiddenCultures
                            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s.Trim())
                            .ToHashSet(StringComparer.OrdinalIgnoreCase);
                        var allowedCultures = CampaignHelpers.MainCultures
                            .Where(c => !forbidden.Contains(c.StringId))
                            .ToList();


                        if (splitArgs.Length < 2)
                        {
                            onFailure("{=BLTHeroFeaturesSelectCulture}Select a culture".Translate());
                            return;
                        }
                        string value = string.Join(" ", splitArgs.Skip(1));
                        var cult = allowedCultures.FirstOrDefault(c => c.Name.ToString().ToLower() == value.ToLower());
                        if (cult == null)
                        {
                            onFailure("{=BLTHeroFeaturesCultureNotFound}No culture named {culture}".Translate(("culture", value)));
                            return;
                        }

                        adoptedHero.Culture = cult;
                        adoptedHero.Clan.Culture = cult;
                        onSuccess("{=BLTHeroFeaturesCultureChanged}Changed culture to {culture}".Translate(("culture", cult.Name)));

                        break;
                    }
                default:
                    onFailure("{=6t9UWDR2}Invalid action".Translate());
                    return;
            }
        }

        private static string GetHeroFeaturesCommand(string command)
        {
            if (MatchesCommand(command, "{=BLTHeroFeaturesSubGender}gender".Translate(), "gender")) return "gender";
            if (MatchesCommand(command, "{=BLTHeroFeaturesSubLooks}looks".Translate(), "looks")) return "looks";
            if (MatchesCommand(command, "{=BLTHeroFeaturesSubMarry}marry".Translate(), "marry")) return "marry";
            if (MatchesCommand(command, "{=BLTHeroFeaturesSubRace}race".Translate(), "race")) return "race";
            if (MatchesCommand(command, "{=BLTHeroFeaturesSubCulture}culture".Translate(), "culture")) return "culture";
            return command?.ToLowerInvariant() ?? "";
        }

        private static bool MatchesCommand(string value, string localized, string english)
            => value.Equals(english, StringComparison.OrdinalIgnoreCase)
               || value.Equals(localized, StringComparison.OrdinalIgnoreCase);

        public static Hero SpawnSpouse(Hero adoptedHero, CultureObject cultureArg)
        {
            bool fallback = false;

            var cultureToSpawn = cultureArg;

            cultureToSpawn ??= CampaignHelpers.MainCultures.FirstOrDefault();

            var character = CampaignHelpers
                .GetWandererTemplates(cultureToSpawn)
                .SelectRandom();

            if (character == null)
            {
                character = CampaignHelpers.AllWandererTemplates.SelectRandom();
                fallback = true;
            }

            if (character == null)
            {
                Log.Info("{=cfkeeEB0}Failed to find a character template to spawn".Translate());
                return null;
            }

            var newHero = HeroCreator.CreateSpecialHero(character);
            if (newHero == null)
                return null;
            newHero.ChangeState(Hero.CharacterStates.Active);

            BLTAdoptAHeroCampaignBehavior.Current.SetIsCreatedHero(newHero, true);

            var towns = Settlement.All.Where(s => s.IsTown).ToList();
            var targetSettlement = adoptedHero.LastKnownClosestSettlement != null
                ? towns.OrderBy(t => t.Position.DistanceSquared(
                    adoptedHero.LastKnownClosestSettlement.Position)).FirstOrDefault()
                : towns.SelectRandom();

            if (targetSettlement != null)
                EnterSettlementAction.ApplyForCharacterOnly(newHero, targetSettlement);

            newHero.SetNewOccupation(Occupation.Lord);
            newHero.Clan = adoptedHero.Clan;

            var rand = new Random();
            newHero.SetBirthDay(
                CampaignTime.YearsFromNow(
                    -Math.Max(
                        Campaign.Current.Models.AgeModel.HeroComesOfAge,
                        adoptedHero.Age + rand.Next(-3, 3))));

            if (adoptedHero.IsFemale == newHero.IsFemale)
                newHero.IsFemale = !newHero.IsFemale;

            TextObject name = newHero.IsFemale
                ? cultureToSpawn.FemaleNameList.SelectRandom()
                : cultureToSpawn.MaleNameList.SelectRandom();

            CampaignHelpers.SetHeroName(newHero, name, name);
            //clean names
            CampaignHelpers.SetHeroName(newHero, newHero.FirstName, newHero.FirstName);

            if (fallback)
                newHero.Culture = cultureToSpawn;

            if (newHero != null)
            {
                adoptedHero.Spouse = newHero;
                newHero.Spouse = adoptedHero;
            }
            
            return newHero;
        }

    }
}
