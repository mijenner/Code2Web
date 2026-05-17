using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace cliCode2Web;

internal class Program
{
    // -----------------------------
    //  File extensions (baseline)
    // -----------------------------
    static readonly string[] BaseAllowedExtensions = {
        ".cs", ".csh", ".cpp", ".h", ".c", ".ino", ".java", ".py",
        ".csproj", ".xaml", ".axaml", ".html", ".shader", ".cginc",
        ".txt", ".md", ".xml", ".uxml", ".asmdef"
        // .json handled via flags/profiles/marker-tune
    };

    static readonly string JsonExtension = ".json";

    // Unity "YAML-ish" asset files (NOISY) – excluded by default
    static readonly string[] UnityYamlAssetExtensions = {
        ".unity", ".prefab", ".asset", ".mat", ".controller", ".anim"
    };

    // Unity code-ish extras (usually ok)
    static readonly string[] UnityCodeExtras = {
        ".cs", ".asmdef", ".shader", ".cginc", ".compute", ".hlsl", ".uxml", ".uss", ".md", ".txt"
    };

    // Unity vendor-ish folders under Assets that should be excluded by default
    // (These are almost never "student code".)
    static readonly string[] UnityVendorAssetFolders_DefaultExclude = {
        "TextMesh Pro",
        "Plugins",
        "Standard Assets"
    };

    // Node/Next typical extensions (incl. common SFC frameworks: Vue/Svelte/Astro)
    static readonly string[] NodeExtensions = {
        ".js", ".jsx", ".ts", ".tsx", ".mjs", ".cjs",
        ".vue", ".svelte", ".astro",
        ".css", ".scss", ".md", ".html",
        ".json"
    };

    // C# / .NET profile – lean and self-contained (console, Avalonia, MAUI, ASP.NET/Blazor).
    // NOTE: this profile is intentionally NOT merged with BaseAllowedExtensions anymore,
    // so a C# project does not silently slurp stray .py/.java/.cpp files.
    // Use a code2web.json marker (tune.addExtensions) for unusual projects.
    static readonly string[] CSharpExtensions = {
        ".cs", ".csh", ".razor", ".cshtml",
        ".csproj", ".sln", ".props", ".targets",
        ".xaml", ".axaml",
        ".html",
        ".xml", ".md", ".txt"
        // .json default off in csharp unless --include-json or marker tune
    };

    // Arduino
    static readonly string[] ArduinoExtensions = {
        ".ino", ".h", ".hpp", ".c", ".cpp", ".md", ".txt"
    };

    // -----------------------------
    //  Types
    // -----------------------------
    enum ProjectType
    {
        Auto,
        Unity,
        Node,
        Next,
        CSharp,
        Arduino,
        Generic
    }

    // -----------------------------
    //  Marker file models
    // -----------------------------
    sealed class MarkerFile
    {
        public int Schema { get; set; } = 1;
        public string Type { get; set; } = "auto";
        public string? Name { get; set; }

        // Finjustering – gælder ALLE typer (ikke kun generic).
        public MarkerTune? Tune { get; set; }
    }

    sealed class MarkerTune
    {
        // Tilføj ekstensioner oven på profilens defaults (fx ".json", ".sql").
        public string[]? AddExtensions { get; set; }

        // Fjern ekstensioner fra profilens defaults (trim støj, fx ".md", ".txt").
        public string[]? RemoveExtensions { get; set; }

        // Ekstra mapper der skal udelades.
        public string[]? ExcludeFolders { get; set; }

        // Begræns visningen til kun disse undermapper (relativt til projektroden).
        public string[]? IncludeFolders { get; set; }
    }

    sealed record ProjectSpec(
        string ProjectRoot,
        string ProjectName,
        ProjectType Type,
        MarkerFile? Marker
    );

    // -----------------------------
    //  Options
    // -----------------------------
    public class Options
    {
        [Option('c', "class", Required = true,
            HelpText = "Holdnavn (fx h2k24, h3k25). Bruges i standard output-sti.")]
        public string ClassName { get; set; } = string.Empty;

        [Option('d', "directory", Required = false,
            HelpText = "Rodmappe med elevmapper. Default: nuværende mappe.")]
        public string? BaseDirectory { get; set; }

        [Option('o', "output", Required = false,
            HelpText = "Outputmappe. Default: <Documents>\\Code2Web\\<Holdnavn>")]
        public string? OutputDirectory { get; set; }

        [Option('r', "depth", Required = false, Default = 5,
            HelpText = "Rekursionsdybde ved fil-scan i elevmapper (default = 5).")]
        public int MaxDepth { get; set; }

        [Option("marker-file", Required = false, Default = "code2web.json",
            HelpText = "Navn på markerfil (default: code2web.json).")]
        public string MarkerFileName { get; set; } = "code2web.json";

        [Option("marker-depth", Required = false, Default = 2,
            HelpText = "Hvor dybt der søges efter markerfil i elevmappen (default: 2).")]
        public int MarkerSearchDepth { get; set; }

        [Option("profile", Required = false, Default = "auto",
            HelpText = "Tving profil for alle elevmapper: auto|unity|node|next|csharp|arduino|generic (default: auto).")]
        public string ForcedProfile { get; set; } = "auto";

        [Option("include-obj", Required = false,
            HelpText = "Inkludér obj/ mapper (default: false).")]
        public bool IncludeObj { get; set; }

        [Option("include-bin", Required = false,
            HelpText = "Inkludér bin/ mapper (default: false).")]
        public bool IncludeBin { get; set; }

        [Option("include-idea", Required = false,
            HelpText = "Inkludér .idea/ mapper (default: false).")]
        public bool IncludeIdea { get; set; }

        [Option("include-vs", Required = false,
            HelpText = "Inkludér .vs/ mapper (default: false).")]
        public bool IncludeVs { get; set; }

        [Option("include-json", Required = false,
            HelpText = "Inkludér .json filer (default: false). Bemærk: node/next profiler inkluderer json som default.")]
        public bool IncludeJson { get; set; }

        // Unity: code-first by default. This flag explicitly includes Unity YAML assets (.unity/.prefab/.asset/...)
        [Option("unity-include-yaml", Required = false,
            HelpText = "Unity: Inkludér Unity YAML-assetfiler (.unity/.prefab/.asset/.mat/.controller/.anim). Default: false (code-first).")]
        public bool UnityIncludeYaml { get; set; }

        // Unity: include .meta is almost always noise; keep as explicit flag if you ever need GUID debugging
        [Option("unity-include-meta", Required = false,
            HelpText = "Unity: Inkludér .meta filer (GUID mapping). Default: false.")]
        public bool UnityIncludeMeta { get; set; }

        // Unity: include vendor folders under Assets (TextMesh Pro, Plugins, Standard Assets)
        [Option("unity-include-vendor", Required = false,
            HelpText = "Unity: Inkludér typiske vendor-foldere under Assets (fx TextMesh Pro, Plugins, Standard Assets). Default: false.")]
        public bool UnityIncludeVendor { get; set; }

        [Option('q', "quiet", Required = false,
            HelpText = "Kører uden ekstra statuslinjer.")]
        public bool Quiet { get; set; }
    }

    // -----------------------------
    //  Main
    // -----------------------------
    static int Main(string[] args)
    {
        var parser = new Parser(cfg => cfg.HelpWriter = null);
        var result = parser.ParseArguments<Options>(args);

        return result.MapResult(
            opts => RunWithOptions(opts),
            errs => DisplayHelp(result, errs)
        );
    }

    // -----------------------------
    //  Run
    // -----------------------------
    private static int RunWithOptions(Options options)
    {
        string baseDir = options.BaseDirectory ?? Directory.GetCurrentDirectory();
        if (!Directory.Exists(baseDir))
        {
            Console.Error.WriteLine($"❌ Rodmappen findes ikke: {baseDir}");
            return 1;
        }

        string outputDir = ResolveOutputDir(options, baseDir);
        Directory.CreateDirectory(outputDir);

        var forcedType = ParseProjectType(options.ForcedProfile);

        if (!options.Quiet)
        {
            Console.WriteLine($"Holdnavn             : {options.ClassName}");
            Console.WriteLine($"Base directory       : {baseDir}");
            Console.WriteLine($"Output directory     : {outputDir}");
            Console.WriteLine($"Rekursionsdybde      : {options.MaxDepth}");
            Console.WriteLine($"Marker file          : {options.MarkerFileName}");
            Console.WriteLine($"Marker depth         : {options.MarkerSearchDepth}");
            Console.WriteLine($"Forced profile       : {forcedType}");
            Console.WriteLine($"Include obj          : {options.IncludeObj}");
            Console.WriteLine($"Include bin          : {options.IncludeBin}");
            Console.WriteLine($"Include .idea        : {options.IncludeIdea}");
            Console.WriteLine($"Include .vs          : {options.IncludeVs}");
            Console.WriteLine($"Include .json        : {options.IncludeJson}");
            Console.WriteLine($"Unity include YAML   : {options.UnityIncludeYaml}");
            Console.WriteLine($"Unity include meta   : {options.UnityIncludeMeta}");
            Console.WriteLine($"Unity include vendor : {options.UnityIncludeVendor}");
            Console.WriteLine();
        }

        // Elevmapper = alle undermapper minus diverse meta-foldere
        var subdirs = Directory.GetDirectories(baseDir)
            .Where(d =>
            {
                var name = Path.GetFileName(d);
                if (IsMacOsXDir(name)) return false;
                if (IsObjDir(name) && !options.IncludeObj) return false;
                if (IsBinDir(name) && !options.IncludeBin) return false;
                if (IsIdeaDir(name) && !options.IncludeIdea) return false;
                if (IsVsDir(name) && !options.IncludeVs) return false;
                return true;
            })
            .ToArray();

        if (subdirs.Length == 0)
        {
            Console.Error.WriteLine("⚠️ Ingen elevmapper fundet.");
            return 1;
        }

        // Index-side
        var indexBuilder = new StringBuilder();
        indexBuilder.AppendLine("<!DOCTYPE html><html lang=\"da\"><head><meta charset=\"UTF-8\"><title>Gruppeoversigt</title>");
        indexBuilder.AppendLine(HeadIncludes());
        indexBuilder.AppendLine("</head><body>");
        indexBuilder.AppendLine(BuildTopNav("Index", subdirs));
        indexBuilder.AppendLine("<h1>Oversigt over elevgrupper</h1><ul>");

        foreach (var groupDir in subdirs)
        {
            var groupName = Path.GetFileName(groupDir);
            var groupHtmlName = $"{groupName}.html";

            indexBuilder.AppendLine($"<li><a href=\"{groupHtmlName}\">{EscapeHtml(groupName)}</a></li>");

            var projects = FindProjectsInGroup(groupDir, options, forcedType);

            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html><html lang=\"da\"><head><meta charset=\"UTF-8\">");
            html.AppendLine($"<title>{EscapeHtml(groupName)}</title>");
            html.AppendLine(HeadIncludes());
            html.AppendLine("</head><body>");
            html.AppendLine(BuildTopNav(groupName, subdirs));
            html.AppendLine($"<h1>{EscapeHtml(groupName)}</h1>");
            html.AppendLine(BuildExpandCollapseButtons());

            if (projects.Count > 1)
            {
                html.AppendLine("<h2>Projekter</h2>");
                html.AppendLine("<ul>");
                foreach (var p in projects)
                {
                    var anchor = MakeAnchor(p.ProjectName);
                    html.AppendLine($"<li><a href=\"#{anchor}\">{EscapeHtml(p.ProjectName)}</a> <span style=\"color:#666;\">({p.Type})</span></li>");
                }
                html.AppendLine("</ul>");
            }

            foreach (var project in projects)
            {
                WriteProjectSection(html, groupDir, project, options);
            }

            html.AppendLine("</body></html>");
            File.WriteAllText(Path.Combine(outputDir, groupHtmlName), html.ToString(), Encoding.UTF8);
        }

        indexBuilder.AppendLine("</ul></body></html>");
        File.WriteAllText(Path.Combine(outputDir, "index.html"), indexBuilder.ToString(), Encoding.UTF8);

        if (!options.Quiet)
            Console.WriteLine("✅ HTML-generering færdig.");

        return 0;
    }

    // -----------------------------
    //  Project discovery
    // -----------------------------
    private static List<ProjectSpec> FindProjectsInGroup(string groupDir, Options options, ProjectType forcedType)
    {
        // --profile tvinger én type for hele gruppemappen.
        if (forcedType != ProjectType.Auto)
        {
            return new List<ProjectSpec>
            {
                new ProjectSpec(
                    ProjectRoot: groupDir,
                    ProjectName: Path.GetFileName(groupDir),
                    Type: forcedType,
                    Marker: null
                )
            };
        }

        // 1) Auto-find ALLE projektrødder i gruppen (håndterer blandede / fuld-stack mapper:
        //    fx en C#-backend og en web-frontend som søskende).
        var discovered = DiscoverProjectRoots(groupDir, options);

        // 2) Find markerfiler. En marker på en fundet rod overstyrer type/navn;
        //    en marker et andet sted bliver sit eget projekt.
        var markerByRoot = new Dictionary<string, MarkerFile?>(StringComparer.OrdinalIgnoreCase);
        foreach (var markerPath in FindMarkerFiles(groupDir, options.MarkerFileName, options.MarkerSearchDepth))
        {
            var dir = Path.GetFullPath(Path.GetDirectoryName(markerPath)!);
            markerByRoot[dir] = TryReadMarker(markerPath);
        }

        var specs = new List<ProjectSpec>();

        // 2a) Fundne rødder (evt. justeret af en marker i samme mappe).
        foreach (var (root, type) in discovered)
        {
            var full = Path.GetFullPath(root);
            markerByRoot.TryGetValue(full, out var marker);

            var resolvedType = type;
            string? name = null;

            if (marker is not null)
            {
                var markerType = ParseProjectType(marker.Type);
                if (markerType != ProjectType.Auto)
                    resolvedType = markerType;
                name = marker.Name;
            }

            if (string.IsNullOrWhiteSpace(name))
                name = Path.GetFileName(root);

            specs.Add(new ProjectSpec(root, name!, resolvedType, marker));
        }

        // 2b) Markerfiler der IKKE sidder på en fundet rod -> bliver deres eget projekt.
        foreach (var (rootFull, marker) in markerByRoot)
        {
            if (specs.Any(s => Path.GetFullPath(s.ProjectRoot).Equals(rootFull, StringComparison.OrdinalIgnoreCase)))
                continue;

            var type = marker is null ? ProjectType.Auto : ParseProjectType(marker.Type);
            if (type == ProjectType.Auto)
                type = AutoDetectProjectType(rootFull);

            var name = marker?.Name;
            if (string.IsNullOrWhiteSpace(name))
                name = Path.GetFileName(rootFull);

            specs.Add(new ProjectSpec(rootFull, name!, type, marker));
        }

        specs = specs
            .GroupBy(s => Path.GetFullPath(s.ProjectRoot), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(s => s.ProjectName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // 3) Fandt vi intet (fx en mappe med løse .py-filer)? Behandl hele gruppen som ét projekt.
        if (specs.Count == 0)
        {
            var detected = AutoDetectProjectType(groupDir);
            specs.Add(new ProjectSpec(groupDir, Path.GetFileName(groupDir), detected, null));
        }

        return specs;
    }

    // -----------------------------
    //  Multi-project auto-discovery
    // -----------------------------
    // Mapper der aldrig skal gennemsøges når vi leder efter projektrødder.
    private static readonly HashSet<string> DiscoveryExcludedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", "obj", "bin", "out", "dist",
        ".git", ".vs", ".idea", ".next", ".turbo", ".cache", ".vercel",
        "Library", "Temp", "Logs", "Build", "UserSettings",
        "__MACOSX"
    };

    private static List<(string Root, ProjectType Type)> DiscoverProjectRoots(string groupDir, Options options)
    {
        var found = new List<(string Root, ProjectType Type)>();
        int maxDepth = Math.Max(options.MarkerSearchDepth, 3);
        DiscoverProjectRootsInternal(groupDir, 0, maxDepth, found);
        return CollapseProjectRoots(found);
    }

    private static void DiscoverProjectRootsInternal(
        string dir, int depth, int maxDepth, List<(string, ProjectType)> found)
    {
        var name = Path.GetFileName(dir);
        if (DiscoveryExcludedFolders.Contains(name)) return;

        // Unity-projekt: selvstændigt -> registrér og stop her.
        if (Directory.Exists(Path.Combine(dir, "Assets")) &&
            Directory.Exists(Path.Combine(dir, "ProjectSettings")))
        {
            found.Add((dir, ProjectType.Unity));
            return;
        }

        // Node/Next-projekt: package.json markerer roden -> registrér og stop her.
        if (File.Exists(Path.Combine(dir, "package.json")))
        {
            found.Add((dir, IsNextProject(dir) ? ProjectType.Next : ProjectType.Node));
            return;
        }

        // C#-projekt: .sln eller .csproj direkte i mappen. Vi stopper IKKE her,
        // så en evt. web-frontend nede i en monorepo stadig kan opdages.
        // Indlejrede .csproj (under en .sln) fjernes bagefter af CollapseProjectRoots.
        bool hasSln = false, hasCsproj = false;
        try { hasSln = Directory.GetFiles(dir, "*.sln").Length > 0; } catch { }
        try { hasCsproj = Directory.GetFiles(dir, "*.csproj").Length > 0; } catch { }
        if (hasSln || hasCsproj)
            found.Add((dir, ProjectType.CSharp));

        if (depth >= maxDepth) return;

        string[] subs;
        try { subs = Directory.GetDirectories(dir); }
        catch { return; }

        foreach (var sub in subs)
            DiscoverProjectRootsInternal(sub, depth + 1, maxDepth, found);
    }

    // Fjern dubletter og indlejrede rødder af SAMME type (fx .csproj under en .sln).
    // Rødder af forskellig type bevares (fuld-stack: C# + web side om side).
    private static List<(string Root, ProjectType Type)> CollapseProjectRoots(
        List<(string Root, ProjectType Type)> roots)
    {
        var norm = roots
            .Select(r => (Root: Path.GetFullPath(r.Root), r.Type))
            .GroupBy(r => r.Root, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        var result = new List<(string, ProjectType)>();
        foreach (var r in norm)
        {
            bool hasSameTypeAncestor = norm.Any(o =>
                o.Type == r.Type &&
                !o.Root.Equals(r.Root, StringComparison.OrdinalIgnoreCase) &&
                IsUnder(o.Root, r.Root));

            if (!hasSameTypeAncestor)
                result.Add(r);
        }
        return result;
    }

    private static bool IsNextProject(string dir)
        => File.Exists(Path.Combine(dir, "next.config.js"))
        || File.Exists(Path.Combine(dir, "next.config.mjs"))
        || File.Exists(Path.Combine(dir, "next.config.ts"))
        || File.Exists(Path.Combine(dir, "next.config.cjs"));

    private static List<string> FindMarkerFiles(string root, string markerFileName, int maxDepth)
    {
        var result = new List<string>();
        FindMarkerFilesInternal(root, 0, maxDepth, markerFileName, result);
        return result;
    }

    private static void FindMarkerFilesInternal(string dir, int depth, int maxDepth, string markerFileName, List<string> result)
    {
        var name = Path.GetFileName(dir);
        if (IsMacOsXDir(name)) return;

        var candidate = Path.Combine(dir, markerFileName);
        if (File.Exists(candidate))
            result.Add(candidate);

        if (depth >= maxDepth) return;

        string[] subdirs;
        try { subdirs = Directory.GetDirectories(dir); }
        catch { return; }

        foreach (var sub in subdirs)
            FindMarkerFilesInternal(sub, depth + 1, maxDepth, markerFileName, result);
    }

    private static MarkerFile? TryReadMarker(string markerPath)
    {
        try
        {
            var json = File.ReadAllText(markerPath, Encoding.UTF8);
            var opts = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            return JsonSerializer.Deserialize<MarkerFile>(json, opts);
        }
        catch
        {
            return null;
        }
    }

    private static ProjectType AutoDetectProjectType(string root)
    {
        if (Directory.Exists(Path.Combine(root, "Assets")) &&
            Directory.Exists(Path.Combine(root, "ProjectSettings")))
            return ProjectType.Unity;

        if (File.Exists(Path.Combine(root, "package.json")))
        {
            // Kun Next hvis der findes en next.config.* — en blot 'app/'-mappe
            // bruges også af React Router 7 og Remix, så den er ikke nok.
            return IsNextProject(root) ? ProjectType.Next : ProjectType.Node;
        }

        if (HasAnyFile(root, "*.sln", searchDepth: 2) || HasAnyFile(root, "*.csproj", searchDepth: 2))
            return ProjectType.CSharp;

        if (HasAnyFile(root, "*.ino", searchDepth: 2))
            return ProjectType.Arduino;

        return ProjectType.Generic;
    }

    private static bool HasAnyFile(string root, string pattern, int searchDepth)
    {
        try
        {
            var q = new Queue<(string dir, int depth)>();
            q.Enqueue((root, 0));

            while (q.Count > 0)
            {
                var (dir, depth) = q.Dequeue();
                try
                {
                    if (Directory.GetFiles(dir, pattern).Length > 0)
                        return true;
                }
                catch { }

                if (depth >= searchDepth) continue;

                string[] subs;
                try { subs = Directory.GetDirectories(dir); }
                catch { continue; }

                foreach (var s in subs)
                {
                    var name = Path.GetFileName(s);
                    if (IsMacOsXDir(name)) continue;
                    q.Enqueue((s, depth + 1));
                }
            }
        }
        catch { }

        return false;
    }

    private static ProjectType ParseProjectType(string? type)
    {
        var t = (type ?? "auto").Trim().ToLowerInvariant();
        return t switch
        {
            "auto" => ProjectType.Auto,
            "unity" => ProjectType.Unity,
            "node" => ProjectType.Node,
            "next" => ProjectType.Next,
            "csharp" => ProjectType.CSharp,
            "arduino" => ProjectType.Arduino,
            "generic" => ProjectType.Generic,
            _ => ProjectType.Auto
        };
    }

    // -----------------------------
    //  Write project section
    // -----------------------------
    private static void WriteProjectSection(StringBuilder html, string groupDir, ProjectSpec project, Options options)
    {
        var anchor = MakeAnchor(project.ProjectName);

        html.AppendLine($"<hr style=\"margin:2rem 0;\"/>");
        html.AppendLine($"<h2 id=\"{anchor}\">{EscapeHtml(project.ProjectName)} <span style=\"color:#666;font-weight:normal;\">({project.Type})</span></h2>");
        html.AppendLine($"<div style=\"color:#666; margin-bottom:0.8rem;\"><code>{EscapeHtml(RelPath(groupDir, project.ProjectRoot))}</code></div>");

        var files = project.Type switch
        {
            ProjectType.Unity => HandleUnityProject(project.ProjectRoot, options, project.Marker),
            ProjectType.Next => HandleNextProject(project.ProjectRoot, options, project.Marker),
            ProjectType.Node => HandleNodeProject(project.ProjectRoot, options, project.Marker),
            ProjectType.CSharp => HandleCSharpProject(project.ProjectRoot, options, project.Marker),
            ProjectType.Arduino => HandleArduinoProject(project.ProjectRoot, options, project.Marker),
            ProjectType.Generic => HandleGenericProject(project.ProjectRoot, options, project.Marker),
            _ => HandleGenericProject(project.ProjectRoot, options, project.Marker)
        };

        // Marker-tune: begræns visningen til bestemte undermapper hvis ønsket.
        var includeFolders = project.Marker?.Tune?.IncludeFolders;
        if (includeFolders is { Length: > 0 })
        {
            var roots = includeFolders
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => Path.GetFullPath(Path.Combine(project.ProjectRoot, p)))
                .Where(Directory.Exists)
                .ToList();

            if (roots.Count > 0)
                files = files.Where(f => roots.Any(r => IsUnder(r, f))).ToList();
        }

        // Vis aldrig selve markerfilen i outputtet.
        files = files
            .Where(f => !Path.GetFileName(f).Equals(options.MarkerFileName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (files.Count == 0)
        {
            html.AppendLine("<p><em>Ingen relevante filer fundet.</em></p>");
            return;
        }

        foreach (var file in files)
        {
            string relative = Path.GetRelativePath(project.ProjectRoot, file)
                .Replace(Path.DirectorySeparatorChar, '/');

            var ext = Path.GetExtension(file).ToLowerInvariant();
            var lang = GetLanguageClass(ext);

            string content;
            try { content = File.ReadAllText(file, Encoding.UTF8); }
            catch
            {
                try { content = File.ReadAllText(file); }
                catch { continue; }
            }

            // Bemærk: alt indhold køres gennem HtmlEncode nedenfor, hvilket allerede
            // gør <script>-tags ufarlige. Ingen separat "neutralisering" nødvendig.
            html.AppendLine("<details>");
            html.AppendLine($"<summary><strong>{EscapeHtml(relative)}</strong></summary>");
            html.AppendLine($"<pre><code class=\"{lang}\">");
            html.AppendLine(System.Net.WebUtility.HtmlEncode(content));
            html.AppendLine("</code></pre>");
            html.AppendLine("</details>");
        }
    }

    // -----------------------------
    //  Profile handlers (hardcoded)
    // -----------------------------
    private static List<string> HandleUnityProject(string root, Options options, MarkerFile? marker)
    {
        // Unity: code-first by default.
        // Exclude generated folders + vendor folders under Assets by default (TextMesh Pro etc.).
        // YAML assets (.asset/.prefab/.unity/...) are excluded by default and can be enabled with --unity-include-yaml.
        // .meta is excluded by default and can be enabled with --unity-include-meta.

        var includeRoots = new[]
        {
            Path.Combine(root, "Assets"),
            Path.Combine(root, "Packages"),
            Path.Combine(root, "ProjectSettings")
        };

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in UnityCodeExtras) allowed.Add(e);

        // Useful JSON: Packages manifests
        allowed.Add(".json");

        if (options.UnityIncludeYaml)
        {
            foreach (var e in UnityYamlAssetExtensions) allowed.Add(e);
        }

        if (options.UnityIncludeMeta)
        {
            allowed.Add(".meta");
        }

        var excludeFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Library", "Temp", "Logs", "Obj", "Build", "UserSettings",
            "__MACOSX"
        };

        // Exclude vendor-ish folders under Assets by default
        if (!options.UnityIncludeVendor)
        {
            foreach (var f in UnityVendorAssetFolders_DefaultExclude)
                excludeFolders.Add(f);
        }

        // Honor global excludes
        if (!options.IncludeObj) excludeFolders.Add("obj");
        if (!options.IncludeBin) excludeFolders.Add("bin");
        if (!options.IncludeIdea) excludeFolders.Add(".idea");
        if (!options.IncludeVs) excludeFolders.Add(".vs");

        // Marker-tune (add/remove extensions, extra excluded folders)
        ApplyMarkerTune(allowed, excludeFolders, marker);

        var files = EnumerateFromRoots(includeRoots, root, options.MaxDepth, allowed, excludeFolders, options);

        // Always include package manifests if they exist
        AddIfExists(files, Path.Combine(root, "Packages", "manifest.json"));
        AddIfExists(files, Path.Combine(root, "Packages", "packages-lock.json"));

        return SortByRelative(root, files);
    }

    private static List<string> HandleNextProject(string root, Options options, MarkerFile? marker)
    {
        var includeRoots = new[]
        {
            Path.Combine(root, "app"),
            Path.Combine(root, "pages"),
            Path.Combine(root, "src"),
            Path.Combine(root, "components"),
            Path.Combine(root, "lib"),
            Path.Combine(root, "public"),
            Path.Combine(root, "styles"),
            root
        };

        var allowed = new HashSet<string>(NodeExtensions, StringComparer.OrdinalIgnoreCase);

        var excludeFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "node_modules", ".next", "dist", "out", ".turbo", ".cache", ".vercel",
            "__MACOSX"
        };

        if (!options.IncludeObj) excludeFolders.Add("obj");
        if (!options.IncludeBin) excludeFolders.Add("bin");
        if (!options.IncludeIdea) excludeFolders.Add(".idea");
        if (!options.IncludeVs) excludeFolders.Add(".vs");

        ApplyMarkerTune(allowed, excludeFolders, marker);

        var files = EnumerateFromRoots(includeRoots, root, options.MaxDepth, allowed, excludeFolders, options);

        files = files.Where(f =>
        {
            var name = Path.GetFileName(f);
            if (name.Equals(".env", StringComparison.OrdinalIgnoreCase)) return false;
            if (name.StartsWith(".env.", StringComparison.OrdinalIgnoreCase) && !name.EndsWith(".example", StringComparison.OrdinalIgnoreCase))
                return false;
            return true;
        }).ToList();

        AddIfExists(files, Path.Combine(root, "package.json"));
        AddIfExists(files, Path.Combine(root, "tsconfig.json"));
        AddIfExists(files, Path.Combine(root, "next.config.js"));
        AddIfExists(files, Path.Combine(root, "next.config.mjs"));
        AddIfExists(files, Path.Combine(root, "next.config.ts"));

        return SortByRelative(root, files);
    }

    private static List<string> HandleNodeProject(string root, Options options, MarkerFile? marker)
    {
        var includeRoots = new[]
        {
            Path.Combine(root, "src"),
            Path.Combine(root, "server"),
            Path.Combine(root, "client"),
            Path.Combine(root, "lib"),
            root
        };

        var allowed = new HashSet<string>(NodeExtensions, StringComparer.OrdinalIgnoreCase);

        var excludeFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "node_modules", "dist", "out", ".turbo", ".cache",
            "__MACOSX"
        };

        if (!options.IncludeObj) excludeFolders.Add("obj");
        if (!options.IncludeBin) excludeFolders.Add("bin");
        if (!options.IncludeIdea) excludeFolders.Add(".idea");
        if (!options.IncludeVs) excludeFolders.Add(".vs");

        ApplyMarkerTune(allowed, excludeFolders, marker);

        var files = EnumerateFromRoots(includeRoots, root, options.MaxDepth, allowed, excludeFolders, options);

        files = files.Where(f =>
        {
            var name = Path.GetFileName(f);
            if (name.Equals(".env", StringComparison.OrdinalIgnoreCase)) return false;
            if (name.StartsWith(".env.", StringComparison.OrdinalIgnoreCase) && !name.EndsWith(".example", StringComparison.OrdinalIgnoreCase))
                return false;
            return true;
        }).ToList();

        AddIfExists(files, Path.Combine(root, "package.json"));
        AddIfExists(files, Path.Combine(root, "tsconfig.json"));

        return SortByRelative(root, files);
    }

    private static List<string> HandleCSharpProject(string root, Options options, MarkerFile? marker)
    {
        // Lean .NET-profil – dækker console, Avalonia (.axaml), MAUI (.xaml),
        // og ASP.NET/Blazor (.razor/.cshtml) ud af boksen.
        var allowed = new HashSet<string>(CSharpExtensions, StringComparer.OrdinalIgnoreCase);

        if (options.IncludeJson)
            allowed.Add(".json");

        var excludeFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "__MACOSX"
        };

        if (!options.IncludeObj) excludeFolders.Add("obj");
        if (!options.IncludeBin) excludeFolders.Add("bin");
        if (!options.IncludeIdea) excludeFolders.Add(".idea");
        if (!options.IncludeVs) excludeFolders.Add(".vs");

        excludeFolders.Add(".idea_modules");
        excludeFolders.Add("out");
        excludeFolders.Add(".jetbrains");

        // Marker-tune: tilføj fx .json/.sql, fjern støj, udelad ekstra mapper.
        ApplyMarkerTune(allowed, excludeFolders, marker);

        var files = EnumerateWithExcludes(root, options.MaxDepth, allowed, excludeFolders, options);

        var sln = FindFirstFile(root, "*.sln", searchDepth: Math.Min(3, options.MaxDepth));
        if (sln is not null)
        {
            AddIfExists(files, sln);
            files = files.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            files = files.OrderBy(f => !f.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
                         .ThenBy(f => Path.GetRelativePath(root, f), StringComparer.OrdinalIgnoreCase)
                         .ToList();
            return files;
        }

        return SortByRelative(root, files);
    }

    private static List<string> HandleArduinoProject(string root, Options options, MarkerFile? marker)
    {
        var allowed = new HashSet<string>(ArduinoExtensions, StringComparer.OrdinalIgnoreCase);

        var excludeFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "__MACOSX"
        };

        if (!options.IncludeObj) excludeFolders.Add("obj");
        if (!options.IncludeBin) excludeFolders.Add("bin");
        if (!options.IncludeIdea) excludeFolders.Add(".idea");
        if (!options.IncludeVs) excludeFolders.Add(".vs");

        ApplyMarkerTune(allowed, excludeFolders, marker);

        return SortByRelative(root, EnumerateWithExcludes(root, options.MaxDepth, allowed, excludeFolders, options));
    }

    private static List<string> HandleGenericProject(string root, Options options, MarkerFile? marker)
    {
        // Generic-profilen starter på BaseAllowedExtensions og finjusteres udelukkende
        // via marker-tune (additivt). Brug den til skæve projekter der ikke matcher en profil.
        var allowed = new HashSet<string>(BaseAllowedExtensions, StringComparer.OrdinalIgnoreCase);

        if (options.IncludeJson)
            allowed.Add(".json");

        var excludeFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "__MACOSX"
        };

        if (!options.IncludeObj) excludeFolders.Add("obj");
        if (!options.IncludeBin) excludeFolders.Add("bin");
        if (!options.IncludeIdea) excludeFolders.Add(".idea");
        if (!options.IncludeVs) excludeFolders.Add(".vs");

        ApplyMarkerTune(allowed, excludeFolders, marker);

        // tune.includeFolders håndteres som efter-filter i WriteProjectSection,
        // så det virker ens for alle profiler.
        return SortByRelative(root, EnumerateWithExcludes(root, options.MaxDepth, allowed, excludeFolders, options));
    }

    // -----------------------------
    //  Marker tune
    // -----------------------------
    private static void ApplyMarkerTune(
        HashSet<string> allowedExtensions,
        HashSet<string> excludeFolders,
        MarkerFile? marker)
    {
        var tune = marker?.Tune;
        if (tune is null) return;

        // Fjern først, tilføj bagefter (add vinder ved overlap).
        if (tune.RemoveExtensions is { Length: > 0 })
        {
            foreach (var e in tune.RemoveExtensions)
            {
                var ext = NormalizeExt(e);
                if (!string.IsNullOrWhiteSpace(ext))
                    allowedExtensions.Remove(ext);
            }
        }

        if (tune.AddExtensions is { Length: > 0 })
        {
            foreach (var e in tune.AddExtensions)
            {
                var ext = NormalizeExt(e);
                if (!string.IsNullOrWhiteSpace(ext))
                    allowedExtensions.Add(ext);
            }
        }

        if (tune.ExcludeFolders is { Length: > 0 })
        {
            foreach (var f in tune.ExcludeFolders)
            {
                if (!string.IsNullOrWhiteSpace(f))
                    excludeFolders.Add(f.Trim());
            }
        }
    }

    // -----------------------------
    //  Enumeration helpers
    // -----------------------------
    private static List<string> EnumerateFromRoots(
        string[] includeRoots,
        string projectRoot,
        int maxDepth,
        HashSet<string> allowedExtensions,
        HashSet<string> excludeFolders,
        Options options)
    {
        var all = new List<string>();

        foreach (var r in includeRoots)
        {
            if (!Directory.Exists(r)) continue;

            int used = DepthBetween(projectRoot, r);
            int remaining = Math.Max(0, maxDepth - used);

            all.AddRange(EnumerateWithExcludes(r, remaining, allowedExtensions, excludeFolders, options));
        }

        all = all
            .Where(f => IsUnder(projectRoot, f))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return SortByRelative(projectRoot, all);
    }

    private static List<string> EnumerateWithExcludes(
        string root,
        int maxDepth,
        HashSet<string> allowedExtensions,
        HashSet<string> excludeFolders,
        Options options)
    {
        var files = EnumerateCodeFilesInternal(
                root, 0, maxDepth,
                options.IncludeObj,
                options.IncludeIdea,
                options.IncludeVs,
                options.IncludeBin,
                excludeFolders)
            .Where(f =>
            {
                var ext = Path.GetExtension(f).ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(ext)) return false;
                return allowedExtensions.Contains(ext);
            })
            .ToList();

        return files;
    }

    private static IEnumerable<string> EnumerateCodeFilesInternal(
        string dir,
        int depth,
        int maxDepth,
        bool includeObj,
        bool includeIdea,
        bool includeVs,
        bool includeBin,
        HashSet<string> extraExcludedFolders)
    {
        var name = Path.GetFileName(dir);

        if (IsMacOsXDir(name)) yield break;
        if (extraExcludedFolders.Contains(name)) yield break;

        if (IsObjDir(name) && !includeObj) yield break;
        if (IsBinDir(name) && !includeBin) yield break;
        if (IsIdeaDir(name) && !includeIdea) yield break;
        if (IsVsDir(name) && !includeVs) yield break;

        string[] files;
        try { files = Directory.GetFiles(dir); }
        catch { yield break; }

        foreach (var f in files)
            yield return f;

        if (depth >= maxDepth) yield break;

        string[] subdirs;
        try { subdirs = Directory.GetDirectories(dir); }
        catch { yield break; }

        foreach (var sub in subdirs)
            foreach (var f in EnumerateCodeFilesInternal(
                         sub, depth + 1, maxDepth,
                         includeObj, includeIdea, includeVs, includeBin,
                         extraExcludedFolders))
                yield return f;
    }

    // -----------------------------
    //  Helpers: paths, sorting
    // -----------------------------
    private static int DepthBetween(string ancestor, string descendant)
    {
        try
        {
            var a = Path.GetFullPath(ancestor).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var d = Path.GetFullPath(descendant).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (!d.StartsWith(a, StringComparison.OrdinalIgnoreCase))
                return 0;

            var rel = d.Substring(a.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.IsNullOrEmpty(rel)) return 0;
            return rel.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries).Length;
        }
        catch { return 0; }
    }

    private static bool IsUnder(string ancestor, string filePath)
    {
        try
        {
            var a = Path.GetFullPath(ancestor).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var f = Path.GetFullPath(filePath);
            return f.StartsWith(a, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static List<string> SortByRelative(string root, List<string> files)
        => files.OrderBy(f => Path.GetRelativePath(root, f), StringComparer.OrdinalIgnoreCase).ToList();

    private static void AddIfExists(List<string> list, string filePath)
    {
        try
        {
            if (File.Exists(filePath))
                list.Add(filePath);
        }
        catch { }
    }

    private static string? FindFirstFile(string root, string pattern, int searchDepth)
    {
        try
        {
            var q = new Queue<(string dir, int depth)>();
            q.Enqueue((root, 0));

            while (q.Count > 0)
            {
                var (dir, depth) = q.Dequeue();

                try
                {
                    var found = Directory.GetFiles(dir, pattern);
                    if (found.Length > 0) return found[0];
                }
                catch { }

                if (depth >= searchDepth) continue;

                string[] subs;
                try { subs = Directory.GetDirectories(dir); }
                catch { continue; }

                foreach (var s in subs)
                {
                    var name = Path.GetFileName(s);
                    if (IsMacOsXDir(name)) continue;
                    q.Enqueue((s, depth + 1));
                }
            }
        }
        catch { }

        return null;
    }

    private static string RelPath(string from, string to)
    {
        try
        {
            var rel = Path.GetRelativePath(from, to);
            return rel.Replace(Path.DirectorySeparatorChar, '/');
        }
        catch { return to; }
    }

    private static string NormalizeExt(string ext)
    {
        var e = (ext ?? "").Trim();
        if (string.IsNullOrEmpty(e)) return e;
        if (!e.StartsWith(".")) e = "." + e;
        return e.ToLowerInvariant();
    }

    // -----------------------------
    //  Help
    // -----------------------------
    private static int DisplayHelp<T>(ParserResult<T> result, IEnumerable<CommandLine.Error> errs)
        where T : class
    {
        bool isHelp = errs.IsHelp() || errs.IsVersion();

        var help = HelpText.AutoBuild(result, h =>
        {
            h.Heading = "cliCode2Web: Udtrækker kode fra elevmapper og viser via HTML-dokumenter";
            h.AddPreOptionsLine("");
            h.AddPreOptionsLine("Syntax: cliCode2Web --class=holdnavn [options]");
            h.AddPreOptionsLine("");
            h.AddPreOptionsLine("Standard output     : <Documents>\\Code2Web\\<Holdnavn>");
            h.AddPreOptionsLine("Standard dybde      : 5");
            h.AddPreOptionsLine("Standard markerfile : code2web.json (søges ned til depth=2)");
            h.AddPreOptionsLine("");
            h.AddPreOptionsLine("Markerfil (code2web.json) kan sætte 'type', 'name' og et 'tune'-objekt.");
            h.AddPreOptionsLine("Type-katalog: auto|unity|node|next|csharp|arduino|generic");
            h.AddPreOptionsLine("tune gælder ALLE typer og bruges til at finjustere usædvanlige projekter:");
            h.AddPreOptionsLine("  tune.addExtensions     - ekstra ekstensioner (fx \".json\", \".sql\")");
            h.AddPreOptionsLine("  tune.removeExtensions  - fjern ekstensioner (trim støj)");
            h.AddPreOptionsLine("  tune.excludeFolders    - ekstra mapper der udelades");
            h.AddPreOptionsLine("  tune.includeFolders    - vis kun disse undermapper");
            h.AddPreOptionsLine("");
            h.AddPreOptionsLine("Unity default er code-first: kun scripts/shaders/asmdef/ui-filer. YAML assets (fx .asset) er off som default.");
            h.AddPreOptionsLine("Unity vendor-foldere under Assets (fx TextMesh Pro) er off som default.");
            h.AddPreOptionsLine("Slå til med: --unity-include-yaml, --unity-include-meta, --unity-include-vendor");
            h.AddPreOptionsLine("");
            h.AddPreOptionsLine("Standard eksklusion : obj/, bin/, .idea/, .vs/, __MACOSX/, .json (undtagelse: node/next profiler inkluderer json).");
            h.AddPreOptionsLine("Tænd med            : --include-obj, --include-bin, --include-idea, --include-vs, --include-json");
            h.AddPreOptionsLine("");
            h.AddPreOptionsLine("Profiler            : --profile=auto|unity|node|next|csharp|arduino|generic (tvinger for alle grupper)");
            return HelpText.DefaultParsingErrorsHandler(result, h);
        }, e => e);

        Console.WriteLine(help);
        return isHelp ? 0 : 1;
    }

    private static string ResolveOutputDir(Options options, string baseDir)
    {
        if (!string.IsNullOrWhiteSpace(options.OutputDirectory))
            return options.OutputDirectory!;

        string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(docs))
            docs = baseDir;

        return Path.Combine(docs, "Code2Web", options.ClassName);
    }

    // -----------------------------
    //  Folder helpers
    // -----------------------------
    private static bool IsObjDir(string name) =>
        name.Equals("obj", StringComparison.OrdinalIgnoreCase);

    private static bool IsBinDir(string name) =>
        name.Equals("bin", StringComparison.OrdinalIgnoreCase);

    private static bool IsIdeaDir(string name) =>
        name.Equals(".idea", StringComparison.OrdinalIgnoreCase);

    private static bool IsVsDir(string name) =>
        name.Equals(".vs", StringComparison.OrdinalIgnoreCase);

    private static bool IsMacOsXDir(string name) =>
        name.Equals("__MACOSX", StringComparison.OrdinalIgnoreCase);

    // -----------------------------
    //  Syntax highlighting
    // -----------------------------
    static string GetLanguageClass(string ext) => ext switch
    {
        ".cs" or ".csh" => "language-csharp",
        ".cpp" or ".h" or ".c" or ".ino" or ".hpp" => "language-cpp",
        ".java" => "language-java",
        ".py" => "language-python",
        ".csproj" or ".props" or ".targets" => "language-xml",
        ".sln" => "language-plaintext",
        ".xml" or ".xaml" or ".axaml" or ".uxml" => "language-xml",
        ".json" or ".asmdef" => "language-json",
        ".html" or ".razor" or ".cshtml" or ".vue" or ".svelte" or ".astro" => "language-html",
        ".shader" or ".cginc" or ".hlsl" or ".compute" => "language-cpp",
        ".md" => "language-markdown",
        ".js" or ".mjs" or ".cjs" => "language-javascript",
        ".ts" or ".tsx" => "language-typescript",
        ".jsx" => "language-javascript",
        ".css" or ".scss" or ".uss" => "language-css",
        ".unity" or ".prefab" or ".asset" or ".mat" or ".controller" or ".anim" => "language-yaml",
        ".meta" => "language-plaintext",
        _ => "language-plaintext"
    };

    // -----------------------------
    //  HTML helpers
    // -----------------------------
    static string BuildTopNav(string current, string[] subdirs)
    {
        var nav = new StringBuilder();
        nav.AppendLine("<div style=\"position:sticky;top:0;background:#eee;padding:0.5rem;border-bottom:1px solid #ccc;overflow-x:auto;white-space:nowrap;\">");

        nav.Append($"<a href=\"index.html\" style=\"margin-right:1rem;font-weight:{(current == "Index" ? "bold" : "normal")}\">Index</a>");

        foreach (var dir in subdirs)
        {
            var name = Path.GetFileName(dir);
            var isCurrent = name.Equals(current, StringComparison.OrdinalIgnoreCase);
            var display = name.Length <= 10 ? name : name[..9] + "…";

            nav.Append($"<a href=\"{EscapeHtml(name)}.html\" style=\"margin-right:1rem;font-weight:{(isCurrent ? "bold" : "normal")}\">{EscapeHtml(display)}</a>");
        }

        nav.AppendLine("</div>");
        return nav.ToString();
    }

    static string BuildExpandCollapseButtons()
    {
        return @"
<div style='margin:1rem 0;'>
  <button onclick='expandAll()'>Fold alle ud</button>
  <button onclick='collapseAll()'>Luk alle</button>
</div>
<script>
  function expandAll() {
    document.querySelectorAll('details').forEach(d => d.open = true);
  }
  function collapseAll() {
    document.querySelectorAll('details').forEach(d => d.open = false);
  }
</script>";
    }

    static string HeadIncludes()
    {
        return @"
<link rel=""stylesheet"" href=""https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/styles/vs.min.css"">
<script src=""https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/highlight.min.js""></script>
<script>hljs.highlightAll();</script>

<style>
  body { font-family: sans-serif; margin: 2rem; }

  pre {
    background: #f5f5f5;
    padding: 1rem;
    border-radius: 6px;
    overflow-x: auto;
    white-space: pre-wrap;
    word-wrap: break-word;
  }

  code {
    font-size: 0.9rem;
    white-space: inherit;
  }

  summary {
    font-size: 1rem;
    padding: 0.2rem;
    background: #eee;
    border-radius: 4px;
    cursor: pointer;
  }

  @media print {
    pre, code {
      white-space: pre-wrap;
      word-wrap: break-word;
    }
  }
</style>";
    }

    private static string EscapeHtml(string s) => System.Net.WebUtility.HtmlEncode(s ?? "");

    private static string MakeAnchor(string s)
    {
        var clean = new string((s ?? "")
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray());

        while (clean.Contains("--"))
            clean = clean.Replace("--", "-");

        clean = clean.Trim('-');
        return string.IsNullOrWhiteSpace(clean) ? "project" : clean;
    }
}
