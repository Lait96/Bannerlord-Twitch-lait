using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using BannerlordTwitch.Util;
using Path = System.IO.Path;

namespace BannerlordTwitch
{
    public class DocumentationGenerator : IDocumentationGenerator
    {
        private int anchor;
        private readonly string idPrefix;
        private readonly bool englishFallback;
        private readonly List<string> content = new();
        private readonly List<string> starterContent = new();
        private List<string> activeContent;
        private List<string> Output => activeContent ?? content;

        public DocumentationGenerator(string idPrefix = "guide", bool englishFallback = false)
        {
            this.idPrefix = idPrefix;
            this.englishFallback = englishFallback;
        }

        private const string CSSFileName = "Bannerlord-Twitch-Documentation.css";
        private const string ScriptFileName = "Bannerlord-Twitch-Documentation.js";
        private static string ModuleFilePath(string fileName) => Path.Combine(
            Path.GetDirectoryName(typeof(DocumentationGenerator).Assembly.Location) ?? ".", "..", "..", fileName);

        public async Task Document(IDocumentable documentable)
        {
            await MainThreadSync.RunWaitAsync(() =>
            {
                if (!englishFallback)
                {
                    documentable.GenerateDocumentation(this);
                    return;
                }

                using (Localization.LocString.UseEnglishFallback())
                    documentable.GenerateDocumentation(this);
            });
        }

        public static string DocumentationRootDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Mount and Blade II Bannerlord",
            "Configs", "BLT-documentation");

        public static string DocumentationPath => Path.Combine(DocumentationRootDir, "index.html");

        public async Task SaveAsync(string title, string introduction, DocumentationGenerator english = null,
            bool currentIsRussian = false, string currentLanguage = null)
        {
            await MainThreadSync.RunWaitAsync(() =>
            {
                var page = new List<string>
                {
                    "<!DOCTYPE html>",
                    $"<html lang=\"{(currentIsRussian ? "ru" : "en")}\">",
                    "<head>",
                    "<meta charset=\"utf-8\"/>",
                    "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"/>",
                    $"<title>{WebUtility.HtmlEncode(title)}</title>",
                    "<link rel=\"stylesheet\" href=\"style.css\">",
                    "</head>",
                    "<body>"
                };
                if (english != null)
                    page.Add($"<div class=\"language-switcher\"><button data-language-select=\"current\" class=\"is-active\">{WebUtility.HtmlEncode(currentLanguage ?? (currentIsRussian ? "Русский" : "Current"))}</button><button data-language-select=\"english\">English</button></div>");
                page.AddRange(BuildLanguagePage(title, introduction, currentIsRussian, "current", false));
                if (english != null)
                    page.AddRange(english.BuildLanguagePage("Bannerlord Twitch Viewer Guide", introduction, false, "english", true));
                page.Add("<script src=\"guide.js\"></script></body></html>");

                Directory.CreateDirectory(DocumentationRootDir);
                foreach (string obsoleteImage in Directory.GetFiles(DocumentationRootDir, "blt_img_*.png"))
                    File.Delete(obsoleteImage);
                File.WriteAllLines(DocumentationPath, page);
                CopyModuleFile(CSSFileName, "style.css");
                CopyModuleFile(ScriptFileName, "guide.js");
            });
        }

        private IEnumerable<string> BuildLanguagePage(string title, string introduction, bool russian,
            string language, bool hidden)
        {
            string Text(string en, string ru) => russian ? ru : en;
            yield return $"<div class=\"language-page{(hidden ? " is-language-hidden" : "")}\" data-guide-language=\"{language}\" data-ui-language=\"{(russian ? "ru" : "en")}\">";
            yield return "<header class=\"guide-header\"><div class=\"guide-header__inner\">";
            yield return "<span class=\"guide-kicker\">BLT · Bannerlord Twitch</span>";
            yield return $"<h1>{WebUtility.HtmlEncode(title)}</h1><p>{WebUtility.HtmlEncode(introduction)}</p>";
            yield return $"<a class=\"developer-guide-link\" href=\"https://lait96.github.io/Bannerlord-Twitch-lait/\" target=\"_blank\" rel=\"noopener noreferrer\"><span>{Text("Developer documentation", "Документация разработчика")}</span><strong>{Text("How every command works", "Как работает каждая команда")} →</strong></a>";
            yield return "</div></header><main class=\"content\">";
            foreach (string line in starterContent) yield return line;
            yield return "<section class=\"guide-search-panel\">";
            yield return $"<label class=\"guide-search\"><span>{Text("Search the guide", "Поиск по гайду")}</span><input type=\"search\" data-guide-search placeholder=\"{Text("Command, alias, reward or setting…", "Команда, синоним, награда или настройка…")}\" autocomplete=\"off\"/></label>";
            yield return "<p class=\"guide-search-status\" data-guide-search-status aria-live=\"polite\"></p>";
            yield return $"<div class=\"guide-filters\" role=\"group\" aria-label=\"{Text("Guide sections", "Разделы гайда")}\"><button class=\"is-active\" type=\"button\" data-guide-filter=\"all\">{Text("All", "Все")}</button><button type=\"button\" data-guide-filter=\"commands\">{Text("Commands", "Команды")}</button><button type=\"button\" data-guide-filter=\"rewards\">{Text("Rewards", "Награды")}</button><button type=\"button\" data-guide-filter=\"classes\">{Text("Classes", "Классы")}</button><button type=\"button\" data-guide-filter=\"settings\">{Text("Settings", "Настройки")}</button><button type=\"button\" data-guide-filter=\"streaks\">{Text("Kill streaks", "Серии убийств")}</button><button type=\"button\" data-guide-filter=\"achievements\">{Text("Achievements", "Достижения")}</button><button type=\"button\" data-guide-filter=\"map\">{Text("Map", "Карта")}</button></div>";
            yield return "</section><div class=\"filterable-guide-content\">";
            foreach (string line in content) yield return line;
            yield return "</div></main></div>";
        }

        private static void CopyModuleFile(string sourceName, string targetName)
        {
            string targetPath = Path.Combine(DocumentationRootDir, targetName);
            if (File.Exists(targetPath)) File.Delete(targetPath);
            File.Copy(ModuleFilePath(sourceName), targetPath);
        }

        // public void SavePdf()
        // {
        //     //var cssData = PdfGenerator.ParseStyleSheet(File.ReadAllText(CSSFullPath));
        //     Save();
        //     
        //     var pdf = PdfGenerator.GeneratePdf(string.Join("\n", docs), 
        //         new PdfGenerateConfig
        //         {
        //             //PageSize = PageSize.A0,
        //             ManualPageSize = XSize.FromSize(new (1000, 4000))
        //         },
        //         //cssData: cssData,
        //         stylesheetLoad: (sender, args) =>
        //         {
        //             args.SetStyleSheetData = PdfGenerator.ParseStyleSheet(
        //                 File.ReadAllText(Path.Combine(DocumentationRootDir, args.Src))
        //                 );
        //         }, 
        //         imageLoad: (sender, args) =>
        //         {
        //             args.Callback(Path.Combine(DocumentationRootDir, args.Src));
        //         });
        //
        //     pdf.Save(Path.Combine(DocumentationRootDir, "blt-docs.pdf"));
        // }

        // private static string LinkToAnchor(string text, string anchorTag = null)
        //     => $"<a href=\"#{text}{anchorTag ?? ""}\">{text}</a>";
        //
        // private static string MakeAnchor(string text, string anchorTag = null)
        //     => $"<a name=\"{text}{anchorTag ?? ""}\">{text}</a>";

        private IDocumentationGenerator ScopedTag(string tag, string css, Action content)
        {
            Output.Add(css != null ? $"<{tag} class=\"{css}\">" : $"<{tag}>");
            content();
            Output.Add($"</{tag}>");
            return this;
        }

        private IDocumentationGenerator Tag(string tag, string css, string content)
        {
            Output.Add(
                css != null
                    ? $"<{tag} class=\"{css}\">{content}</{tag}>"
                    : $"<{tag}>{content}</{tag}>"
                );
            return this;
        }

        public IDocumentationGenerator Div(string css, Action content)
        {
            if (css != "starter-guide") return ScopedTag("div", css, content);
            List<string> previous = activeContent;
            activeContent = starterContent;
            try { return ScopedTag("section", css, content); }
            finally { activeContent = previous; }
        }
        public IDocumentationGenerator Div(Action content) => Div(null, content);

        public IDocumentationGenerator Details(string css, Action content) => ScopedTag("details", css, content);
        public IDocumentationGenerator Details(Action content) => Details(null, content);

        public IDocumentationGenerator Summary(string css, Action content) => ScopedTag("summary", css, content);
        public IDocumentationGenerator Summary(Action content) => Summary(null, content);
        public IDocumentationGenerator Summary(string css, string content) => Tag("summary", css, content);
        public IDocumentationGenerator Summary(string content) => Summary(null, content);

        public IDocumentationGenerator H1(string css, string content)
        {
            string id = $"{idPrefix}-section-{++anchor}";
            Output.Add(css == null
                ? $"<h1 id=\"{id}\">{content}</h1>"
                : $"<h1 id=\"{id}\" class=\"{css}\">{content}</h1>");
            return this;
        }

        public IDocumentationGenerator H1(string content) => H1(null, content);

        public IDocumentationGenerator H2(string css, string content)
        {
            string id = $"{idPrefix}-section-{++anchor}";
            Output.Add(css == null
                ? $"<h2 id=\"{id}\">{content}</h2>"
                : $"<h2 id=\"{id}\" class=\"{css}\">{content}</h2>");
            return this;
        }

        public IDocumentationGenerator H2(string content) => H2(null, content);

        public IDocumentationGenerator H3(string css, string content)
        {
            string id = $"{idPrefix}-section-{++anchor}";
            Output.Add(css == null
                ? $"<h3 id=\"{id}\">{content}</h3>"
                : $"<h3 id=\"{id}\" class=\"{css}\">{content}</h3>");
            return this;
        }

        public IDocumentationGenerator H3(string content) => H3(null, content);

        public IDocumentationGenerator Table(string css, Action content, bool collapsible = false, string summary = "")
        {
            if (!collapsible)
                return ScopedTag("table", css, content);

            return Details(() =>
            {
                Summary(summary);
                ScopedTag("table", css, content);
            });
        }
        public IDocumentationGenerator Table(Action content, bool collapsible = false, string summary = "")
        {
            return Table(null, content, collapsible, summary);
        }

        public IDocumentationGenerator TR(string css, Action content) => ScopedTag("tr", css, content);
        public IDocumentationGenerator TR(Action content) => TR(null, content);
        public IDocumentationGenerator TR(string css, string content) => Tag("tr", css, content);
        public IDocumentationGenerator TR(string content) => TR(null, content);

        public IDocumentationGenerator TH(string css, Action content) => ScopedTag("th", css, content);
        public IDocumentationGenerator TH(Action content) => TH(null, content);
        public IDocumentationGenerator TH(string css, string content) => Tag("th", css, content);
        public IDocumentationGenerator TH(string content) => TH(null, content);

        public IDocumentationGenerator TD(string css, Action content) => ScopedTag("td", css, content);
        public IDocumentationGenerator TD(Action content) => TD(null, content);
        public IDocumentationGenerator TD(string css, string content) => Tag("td", css, content);
        public IDocumentationGenerator TD(string content) => TD(null, content);

        public IDocumentationGenerator P(string css, string content) => Tag("p", css, content);
        public IDocumentationGenerator P(string content) => P(null, content);

        public IDocumentationGenerator Br()
        {
            Output.Add("<br>");
            return this;
        }

        public IDocumentationGenerator MakeAnchor(string tag, Action content)
        {
            Output.Add($"<a name=\"{tag}\">");
            content();
            Output.Add("</a>");
            return this;
        }

        public IDocumentationGenerator MakeAnchor(string tag, string content)
        {
            Output.Add($"<a name=\"{tag}\">{content}</a>");
            return this;
        }

        public IDocumentationGenerator LinkToAnchor(string tag, Action content)
        {
            Output.Add($"<a href=\"#{tag}\">");
            content();
            Output.Add("</a>");
            return this;
        }

        public IDocumentationGenerator LinkToAnchor(string tag, string content)
        {
            Output.Add($"<a href=\"#{tag}\">{content}</a>");
            return this;
        }

        public IDocumentationGenerator MapLabel(float x, float y, string name, string type, string kingdomId, Func<string, string> getFillColor, Func<string, string> getBorderColor)
        {
            // Determine shape
            string shapeStyle = type switch
            {
                "Castle" => "border-radius:0%;",  // square
                "Town" => "border-radius:50%;",   // circle
                _ => "border-radius:25%;"         // rounded default
            };

            string fillColor = "#000080";
            string borderColor = "#000000";

            if (!string.IsNullOrEmpty(kingdomId))
            {
                // Get fill and border colors
                if (getFillColor != null)
                {
                    string c = getFillColor(kingdomId);
                    if (!string.IsNullOrEmpty(c))
                        fillColor = c.StartsWith("#") ? c : "#" + c;
                }

                if (getBorderColor != null)
                {
                    string c = getBorderColor(kingdomId);
                    if (!string.IsNullOrEmpty(c))
                        borderColor = c.StartsWith("#") ? c : "#" + c;
                }
            }                             

            string size = "12px";

            return Div(() =>
            {
                // Marker
                P($"<div style=\"position:absolute; left:{x}px; top:{y}px;" +
                  "transform:translate(-50%,-50%);" +
                  $"width:{size}; height:{size}; background:{fillColor}; {shapeStyle};" +
                  $"border:1px solid {borderColor}; box-shadow:1px 1px 2px rgba(0,0,0,0.5);\"></div>");

                // Name label slightly below marker
                P($"<div style=\"position:absolute; left:{x}px; top:{y + 8}px;" +
                  "transform:translate(-50%,0); font-size:10px; font-weight:bold;" +
                  "text-shadow:1px 1px 2px #000; white-space:nowrap;\">" +
                  $"{name}</div>");
            });
        }

        public IDocumentationGenerator MapSegment(float x1, float y1, float x2, float y2)
        {
            string color = "#2b5d87"; float thickness = 2f;
            float dx = x2 - x1;
            float dy = y2 - y1;

            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            float angle = (float)(Math.Atan2(dy, dx) * 180.0 / Math.PI);

            return Div(() =>
            {
                P($"<div style=\"position:absolute;" +
                  $"left:{x1}px;" +
                  $"top:{y1}px;" +
                  $"width:{length}px;" +
                  $"height:{thickness}px;" +
                  $"background:{color};" +
                  "transform-origin:0 50%;" +
                  $"transform:rotate({angle}deg);" +
                  "box-shadow:0 0 2px rgba(0,0,0,0.4);" +
                  "\"></div>");
            });
        }
    }
}
