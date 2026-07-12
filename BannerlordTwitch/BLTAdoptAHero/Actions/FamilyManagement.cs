using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using BannerlordTwitch;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using BannerlordTwitch.Helpers;
using BLTAdoptAHero.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero.Actions
{
    [LocDisplayName("{=FamilyMgmt}Family Management"),
     LocDescription("{=FamilyMgmtDesc}Manage and view your hero's family members"),
     UsedImplicitly]
    public class FamilyManagement : HeroCommandHandlerBase
    {
        [CategoryOrder("General", 0)]
        private class Settings : IDocumentable
        {
            // General
            [LocDisplayName("{=BLTFamilyBabyLimit}Baby Command Limit"),
             LocCategory("General", "{=BLTFamilyCategoryGeneral}General"),
             LocDescription("{=BLTFamilyBabyLimitDescription}Maximum number of kids before the baby command is blocked."),
             PropertyOrder(1), UsedImplicitly]
            public int MakeKidsLimit { get; set; } = 3;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {

                generator.Value("{=BLTFamilyDocsOverview}<strong>Family Management:</strong> Manage and view your hero's family members, including spouse, children, and parents.".Translate());
                generator.Value("{=BLTFamilyDocsLimit}<strong>Max Kids From Baby Command:</strong> {limit}".Translate(("limit", MakeKidsLimit)));
                generator.Value("{=BLTFamilyDocsUsage}<strong>Usage:</strong> spouse; spouse rename [name]; spouse looks [body]; spouse baby; spouse skills; children; [childName]; [childName] rename [name]; [childName] looks [body]; [childName] marry [viewer] [viewer_child]; [childName] [grandchildName]".Translate());
                generator.Value("{=BLTFamilyDocsNotes}<strong>Notes:</strong> Names in square brackets are arguments. If children have the same name, add their list number, for example Caladog1 or Caladog2.".Translate());

            }

        }
        public override Type HandlerConfigType => typeof(Settings);

        protected override void ExecuteInternal(Hero adoptedHero, ReplyContext context, object config,
        Action<string> onSuccess, Action<string> onFailure)
        {
            if (config is not Settings settings) return;

            if (adoptedHero == null)
            {
                onFailure(AdoptAHero.NoHeroMessage);
                return;
            }

            if (Mission.Current != null)
            {
                onFailure("{=FamilyMissionActive}You cannot manage your family, as a mission is active!".Translate());
                return;
            }

            if (string.IsNullOrWhiteSpace(context.Args))
            {
                ShowFamilyOverview(adoptedHero, onSuccess);
                return;
            }

            var splitArgs = context.Args.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var command = GetFamilyCommand(splitArgs[0]);

            switch (command)
            {
                case "spouse":
                    HandleSpouseCommand(adoptedHero, splitArgs, onSuccess, onFailure, settings);
                    break;
                case "children":
                    HandleChildListCommand(adoptedHero, onSuccess, onFailure);
                    break;
                case "parents":
                    HandleParentsCommand(adoptedHero, onSuccess, onFailure);
                    break;
                default:
                    HandleNamedMemberCommand(adoptedHero, splitArgs, onSuccess, onFailure);
                    break;
            }
        }

        private static string GetFamilyCommand(string command)
        {
            if (MatchesCommand(command, "{=BLTFamilySubSpouse}spouse".Translate(), "spouse")) return "spouse";
            if (MatchesCommand(command, "{=BLTFamilySubChildren}children".Translate(), "children")) return "children";
            if (MatchesCommand(command, "{=BLTFamilySubParents}parents".Translate(), "parents")) return "parents";
            return command;
        }

        private static string GetMemberCommand(string command)
        {
            if (MatchesCommand(command, "{=BLTFamilySubLooks}looks".Translate(), "looks")) return "looks";
            if (MatchesCommand(command, "{=BLTFamilySubRename}rename".Translate(), "rename")) return "rename";
            if (MatchesCommand(command, "{=BLTFamilySubBaby}baby".Translate(), "baby")) return "baby";
            if (MatchesCommand(command, "{=BLTFamilySubSkills}skills".Translate(), "skills")) return "skills";
            if (MatchesCommand(command, "{=BLTFamilySubMarry}marry".Translate(), "marry")) return "marry";
            return command;
        }

        private static bool MatchesCommand(string value, string localized, string english) =>
            value.Equals(localized, StringComparison.OrdinalIgnoreCase) ||
            value.Equals(english, StringComparison.OrdinalIgnoreCase);

        private void ShowFamilyOverview(Hero adoptedHero, Action<string> onSuccess)
        {
            int spouseCount = (adoptedHero.Spouse != null ? 1 : 0) + adoptedHero.ExSpouses.Count(h=> !h.IsDead);
            int childrenCount = adoptedHero.Children.Count(h => !h.IsDead);
            int grandchildrenCount = adoptedHero.Children.Sum(c => c.Children.Count(h => !h.IsDead));
            int greatCount = adoptedHero.Children.Sum(c => c.Children.Sum(g => g.Children.Count(h => !h.IsDead)));
            int parentCount = ((adoptedHero.Father != null && !adoptedHero.Father.IsDead) ? 1 : 0) + ((adoptedHero.Mother != null && !adoptedHero.Mother.IsDead) ? 1 : 0);
            int siblingCount = adoptedHero.Siblings.Count(h => !h.IsDead);
            int totalFamily = spouseCount + childrenCount + grandchildrenCount + greatCount + parentCount + siblingCount;

            var sb = new StringBuilder();
            sb.Append("{=FamilyOverview}Family Overview: ".Translate());
            sb.Append("{=SpouseCount}Spouses: {count} | ".Translate(("count", spouseCount)));
            sb.Append("{=ChildCount}Children: {count} | ".Translate(("count", childrenCount)));
            if (grandchildrenCount > 0)
                sb.Append("{=GrandchildCount}Grandchildren: {count} | ".Translate(("count", grandchildrenCount)));
            if (greatCount > 0)
                sb.Append("{=BLTFamilyGreatGrandchildCount}Great grandchildren: {count} | ".Translate(("count", greatCount)));
            if (parentCount > 0)
                sb.Append("{=BLTFamilyParentCount}Parents: {count} | ".Translate(("count", parentCount)));
            if (siblingCount > 0)
                sb.Append("{=BLTFamilySiblingCount}Siblings: {count} | ".Translate(("count", siblingCount)));
            sb.Append("{=TotalFamily}Total Family: {count}".Translate(("count", totalFamily)));

            onSuccess(sb.ToString());
        }

        private void HandleSpouseCommand(Hero adoptedHero, string[] args, Action<string> onSuccess, Action<string> onFailure, Settings settings)
        {
            if (args.Length > 1 && GetMemberCommand(args[1]) == "looks")
            {
                if (adoptedHero.Spouse == null)
                {
                    onFailure("{=NoSpouse}You have no spouse".Translate());
                    return;
                }

                if (args.Length < 3)
                {
                    onFailure("{=ProvideLooks}Please provide an appearance string".Translate());
                    return;
                }

                string appearanceArg = string.Join(" ", args.Skip(2));
                ApplyLooks(adoptedHero.Spouse, appearanceArg, onSuccess, onFailure);
                return;
            }

            if (args.Length > 1 && GetMemberCommand(args[1]) == "rename")
            {
                if (adoptedHero.Spouse == null)
                {
                    onFailure("{=NoSpouse}You have no spouse".Translate());
                    return;
                }

                if (args.Length < 3)
                {
                    onFailure("{=ProvideNewName}Please provide a new name".Translate());
                    return;
                }
                string newName = string.Join(" ", args.Skip(2));
                RenameHero(adoptedHero.Spouse, newName, onSuccess, onFailure);
                return;
            }

            if (args.Length > 1 && GetMemberCommand(args[1]) == "baby")
            {
                if (adoptedHero.Spouse == null)
                {
                    onFailure("{=NoSpouse}You have no spouse".Translate());
                    return;
                }

                MakeBaby(adoptedHero, onSuccess, onFailure, settings);
                return;
            }

            if (args.Length > 1 && GetMemberCommand(args[1]) == "skills")
            {
                if (adoptedHero.Spouse == null)
                {
                    onFailure("{=NoSpouse}You have no spouse".Translate());
                    return;
                }
                string skills = ShowSkills(adoptedHero.Spouse);
                onSuccess(skills);
                return;
            }

            if (adoptedHero.Spouse == null)
            {
                if (adoptedHero.ExSpouses.Count > 0)
                {
                    var sB = new StringBuilder();
                    sB.Append("{=BLTFamilyExSpouses}Ex-spouses: ".Translate());

                    var spouses = adoptedHero.ExSpouses.OrderByDescending(c => c.Age).ToList();
                    for (int i = 0; i < spouses.Count; i++)
                    {
                        var exSpouse = spouses[i];
                        sB.Append(CleanName(exSpouse.Name.ToString()));
                        sB.Append($" ({(int)exSpouse.Age}, ");
                        sB.Append(exSpouse.IsFemale ? "{=F}F".Translate() : "{=M}M".Translate());
                        if (exSpouse.Spouse != null)
                            sB.Append(", 💍");
                        if (exSpouse.IsDead)
                            sB.Append(", 💀");
                        if (exSpouse.Children.Count > 0)
                            sB.Append($", 👪:{exSpouse.Children.Count}");
                        sB.Append(")");

                        if (i < spouses.Count - 1)
                        {
                            sB.Append(", ");
                        }
                    }
                    onSuccess(sB.ToString());
                }
                else
                {
                    onFailure("{=NoSpouse}You have no spouse".Translate());
                }
                return;
            }

            var spouse = adoptedHero.Spouse;
            var sb = new StringBuilder();

            sb.Append("{=SpouseInfo}Spouse: ".Translate());
            sb.Append(CleanName(spouse.Name.ToString()));
            sb.Append(" | ");
            sb.Append("{=Age}Age: {age}".Translate(("age", (int)spouse.Age)));
            sb.Append(" | ");
            sb.Append(spouse.IsFemale ? "{=Female}Female".Translate() : "{=Male}Male".Translate());

            if (adoptedHero.IsFemale && adoptedHero.IsPregnant)
            {
                sb.Append(" | {=YouPregnant}You are pregnant".Translate());
            }
            else if (!adoptedHero.IsFemale && spouse.IsPregnant)
            {
                sb.Append(" | {=SpousePregnant}Your spouse is pregnant".Translate());
            }
            var highestSkill = CampaignHelpers.AllSkillObjects
                .OrderByDescending(s => spouse.GetSkillValue(s))
                .FirstOrDefault();
            sb.Append("{=BLTFamilyTopSkill} | Top skill: {skill} {value}".Translate(("skill", SkillXP.GetShortSkillName(highestSkill)), ("value", spouse.GetSkillValue(highestSkill))));

            onSuccess(sb.ToString());
        }

        private void HandleChildListCommand(Hero adoptedHero, Action<string> onSuccess, Action<string> onFailure)
        {
            if (adoptedHero.Children.Count == 0)
            {
                onFailure("{=NoChildren}You have no children".Translate());
                return;
            }

            var sb = new StringBuilder();
            sb.Append("{=ChildrenList}Children: ".Translate());

            var children = adoptedHero.Children.OrderByDescending(c => c.Age).ToList();
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                sb.Append(CleanName(child.Name.ToString()));
                sb.Append($" ({(int)child.Age}, ");
                sb.Append(child.IsFemale ? "{=F}F".Translate() : "{=M}M".Translate());
                if (child.Spouse != null)
                    sb.Append(", 💍");
                if (child.IsDead)
                    sb.Append(", 💀");
                if (child.Children.Count > 0)
                    sb.Append($", 👪:{child.Children.Count}");
                sb.Append(")");

                if (i < children.Count - 1)
                {
                    sb.Append(", ");
                }
            }

            onSuccess(sb.ToString());
        }

        private void HandleParentsCommand(Hero adoptedHero, Action<string> onSuccess, Action<string> onFailure)
        {
            if (adoptedHero.Father == null && adoptedHero.Mother == null)
            {
                onFailure("{=BLTFamilyNoParents}No parents found".Translate());
                return;
            }

            var parents = new StringBuilder("{=BLTFamilyParents}Parents: ".Translate());
            if (adoptedHero.Father != null)
            {
                parents.Append("{=BLTFamilyFather}Father: {name}".Translate(("name", CleanName(adoptedHero.Father.Name.ToString()))));
                if (adoptedHero.Father.IsDead)
                    parents.Append(" 💀");
            }

            if (adoptedHero.Father != null && adoptedHero.Mother != null)
                parents.Append(" | ");

            if (adoptedHero.Mother != null)
            {
                parents.Append("{=BLTFamilyMother}Mother: {name}".Translate(("name", CleanName(adoptedHero.Mother.Name.ToString()))));
                if (adoptedHero.Mother.IsDead)
                    parents.Append(" 💀");
            }

            onSuccess(parents.ToString());
        }

        private void HandleNamedMemberCommand(Hero adoptedHero, string[] args, Action<string> onSuccess, Action<string> onFailure)
        {
            string memberName = args[0];
            int? index = null;

            // Check if name ends with a number (e.g., "John2")
            var match = Regex.Match(memberName, @"^(.+?)(\d+)$");
            if (match.Success)
            {
                memberName = match.Groups[1].Value;
                index = int.Parse(match.Groups[2].Value);
            }

            // Find matching children
            var matchingChildren = adoptedHero.Children
                .Where(c => CleanName(c.Name.ToString()).IndexOf(memberName, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (matchingChildren.Count == 0)
            {
                onFailure("{=NoChildFound}No child found with name '{name}'".Translate(("name", memberName)));
                return;
            }

            if (matchingChildren.Count > 1 && !index.HasValue)
            {
                var sb = new StringBuilder();
                sb.Append("{=MultipleChildren}Multiple children found: ".Translate());
                for (int i = 0; i < matchingChildren.Count; i++)
                {
                    sb.Append($"{CleanName(matchingChildren[i].FirstName.ToString())}{i + 1}");
                    sb.Append($" ({(int)matchingChildren[i].Age}, ");
                    sb.Append(matchingChildren[i].IsFemale ? "{=F}F".Translate() : "{=M}M".Translate());
                    if (matchingChildren[i].Spouse != null)
                        sb.Append(", 💍");
                    if (matchingChildren[i].IsDead)
                        sb.Append(", 💀");
                    if (matchingChildren[i].Children.Count > 0)
                        sb.Append($", 👪:{matchingChildren[i].Children.Count}");
                    sb.Append(")");
                    if (i < matchingChildren.Count - 1) sb.Append(" - ");
                }
                onFailure(sb.ToString());
                return;
            }

            Hero child = index.HasValue && index.Value > 0 && index.Value <= matchingChildren.Count
                ? matchingChildren[index.Value - 1]
                : matchingChildren[0];

            // Check for subcommands
            if (args.Length > 1)
            {
                var subCommand = GetMemberCommand(args[1]);

                switch (subCommand)
                {
                    case "looks":
                        if (args.Length < 3)
                        {
                            onFailure("{=ProvideLooks}Please provide an appearance string".Translate());
                            return;
                        }
                        string appearanceArg = string.Join(" ", args.Skip(2));
                        ApplyLooks(child, appearanceArg, onSuccess, onFailure);
                        break;

                    case "rename":
                        if (args.Length < 3)
                        {
                            onFailure("{=ProvideNewName}Please provide a new name".Translate());
                            return;
                        }
                        string newName = string.Join(" ", args.Skip(2));
                        RenameHero(child, newName, onSuccess, onFailure);
                        break;
                    case "marry":
                        {
                            string[] targets = args.Skip(2).ToArray();
                            MarryHero(adoptedHero, child, targets, onSuccess, onFailure);
                        }
                        break;                      
                    case "skills":
                        { 
                            string skills = ShowSkills(child);
                            onSuccess(skills);
                            return;
                        }

                    default:
                        // Check if it's a grandchild name
                        HandleGrandchildCommand(child, args.Skip(1).ToArray(), onSuccess, onFailure);
                        break;
                }
            }
            else
            {
                ShowChildInfo(child, onSuccess);
            }
        }

        private void HandleGrandchildCommand(Hero parent, string[] args, Action<string> onSuccess, Action<string> onFailure)
        {
            if (parent.Children.Count == 0 && parent.Spouse == null)
            {
                onFailure("{=NoGrandchildren}{parent} has no children and spouse".Translate(("parent", CleanName(parent.Name.ToString()))));
                return;
            }

            string grandchildName = args[0];
            int? index = null;

            var match = Regex.Match(grandchildName, @"^(.+?)(\d+)$");
            if (match.Success)
            {
                grandchildName = match.Groups[1].Value;
                index = int.Parse(match.Groups[2].Value);
            }
            var family = parent.Children.ToList();
            if (parent.Spouse != null)
                family.Insert(0, parent.Spouse);

            var matchingGrandchildren = family
                .Where(c => CleanName(c.Name.ToString()).IndexOf(grandchildName, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            if (matchingGrandchildren.Count == 0)
            {
                onFailure("{=NoGrandchildFound}No grandchild found with name '{name}'".Translate(("name", grandchildName)));
                return;
            }

            if (matchingGrandchildren.Count > 1 && !index.HasValue)
            {
                var sb = new StringBuilder();
                sb.Append("{=MultipleGrandchildren}Multiple grandchildren found: ".Translate());
                for (int i = 0; i < matchingGrandchildren.Count; i++)
                {
                    sb.Append($"{CleanName(matchingGrandchildren[i].Name.ToString())}{i + 1}");
                    if (i < matchingGrandchildren.Count - 1) sb.Append(", ");
                }
                onFailure(sb.ToString());
                return;
            }

            Hero grandchild = index.HasValue && index.Value > 0 && index.Value <= matchingGrandchildren.Count
                ? matchingGrandchildren[index.Value - 1]
                : matchingGrandchildren[0];

            ShowChildInfo(grandchild, onSuccess);
        }

        private void ShowChildInfo(Hero child, Action<string> onSuccess)
        {
            var sb = new StringBuilder();

            sb.Append("{=ChildInfo}Name: {name}".Translate(("name", CleanName(child.Name.ToString()))));
            sb.Append(" | ");
            sb.Append("{=Age}Age: {age}".Translate(("age", (int)child.Age)));
            sb.Append(" | ");
            sb.Append(child.IsFemale ? "{=Female}Female".Translate() : "{=Male}Male".Translate());

            if (child.Clan != null)
            {
                sb.Append(" | ");
                sb.Append("{=Clan}Clan: {clan}".Translate(("clan", child.Clan.Name.ToString())));
            }

            if (child.IsDead)
            {
                sb.Append(" | {=Deceased}DECEASED".Translate());
            }
            if (child.Spouse != null)
            {
                sb.Append(" | ");
                sb.Append("{=Spouse}Spouse: {spouse}".Translate(("spouse", CleanName(child.Spouse.Name.ToString()))));
            }
            var highestSkill = CampaignHelpers.AllSkillObjects
                .OrderByDescending(s => child.GetSkillValue(s))
                .FirstOrDefault();

            sb.Append("{=BLTFamilyTopSkill} | Top skill: {skill} {value}".Translate(("skill", SkillXP.GetShortSkillName(highestSkill)), ("value", child.GetSkillValue(highestSkill))));

            if (child.Children.Count > 0)
            {
                sb.Append(" | ");
                sb.Append("{=Children}Children: ".Translate());
                var children = child.Children.OrderByDescending(c => c.Age).ToList();

                for (int i = 0; i < children.Count; i++)
                {
                    var grandchild = children[i];
                    sb.Append(CleanName(grandchild.FirstName.ToString()));
                    sb.Append($" ({(int)grandchild.Age}, ");
                    sb.Append(grandchild.IsFemale ? "{=F}F".Translate() : "{=M}M".Translate());
                    if (grandchild.Spouse != null)
                        sb.Append(", 💍");
                    if (grandchild.IsDead)
                        sb.Append(", 💀");
                    if (grandchild.Children.Count > 0)
                        sb.Append($", 👪:{child.Children.Count}");
                    sb.Append(")");

                    if (i < children.Count - 1)
                    {
                        sb.Append(", ");
                    }
                }
            }

            onSuccess(sb.ToString());
        }

        private void ApplyLooks(Hero hero, string appearanceArg, Action<string> onSuccess, Action<string> onFailure)
        {
            if (string.IsNullOrEmpty(appearanceArg))
            {
                onFailure("{=ProvideLooks}Please provide an appearance string".Translate());
                return;
            }

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
                onFailure("{=InvalidAppearance}Invalid appearance string format".Translate());
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

            string updatedAppearance = ReplaceAge(appearanceArg, hero.Age);

            BodyProperties updatedBodyProperties = BodyProperties.Default;
            BodyProperties.FromString(updatedAppearance, out updatedBodyProperties);

            bool isFemale = hero.IsFemale;
            int race = hero.CharacterObject?.Race ?? 0;

            hero.CharacterObject.UpdatePlayerCharacterBodyProperties(updatedBodyProperties, race, isFemale);

            onSuccess("{=AppearanceUpdated}Appearance updated for {name}!".Translate(("name", CleanName(hero.Name.ToString()))));
        }

        private void RenameHero(Hero hero, string newName, Action<string> onSuccess, Action<string> onFailure)
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                onFailure("{=ProvideNewName}Please provide a new name".Translate());
                return;
            }

            string oldName = CleanName(hero.Name.ToString());
            var newNameObj = new TextObject(newName);
            hero.SetName(newNameObj, newNameObj);

            onSuccess("{=HeroRenamed}{oldName} has been renamed to {newName}!".Translate(
                ("oldName", oldName),
                ("newName", newName)));

            Log.ShowInformation("{=HeroRenamed}{oldName} has been renamed to {newName}!".Translate(
                ("oldName", oldName),
                ("newName", newName)), hero.CharacterObject);
        }

        private string ShowSkills(Hero hero)
        {
            var stats = new StringBuilder();
            stats.Append($"{"{=fRwyY6ms}[LVL]".Translate()} {hero.Level}");

            var skillsList = CampaignHelpers.AllSkillObjects         
                .OrderByDescending(s => hero.GetSkillValue(s))
                .Select(skill =>
                    $"{SkillXP.GetShortSkillName(skill)} {hero.GetSkillValue(skill)} " +
                    $"[" +
                    $"{"{=lHRDKsUT}f".Translate()}" +
                    $"{hero.HeroDeveloper.GetFocus(skill)}]");

            stats.Append($"{"{=rTId8pBy}[SKILLS]".Translate()} {string.Join(Naming.Sep2, skillsList)}");
            return stats.ToString();
        }

        private Dictionary<(Hero proposer, Hero receiver), (Hero hero1, Hero hero2)> _marriageProposals = new();

        private void MarryHero(Hero adoptedHero, Hero hero, string[] targets, Action<string> onSuccess, Action<string> onFailure)
        {
            if (targets.Length < 2)
            {
                onFailure("{=BLTFamilyMarryUsage}Usage: marry (viewer) (child first name). The viewer who sends the proposal gives their child in marriage.".Translate());
                return;
            }

            Hero adoptedHero1 = Hero.AllAliveHeroes.FirstOrDefault(h => h.Name.ToString().IndexOf(targets[0], StringComparison.OrdinalIgnoreCase) >= 0);

            if (adoptedHero1 == null)
            {
                onFailure("{=BLTFamilyHeroNotFound}Could not find a hero named {name}".Translate(("name", targets[0])));
                return;
            }

            if (MatchesCommand(targets[1], "{=BLTFamilySubReject}reject".Translate(), "reject"))
            {
                if (_marriageProposals.Remove((adoptedHero1, adoptedHero)))
                    onSuccess("{=BLTFamilyProposalRejected}Rejected {hero}'s proposal".Translate(("hero", adoptedHero1.Name)));
                else
                    onFailure("{=BLTFamilyNoProposal}There is no proposal to reject".Translate());
                return;
            }

            Hero target = Hero.AllAliveHeroes.FirstOrDefault(h => h.Name.ToString().IndexOf(targets[1], StringComparison.OrdinalIgnoreCase) >= 0 && (h.Father == adoptedHero1 || h.Mother == adoptedHero1));
            
            if (target == null)
            {
                onFailure("{=BLTFamilyHeroNotFound}Could not find a hero named {name}".Translate(("name", targets[1])));
                return;
            }
            if (hero.Age < 18 || target.Age < 18)
            {
                onFailure("{=BLTFamilyTooYoung}One of the heroes is too young to marry".Translate());
                return;
            }
            if (hero.IsAdopted()|| target.IsAdopted())
            {
                onFailure("{=BLTFamilyCannotMarryBLT}Adopted heroes cannot be married by this command".Translate());
                return;
            }
            if (hero.IsClanLeader || target.IsClanLeader)
            {
                onFailure("{=BLTFamilyCannotMarryLeader}Clan leaders cannot be married by this command".Translate());
                return;
            }

            if (_marriageProposals.TryGetValue((adoptedHero1, adoptedHero), out var pair))
            {
                var h1 = pair.hero1;
                var h2 = pair.hero2;

                if (h1.Spouse != null || h2.Spouse != null)
                {
                    onFailure("{=BLTFamilyAlreadyMarried}One of the heroes is already married".Translate());
                    _marriageProposals.Remove((adoptedHero1, adoptedHero));
                    return;
                }

                var oldClan = h1.Clan;

                h1.Spouse = h2;
                h2.Spouse = h1;
                if (h1.GovernorOf != null)
                {
                    ChangeGovernorAction.RemoveGovernorOf(h1);
                }
                if (h1.PartyBelongedTo != null)
                {
                    var oldParty = h1.PartyBelongedTo;
                    bool wasLeader = oldParty.LeaderHero == h1;
                    oldParty.MemberRoster.RemoveTroop(h1.CharacterObject, 1, default(UniqueTroopDescriptor), 0);
                    MakeHeroFugitiveAction.Apply(h1, false);
                    if (wasLeader && oldParty.IsLordParty)
                        DisbandPartyAction.StartDisband(oldParty);
                }
                h1.Clan = h2.Clan;
                _marriageProposals.Remove((adoptedHero1, adoptedHero));

                var marriageModel = Campaign.Current.Models.MarriageModel;
                h2.UpdateHomeSettlement();
                ChangeRelationAction.ApplyRelationChangeBetweenHeroes(h1, h2, Campaign.Current.Models.MarriageModel.GetEffectiveRelationIncrease(h1, h2), false);

                onSuccess("{=BLTFamilyMarried}{hero1} of {clan1} married {hero2} of {clan2}".Translate(
                    ("hero1", h1.Name), ("clan1", oldClan.Name), ("hero2", h2.Name), ("clan2", h2.Clan.Name)));
                return;
            }
            if (hero.Spouse != null || target.Spouse != null)
            {
                onFailure("{=BLTFamilyInvalidMarriage}The selected marriage is invalid".Translate());
                return;
            }
            else
            {
                _marriageProposals[(adoptedHero, adoptedHero1)] = (hero, target);
                onSuccess("{=BLTFamilyProposalSent}Sent a marriage proposal to {hero}".Translate(("hero", adoptedHero1.Name)));
                return;
            }
        }

        private void MakeBaby(Hero hero, Action<string> onSuccess, Action<string> onFailure, Settings settings)
        {
            int childCount = hero.Children.Where(c => !c.IsDead && c.Clan == hero.Clan).Count();
            if (childCount >= settings.MakeKidsLimit)
            {
                onFailure("{=BLTFamilyBabyLimitReached}You already have {count} living children in your clan; the baby command limit is {limit}".Translate(
                    ("count", childCount), ("limit", settings.MakeKidsLimit)));
                return;
            }
            bool isTarget = hero.IsFemale;
            if (isTarget)
            {
                if (hero.IsPregnant)
                {
                    onFailure("{=BLTFamilyAlreadyPregnant}{hero} is already pregnant.".Translate(("hero", hero.Name)));
                    return;
                }
                else
                {
                    MakePregnantAction.Apply(hero);
                    onSuccess("{=BLTFamilyNowPregnant}{hero} is now pregnant.".Translate(("hero", hero.Name)));
                }
            }
            else
            {
                if (hero.Spouse.IsPregnant)
                {
                    onFailure("{=BLTFamilyAlreadyPregnant}{hero} is already pregnant.".Translate(("hero", hero.Spouse.Name)));
                    return;
                }
                else
                {
                    MakePregnantAction.Apply(hero.Spouse);
                    onSuccess("{=BLTFamilyNowPregnant}{hero} is now pregnant.".Translate(("hero", hero.Spouse.Name)));
                }
            }
        }

        private string CleanName(string name)
        {
            return name.StartsWith("{=") ? name.Substring(name.IndexOf("}") + 1) : name;
        }
    }
}
