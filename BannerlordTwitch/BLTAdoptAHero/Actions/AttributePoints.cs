using System;
using System.Linq;
using BannerlordTwitch;
using BannerlordTwitch.Helpers;
using BannerlordTwitch.Localization;
using BannerlordTwitch.Util;
using JetBrains.Annotations;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace BLTAdoptAHero
{
    [LocDisplayName("{=action_attribute_points_name}Add Attribute Points"),
     LocDescription("{=action_attribute_points_desc}Improve adopted heroes attribute points"),
     UsedImplicitly]
    internal class AttributePoints : ImproveAdoptedHero
    {
        protected class AttributePointsSettings : SettingsBase, IDocumentable
        {
            [LocDisplayName("{=random}Random"),
             LocDescription("{=action_attribute_points_random_desc}If set a random attribute is improved, otherwise the viewer should provide part of the name of the attribute to improve (this works best as a chat command)"),
             PropertyOrder(1), UsedImplicitly]
            public bool Random { get; set; } = true;

            public void GenerateDocumentation(IDocumentationGenerator generator)
            {
                generator.P(Random ?
                    "{=random_attribute}Random attribute".Translate()
                    : "{=action_attribute_points_enter_attribute}Provide the attribute name (or part of it) when calling this".Translate());
                generator.PropertyValuePair("{=amount}Amount".Translate(),
                    AmountLow == AmountHigh
                        ? AmountLow.ToString()
                        : "{=range}{From} to {To}".Translate(
                            ("From", AmountLow), ("To", AmountHigh)));
                if (GoldCost != 0)
                {
                    generator.P("{=action_attribute_points_cost}Costs {GoldCost}".Translate(("GoldCost", GoldCost)) + $"{Naming.Gold}");
                }
            }
        }

        protected override Type ConfigType => typeof(AttributePointsSettings);

        protected override (bool success, string description) Improve(string userName,
            Hero adoptedHero, int amount, SettingsBase baseSettings, string args)
        {
            var settings = (AttributePointsSettings)baseSettings;

            return IncreaseAttribute(adoptedHero, amount, settings.Random, args);
        }

        private static (bool success, string description) IncreaseAttribute(Hero adoptedHero, int amount, bool random,
            string args)
        {
            // Get attributes that can be buffed
            var improvableAttributes = CampaignHelpers.AllAttributes
                .Where(a => adoptedHero.GetAttributeValue(a) < 10)
                .ToList();

            if (!improvableAttributes.Any())
            {
                return (false, "{=action_attribute_points_all_max}Couldn't improve any attributes, they are all at max level!".Translate());
            }

            if (!random && string.IsNullOrEmpty(args))
            {
                return (false, "{=action_attribute_points_need_attribute}Provide the attribute name to improve (or part of it)".Translate());
            }

            // ReSharper disable once RedundantAssignment
            var attribute = CampaignHelpers.DefaultAttribute;
            if (random)
            {
                attribute = improvableAttributes.SelectRandom();
            }
            else
            {
                // We do this because in <=1.5.10 attributes are an enum, which doesn't have a useful default for FirstOrDefault (default is the same as the first enum value)
                if (!CampaignHelpers.AllAttributes
                    .Any(a => CampaignHelpers.GetAttributeName(a).ToLower().Contains(args.ToLower())))
                {
                    return (false, "{=action_attribute_points_not_found}Couldn't find attribute matching '{Args}'!".Translate(("Args", args)));
                }
                attribute = CampaignHelpers.AllAttributes
                    .First(a => CampaignHelpers.GetAttributeName(a).ToLower().Contains(args.ToLower()));
                if (!improvableAttributes.Contains(attribute))
                {
                    return (false, "{=action_attribute_points_already_max}Couldn't improve {Attribute} attribute, it is already at max level!"
                        .Translate(("Attribute", attribute)));
                }
            }

            amount = Math.Min(amount, 10 - adoptedHero.GetAttributeValue(attribute));
            adoptedHero.HeroDeveloper.AddAttribute(attribute, amount, checkUnspentPoints: false);
            return (true,
                    (amount > 1
                        ? "{=action_attribute_points_success_multi}You have gained {Amount} points in {Attribute}, you now have {NewAmount}!"
                        : "{=action_attribute_points_success_single}You have gained a point in {Attribute}, you now have {NewAmount}!")
                    .Translate(
                        ("Amount", amount),
                        ("Attribute", CampaignHelpers.GetAttributeName(attribute)),
                        ("NewAmount", adoptedHero.GetAttributeValue(attribute)))
                );
        }
    }
}
