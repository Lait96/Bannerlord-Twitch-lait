using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using BannerlordTwitch.Rewards;
using BannerlordTwitch.Util;
using TaleWorlds.Library;
using YamlDotNet.Serialization;

#if DEBUG
using System.Runtime.CompilerServices;
#endif

// ReSharper disable MemberCanBePrivate.Global
#pragma warning disable 649

namespace BannerlordTwitch
{
    // Docs here https://dev.twitch.tv/docs/api/reference#create-custom-rewards

    public class Settings : IDocumentable, IUpdateFromDefault
    {
        public ObservableCollection<Reward> Rewards { get; set; } = new();
        [YamlIgnore]
        public IEnumerable<Reward> EnabledRewards => Rewards.Where(r => r.Enabled);
        public ObservableCollection<Command> Commands { get; set; } = new();
        [YamlIgnore]
        public IEnumerable<Command> EnabledCommands => Commands.Where(r => r.Enabled);
        public ObservableCollection<GlobalConfig> GlobalConfigs { get; set; } = new();
        public SimTestingConfig SimTesting { get; set; }
        [YamlIgnore, Browsable(false)]
        public IEnumerable<ActionBase> AllActions => Rewards.Cast<ActionBase>().Concat(Commands);

        public bool DisableAutomaticFulfillment { get; set; }

        public Command GetCommand(string id) =>
            EnabledCommands.FirstOrDefault(c => c.HasName(id))
            ?? EnabledCommands.FirstOrDefault(c => c.HasAlias(id));

        public T GetGlobalConfig<T>(string id) => (T)GlobalConfigs.First(c => c.Id == id).Config;

        private static string DefaultSettingsFileName
            => Path.Combine(Path.GetDirectoryName(typeof(Settings).Assembly.Location) ?? ".",
                "..", "..", "Bannerlord-Twitch-v4.yaml");

        public static Settings DefaultSettings { get; private set; }
        public static int ActiveProfile { get; set; } = 1;
        public static bool GameStarted { get; set; } = false;

#if DEBUG
        private static string ProjectRootDir([CallerFilePath]string file = "") => Path.Combine(Path.GetDirectoryName(file) ?? ".", "..");
        private static string SaveFilePath => Path.Combine(ProjectRootDir(), "_Module", "Bannerlord-Twitch-v4.yaml");
        public static Settings Load()
        {
            
            var settings = YamlHelpers.Deserialize<Settings>(File.ReadAllText(SaveFilePath));
            if (settings == null)
                throw new Exception($"Couldn't load the mod settings from {SaveFilePath}");

            SettingsPostLoad(settings);
            
            return settings;
        }

        public static void Save(Settings settings)
        {
            SettingsPreSave(settings);
            File.WriteAllText(SaveFilePath, YamlHelpers.Serialize(settings));
        }
        public static void ChangeProfile(int Profile)
        {
            ActiveProfile = Profile;
        }
#else

        public static Settings Load()
        {
            Settings settings = null;
            // Try loading settings from the active profile
            try
            {
                PlatformFilePath ProfileFilePath = FileSystem.GetConfigPath($"Bannerlord-Twitch-v4-p{ActiveProfile}.yaml");
                if (FileSystem.FileExists(ProfileFilePath))
                {
                    try
                    {
                        settings = YamlHelpers.Deserialize<Settings>(FileSystem.GetFileContentString(ProfileFilePath));
                        Log.Info($"Settings loaded from {ProfileFilePath}");
                    }
                    catch (Exception ex)
                    {
                        Log.Exception($"Exception loading settings from {ProfileFilePath}: {ex.Message}", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Exception($"Failed to load profile {ActiveProfile}", ex);
            }

            // If we failed to load anything then load defaults
            if (settings == null)
            {
                Log.Info($"Couldn't find existing settings, loading defaults from internal {DefaultSettingsFileName}.");
                settings = YamlHelpers.Deserialize<Settings>(File.ReadAllText(DefaultSettingsFileName));
                if (settings != null)
                    Save(settings);
            }

            // If we STILL haven't loaded anything, then the mod install must be broken
            if (settings == null)
            {
                throw new Exception($"Couldn't load the settings, check the mod is installed correctly!");
            }

            SettingsPostLoad(settings);

            SettingsHelpers.CallInDepth<IUpdateFromDefault>(settings,
                config => config.OnUpdateFromDefault(settings));

            Log.Info($"Settings succesfully applied");

            return settings;
        }

        //public static void ImportOld()
        //{
        //    Settings settings = null;
        //    // Try loading settings from the active profile
        //    try
        //    {
        //        PlatformFilePath ProfileFilePath = FileSystem.GetConfigPath($"Bannerlord-Twitch-v3.yaml");
        //        PlatformFilePath ProfileFilePath2 = FileSystem.GetConfigPath($"Bannerlord-Twitch-v4.yaml");
        //        if (FileSystem.FileExists(ProfileFilePath))
        //        {
        //            try
        //            {
        //                settings = YamlHelpers.Deserialize<Settings>(FileSystem.GetFileContentString(ProfileFilePath));
        //                Log.Info($"Settings imported from {ProfileFilePath}");
        //            }
        //            catch (Exception ex)
        //            {
        //                Log.Exception($"Exception loading settings from {ProfileFilePath}: {ex.Message}", ex);
        //            }
        //        }
        //        else if (FileSystem.FileExists(ProfileFilePath2))
        //        {
        //            try
        //            {
        //                settings = YamlHelpers.Deserialize<Settings>(FileSystem.GetFileContentString(ProfileFilePath2));
        //                Log.Info($"Settings imported from {ProfileFilePath2}");
        //            }
        //            catch (Exception ex)
        //            {
        //                Log.Exception($"Exception loading settings from {ProfileFilePath2}: {ex.Message}", ex);
        //            }
        //        }
        //        else
        //            Log.Info($"No settings found at {ProfileFilePath} or {ProfileFilePath2}");
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.Exception($"Failed to import settings", ex);
        //    }

        //    SettingsHelpers.CallInDepth<IUpdateFromDefault>(settings,
        //        config => config.OnUpdateFromDefault(settings));

        //    SettingsPostLoad(settings);

        //    Log.Info($"Settings succesfully imported");

        //}

        public static void Save(Settings settings)
        {
            SettingsPreSave(settings);
            FileSystem.SaveFileString(FileSystem.GetConfigPath($"Bannerlord-Twitch-v4-p{ActiveProfile}.yaml"), YamlHelpers.Serialize(settings));
            Log.Info($"Settings Saved to Profile {ActiveProfile} at Bannerlord-Twitch-v4-p{ActiveProfile}.yaml");
        }
        public static void ChangeProfile(int Profile)
        {
            ActiveProfile = Profile;
        }
#endif

        private static void SettingsPostLoad(Settings settings)
        {
            settings.Commands ??= new();
            settings.Rewards ??= new();
            settings.GlobalConfigs ??= new();
            settings.SimTesting ??= new();

            ActionManager.ConvertSettings(settings.Commands);
            ActionManager.ConvertSettings(settings.Rewards);
            ActionManager.EnsureGlobalSettings(settings.GlobalConfigs);

            SettingsHelpers.CallInDepth<ILoaded>(settings, config => config.OnLoaded(settings));
        }

        private static void SettingsPreSave(Settings settings)
        {
            SettingsHelpers.CallInDepth<ISaving>(settings, config => config.OnSaving());
        }

        public void GenerateDocumentation(IDocumentationGenerator generator)
        {
            GenerateStarterGuide(generator);

            generator.Div("guide-section commands", () =>
            {
                generator.H1("{=JlFpeaxe}Commands".Translate());
                generator.Table(() =>
                {
                    generator.TR(() => generator
                        .TH("{=15umM0Xo}Command".Translate())
                        .TH("{=BLT_Command_Aliases_Name}Aliases".Translate())
                        .TH("{=J6daarYb}Description".Translate())
                        .TH("{=e2Fu7JYS}Settings".Translate()));
                    foreach (var d in Commands.Where(c => c.Enabled))
                    {
                        generator.TR(() =>
                        {
                            generator.TD("copy-command", $"!{d.Name}");
                            generator.TD(d.AliasList.Any()
                                ? string.Join(", ", d.AliasList.Select(alias => $"!{alias}"))
                                : "—");
                            generator.TD(string.IsNullOrEmpty(d.Documentation.ToString())
                                ? d.Help.ToString()
                                : d.Documentation.ToString());
                            generator.TD(() =>
                            {
                                if (d.HandlerConfig == null) return;

                                generator.Details("entry-details", () =>
                                {
                                    generator.Summary("{=A79HrgZ0}Details".Translate());
                                    if (d.HandlerConfig is IDocumentable doc)
                                        doc.GenerateDocumentation(generator);
                                    else
                                        DocumentationHelpers.AutoDocument(generator, d.HandlerConfig);
                                });
                            });
                        });
                    }
                });
            });
            generator.Br();
            generator.Div("guide-section rewards", () =>
            {
                generator.H1("{=u6xsREDY}Channel Point Rewards".Translate());
                generator.Table(() =>
                {
                    generator.TR(() => generator
                        .TH("{=15umM0Xo}Command".Translate())
                        .TH("{=J6daarYb}Description".Translate())
                        .TH("{=e2Fu7JYS}Settings".Translate()));
                    foreach (var r in Rewards.Where(r => r.Enabled))
                    {
                        generator.TR(() =>
                        {
                            generator.TD(r.RewardSpec.Title.ToString());
                            generator.TD(string.IsNullOrEmpty(r.Documentation.ToString())
                                ? r.RewardSpec.Prompt?.ToString() : r.Documentation.ToString());
                            generator.TD(() =>
                            {
                                if (r.HandlerConfig == null) return;

                                generator.Details("entry-details", () =>
                                {
                                    generator.Summary("{=A79HrgZ0}Details".Translate());
                                    if (r.HandlerConfig is IDocumentable doc)
                                        doc.GenerateDocumentation(generator);
                                    else
                                        DocumentationHelpers.AutoDocument(generator, r.HandlerConfig);
                                });
                            });
                        });
                    }
                });
            });
            generator.Br();
            generator.Div("guide-section global-configs", () =>
            {
                foreach (var g in GlobalConfigs.Select(c => c.Config).OfType<IDocumentable>())
                {
                    g.GenerateDocumentation(generator);
                }
            });
        }

        private void GenerateStarterGuide(IDocumentationGenerator generator)
        {
            string CommandFor(params string[] handlers)
            {
                Command command = EnabledCommands.FirstOrDefault(c => handlers.Contains(c.Handler));
                if (command == null) return null;
                string value = command.Name?.ToString() ?? command.AliasList.FirstOrDefault();
                return string.IsNullOrWhiteSpace(value) ? null : "!" + value.TrimStart('!');
            }

            string Chip(string command, string suffix = null) => command == null
                ? ""
                : $"<button type=\"button\" class=\"starter-command copy-chip\">{WebUtility.HtmlEncode(command + suffix)}</button>";

            string adopt = CommandFor("AdoptAHero");
            string chooseClass = CommandFor("SetHeroClass");
            string summon = CommandFor("SummonHero");
            string power = CommandFor("UsePower");
            string equipment = CommandFor("UpgradeAction");
            string focus = CommandFor("FocusPoints");
            string attributes = CommandFor("AttributePoints");
            string retinue = CommandFor("Retinue", "Retinue2");
            string clan = CommandFor("ClanManagement");
            string kingdom = CommandFor("KingdomManagement");

            generator.Div("starter-guide", () =>
            {
                generator.H2("{=BLTDocs_GettingStarted}Getting started".Translate());
                generator.P("starter-note", "{=BLTDocs_StarterNote}Commands below come from the active profile and can be copied with one click.".Translate());
                generator.Div("newbie-steps", () =>
                {
                    StarterStep("1", "{=BLTDocs_StartHero}Create your hero".Translate(),
                        $"{Chip(adopt)} {"{=BLTDocs_StartHeroText}adopts or creates a viewer hero. Check the Commands section for optional culture and faction variants.".Translate()}");
                    StarterStep("2", "{=BLTDocs_StartClass}Choose a class".Translate(),
                        $"{Chip(chooseClass, " list")} {"{=BLTDocs_StartClassText}shows available classes; repeat the command with a class name to select it. Compare equipment and level powers in the Classes section.".Translate()}");
                    StarterStep("3", "{=BLTDocs_StartBattle}Join battles".Translate(),
                        $"{Chip(summon)} {"{=BLTDocs_StartBattleText}summons your hero when a suitable battle is active. Kills and participation provide progression configured by the streamer.".Translate()} {Chip(power)}");
                    StarterStep("4", "{=BLTDocs_StartProgress}Develop your hero".Translate(),
                        $"{Chip(equipment)} {Chip(focus)} {Chip(attributes)} {"{=BLTDocs_StartProgressText}improve equipment, focus points and attributes when those commands are enabled.".Translate()}");
                    StarterStep("5", "{=BLTDocs_StartRetinue}Build a retinue".Translate(),
                        $"{Chip(retinue)} {"{=BLTDocs_StartRetinueText}opens the retinue actions for recruiting and improving companions.".Translate()}");
                    StarterStep("6", "{=BLTDocs_StartWorld}Join the world".Translate(),
                        $"{Chip(clan)} {Chip(kingdom)} {"{=BLTDocs_StartWorldText}cover clans and kingdoms. These are later-stage systems, so develop the hero first.".Translate()}");
                });
            });

            void StarterStep(string number, string title, string body) => generator.Div("newbie-step", () =>
            {
                generator.P("newbie-step__number", number);
                generator.H3(title);
                generator.P(body);
            });
        }

        #region IUpdateFromDefault
        public void OnUpdateFromDefault(Settings defaultSettings)
        {
            // merge missing actions / rewards / global configs from template
            SettingsHelpers.MergeCollectionsSorted(
                Commands,
                defaultSettings.Commands,
                (s, s2) => s.ID == s2.ID || s.ToString() == s2.ToString(),
                (a, b) => string.Compare(a.ToString(), b.ToString(), StringComparison.CurrentCulture)
            );
            SettingsHelpers.MergeCollectionsSorted(
                Rewards,
                defaultSettings.Rewards,
                (s, s2) => s.ID == s2.ID || s.ToString() == s2.ToString(),
                (a, b) => string.Compare(a.ToString(), b.ToString(), StringComparison.CurrentCulture)
            );
            SettingsHelpers.MergeCollectionsSorted(
                GlobalConfigs,
                defaultSettings.GlobalConfigs,
                (s, s2) => s.Id == s2.Id,
                (a, b) => string.Compare(a.ToString(), b.ToString(), StringComparison.CurrentCulture)
            );
        }
        #endregion
    }
}
