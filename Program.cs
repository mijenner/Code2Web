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
        [Option('c', "class", Required = false,
            HelpText = "Holdnavn (fx h2k24, h3k25). Påkrævet i normal kørsel. Bruges i standard output-sti.")]
        public string ClassName { get; set; } = string.Empty;

        [Option("make-reference", Required = false,
            HelpText = "Reference-tilstand: udtræk skabelon-referencer fra projekter under --directory.")]
        public bool MakeReference { get; set; }

        [Option("prerun", Required = false,
            HelpText = "Prerun-tilstand: detektér projekter, klassificér mod referencer og skriv code2web-plan.txt pr. produkt.")]
        public bool Prerun { get; set; }

        [Option("overwrite", Required = false,
            HelpText = "Sammen med --prerun: genskab plan-filer fra bunden (ellers flettes nye filer additivt ind).")]
        public bool Overwrite { get; set; }

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
        // Sikrer at ✅ ❌ ⚠️ 📝 vises korrekt i konsollen (i stedet for '?').
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }

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
        // Reference-tilstand: udtræk skabelon-referencer og afslut.
        if (options.MakeReference)
            return RunReferenceExtraction(options);

        // --overwrite giver kun mening sammen med --prerun.
        if (options.Overwrite && !options.Prerun)
        {
            Console.Error.WriteLine("❌ --overwrite kan kun bruges sammen med --prerun.");
            return 1;
        }

        // Prerun-tilstand: skriv plan-filer og afslut.
        if (options.Prerun)
            return RunPrerun(options);

        // Normal kørsel kræver --class.
        if (string.IsNullOrWhiteSpace(options.ClassName))
        {
            Console.Error.WriteLine("❌ --class er påkrævet (medmindre du kører --make-reference).");
            return 1;
        }

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
        ".git", ".svn", ".hg",
        ".vs", ".idea", ".vscode", ".fleet",
        ".next", ".turbo", ".cache", ".vercel", "TestResults",
        "Library", "Temp", "Logs", "Build", "UserSettings",
        "__MACOSX"
    };

    // IDE-/VCS-støj der ALTID udelades fra fil-scan (i modsætning til
    // obj/bin/.idea/.vs som styres af --include-* flag).
    private static readonly HashSet<string> AlwaysNoiseFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", ".git", ".svn", ".hg", ".vscode", ".fleet", "TestResults", "__MACOSX"
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

        // C#-projekt: kun .csproj direkte i mappen markerer en projektrod.
        // En mappe der KUN har .sln (uden .csproj) er en solution-container -
        // vi går igennem den for at finde de rigtige projekter nedenunder, men
        // mappen selv bliver IKKE en projektrod (ellers ville den slå produkt-
        // mappens code2web-plan.txt og lignende ind i scanningen).
        bool hasSln = false, hasCsproj = false;
        try { hasSln = Directory.GetFiles(dir, "*.sln").Length > 0; } catch { }
        try { hasCsproj = Directory.GetFiles(dir, "*.csproj").Length > 0; } catch { }
        if (hasCsproj)
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

    // =================================================================
    //  Reference extraction  (--make-reference)
    // -----------------------------------------------------------------
    //  Gennemgår --directory, finder hvert projekt (på .csproj-/Unity-/
    //  package.json-niveau) og skriver reference-filer: rene tokeniserede
    //  sti-lister (projektnavn erstattet med {PROJECT}). Navngivning og
    //  fletning styres af code2web-mapping.txt i references-mappen.
    // =================================================================
    private const string ProjectToken = "{PROJECT}";
    private const string MappingFileName = "code2web-mapping.txt";

    private static int RunReferenceExtraction(Options options)
    {
        string inputDir = options.BaseDirectory ?? Directory.GetCurrentDirectory();
        if (!Directory.Exists(inputDir))
        {
            Console.Error.WriteLine($"❌ Mappen findes ikke: {inputDir}");
            return 1;
        }

        // Reference-bibliotek samles ét fast sted, konsistent med at al
        // Code2Web-output ligger under <Documents>\Code2Web. Det er additivt:
        // gen-kørsler tilføjer/opdaterer referencer og sletter aldrig andre.
        string outputDir;
        if (!string.IsNullOrWhiteSpace(options.OutputDirectory))
        {
            outputDir = options.OutputDirectory!;
        }
        else
        {
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrWhiteSpace(docs)) docs = inputDir;
            outputDir = Path.Combine(docs, "Code2Web", "references");
        }
        Directory.CreateDirectory(outputDir);

        string mappingPath = Path.Combine(outputDir, MappingFileName);

        if (!options.Quiet)
        {
            Console.WriteLine("Reference-tilstand");
            Console.WriteLine($"Input   : {inputDir}");
            Console.WriteLine($"Output  : {outputDir}");
            Console.WriteLine($"Mapping : {mappingPath}");
            Console.WriteLine();
        }

        var projects = DiscoverProjectsForReference(inputDir);
        if (projects.Count == 0)
        {
            Console.Error.WriteLine("⚠️ Ingen projekter fundet at udtrække reference fra.");
            return 1;
        }

        // Tokeniserings-token pr. projekt (fælles basis for multiprojekter).
        var projectTokens = ComputeProjectTokens(projects);

        // Læs eksisterende mapping (projekt-sti -> reference-navn). Begge felter trimmes.
        var mapping = ReadMappingFile(mappingPath);

        // Knyt hvert fundne projekt til en mapping-nøgle = sti relativ til inputDir.
        var resolved = new List<(string Root, ProjectType Type, string Key, string RefName)>();
        var newMappingKeys = new List<(string Key, string Suggested)>();

        foreach (var (root, type) in projects)
        {
            var key = Path.GetRelativePath(inputDir, root).Replace(Path.DirectorySeparatorChar, '/');

            if (!mapping.TryGetValue(key, out var refName) || string.IsNullOrWhiteSpace(refName))
            {
                // Nyt projekt: foreslå navn = mappenavn uden efterstillede cifre.
                refName = SuggestReferenceName(Path.GetFileName(root));
                newMappingKeys.Add((key, refName));
            }

            resolved.Add((root, type, key, SanitizeReferenceName(refName)));
        }

        // Gruppér efter reference-navn -> det er her fletningen (union) sker.
        int written = 0;
        foreach (var group in resolved.GroupBy(r => r.RefName, StringComparer.OrdinalIgnoreCase))
        {
            var refName = group.Key;
            var members = group.ToList();

            // Type-konsistens-tjek: bland ikke fx Unity og C# i samme reference.
            var distinctTypes = members.Select(m => m.Type).Distinct().ToList();
            if (distinctTypes.Count > 1)
            {
                Console.Error.WriteLine(
                    $"⚠️ Reference '{refName}' samler projekter af flere typer ({string.Join(", ", distinctTypes)}). " +
                    $"Springer over - ret code2web-mapping.txt så hver type har sit eget navn.");
                continue;
            }
            var type = distinctTypes[0];

            // Union af tokeniserede stier på tværs af alle medlemmer (VS+Rider osv.).
            var pathSet = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            int totalSourceFiles = 0;
            bool anyMultiProject = false;
            foreach (var (root, _, _, _) in members)
            {
                var folderName = Path.GetFileName(root);
                var token = projectTokens.TryGetValue(Path.GetFullPath(root), out var tk)
                    ? tk : folderName;
                if (!token.Equals(folderName, StringComparison.OrdinalIgnoreCase))
                    anyMultiProject = true;

                foreach (var file in ProfileFilesFor(root, type, options))
                {
                    var rel = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
                    pathSet.Add(TokenizePath(rel, token));
                    totalSourceFiles++;
                }
            }

            if (pathSet.Count == 0)
            {
                if (!options.Quiet)
                    Console.WriteLine($"  (sprunget over, ingen filer) {refName}");
                continue;
            }

            var refPath = Path.Combine(outputDir, refName + ".txt");
            bool existed = File.Exists(refPath);

            // Bevar håndsatte 't'/'i'-markeringer fra en evt. eksisterende reference.
            var existingTags = ReadReferenceTags(refPath);

            var sb = new StringBuilder();
            sb.AppendLine($"# Code2Web reference: {refName}");
            sb.AppendLine($"# genereret {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine("schema;4");
            sb.AppendLine($"name;{refName}");
            sb.AppendLine($"type;{type.ToString().ToLowerInvariant()}");
            if (members.Count > 1)
                sb.AppendLine($"# flettet fra: {string.Join(" + ", members.Select(m => m.Key))}");
            if (anyMultiProject)
                sb.AppendLine("# multiprojekt: tokeniseret på fælles navne-basis");
            sb.AppendLine("# format: tag;sti   tag = t (skabelon) eller i (skabelon, interessant)");
            sb.AppendLine("# {PROJECT} = elevprojektets navn");
            foreach (var p in pathSet)
            {
                // Default 't'; behold håndsatte 'i'- og 's'-tags ved gen-kørsel.
                var tag = 't';
                if (existingTags.TryGetValue(p, out var t) && (t == 'i' || t == 's'))
                    tag = t;
                sb.AppendLine($"{tag};{p}");
            }

            File.WriteAllText(refPath, sb.ToString(), Encoding.UTF8);
            written++;

            if (!options.Quiet)
            {
                var status = existed ? "opdateret" : "ny";
                var mergeNote = members.Count > 1 ? $", flettet fra {members.Count} projekter" : "";
                var keptNote = existingTags.Values.Count(v => v == 'i') > 0
                    ? $", {existingTags.Values.Count(v => v == 'i')} 'i' bevaret" : "";
                Console.WriteLine($"  [{type}] {refName}  ({pathSet.Count} unikke stier{mergeNote}{keptNote}) - {status}");
            }
        }

        // Additivt: føj nyopdagede projekter til mapping-filen uden at røre eksisterende linjer.
        if (newMappingKeys.Count > 0)
        {
            AppendToMappingFile(mappingPath, newMappingKeys, !File.Exists(mappingPath) || mapping.Count == 0);
            if (!options.Quiet)
            {
                Console.WriteLine();
                Console.WriteLine($"📝 {newMappingKeys.Count} nyt/nye projekt(er) føjet til {MappingFileName} - " +
                                  "gennemgå reference-navnene og kør igen.");
            }
        }

        if (!options.Quiet)
        {
            Console.WriteLine();
            Console.WriteLine($"✅ {written} reference(r) skrevet til: {outputDir}");
        }
        return 0;
    }

    // Læs code2web-mapping.txt -> dict(projekt-sti -> reference-navn). Begge felter trimmes.
    private static Dictionary<string, string> ReadMappingFile(string mappingPath)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(mappingPath)) return map;

        try
        {
            foreach (var raw in File.ReadAllLines(mappingPath, Encoding.UTF8))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                var idx = line.IndexOf(';');
                if (idx < 0) continue;

                var key = line.Substring(0, idx).Trim().Replace('\\', '/');
                var val = line.Substring(idx + 1).Trim();
                if (key.Length > 0 && val.Length > 0)
                    map[key] = val;
            }
        }
        catch { /* korrupt mapping ignoreres - alt bliver da nyt */ }

        return map;
    }

    private static void AppendToMappingFile(
        string mappingPath, List<(string Key, string Suggested)> entries, bool writeHeader)
    {
        var sb = new StringBuilder();
        if (writeHeader)
        {
            sb.AppendLine("# code2web-mapping  -  rediger hoejre kolonne; flere linjer med samme navn flettes");
            sb.AppendLine("# projekt-sti (relativ til udtraeks-mappen) ; reference-navn");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine($"# tilfoejet {DateTime.Now:yyyy-MM-dd HH:mm} - gennemgaa navnene:");
        }

        int pad = entries.Max(e => e.Key.Length);
        foreach (var (key, suggested) in entries.OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase))
            sb.AppendLine($"{key.PadRight(pad)} ; {suggested}");

        File.AppendAllText(mappingPath, sb.ToString(), Encoding.UTF8);
    }

    // Forslag til reference-navn: mappenavn uden efterstillede cifre.
    private static string SuggestReferenceName(string folderName)
    {
        var name = SanitizeReferenceName(folderName);
        var trimmed = name.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
        return trimmed.Length > 0 ? trimmed : name;
    }

    // =================================================================
    //  Prerun  (--prerun)
    // -----------------------------------------------------------------
    //  For hvert produkt (niveau-2-mappe under elev-roden) detekteres
    //  projekterne, hver fil klassificeres mod den tildelte reference,
    //  og der skrives en code2web-plan.txt i produktmappen:
    //     [projekt] <navn> ; reference=<navn> ; type=<type>
    //     t;<sti>      (skabelon)
    //     i;<sti>      (skabelon, interessant)
    //     s;<sti>      (elev-bidrag)
    //  --prerun alene: flettes additivt (nye filer tilføjes, dine tags bevares).
    //  --prerun --overwrite: plan-filen genskabes fra bunden.
    // =================================================================
    private const string PlanFileName = "code2web-plan.txt";

    private sealed class Reference
    {
        public string Name = "";
        public ProjectType Type = ProjectType.Generic;
        // tokeniseret sti -> 't' eller 'i'
        public Dictionary<string, char> Paths = new(StringComparer.OrdinalIgnoreCase);
    }

    private static int RunPrerun(Options options)
    {
        string baseDir = options.BaseDirectory ?? Directory.GetCurrentDirectory();
        if (!Directory.Exists(baseDir))
        {
            Console.Error.WriteLine($"❌ Rodmappen findes ikke: {baseDir}");
            return 1;
        }

        // Reference-bibliotek: samme placering som --make-reference skriver til.
        string refDir;
        if (!string.IsNullOrWhiteSpace(options.OutputDirectory))
        {
            refDir = options.OutputDirectory!;
        }
        else
        {
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrWhiteSpace(docs)) docs = baseDir;
            refDir = Path.Combine(docs, "Code2Web", "references");
        }

        var references = LoadReferences(refDir, out int localRefs, out int shippedRefs);

        if (!options.Quiet)
        {
            var shipped = GetShippedReferencesDir();
            Console.WriteLine("Prerun-tilstand");
            Console.WriteLine($"Elev-rod   : {baseDir}");
            Console.WriteLine($"Referencer : {refDir}  ({localRefs} lokal{(localRefs == 1 ? "" : "e")})");
            if (shipped is not null)
                Console.WriteLine($"  + shipped: {shipped}  ({shippedRefs} brugt, øvrige overstyret af lokale)");
            Console.WriteLine($"  i alt    : {references.Count} reference(r) indlæst");
            Console.WriteLine($"Tilstand   : {(options.Overwrite ? "overwrite (genskab)" : "additiv fletning")}");
            Console.WriteLine();
        }

        // Produkter = niveau-2-mapper (én pr. elev/gruppe), minus støj.
        var products = Directory.GetDirectories(baseDir)
            .Where(d =>
            {
                var name = Path.GetFileName(d);
                return !IsMacOsXDir(name) && !DiscoveryExcludedFolders.Contains(name);
            })
            .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (products.Length == 0)
        {
            Console.Error.WriteLine("⚠️ Ingen produkt-mapper fundet under elev-roden.");
            return 1;
        }

        int planCount = 0;
        foreach (var productDir in products)
        {
            var projects = FindProjectsInGroup(productDir, options, ProjectType.Auto);
            var plan = BuildProductPlan(productDir, projects, references, options);

            var planPath = Path.Combine(productDir, PlanFileName);
            WritePlanFile(planPath, plan, options.Overwrite, options);
            planCount++;

            if (!options.Quiet)
            {
                int t = plan.Sum(p => p.Files.Count(f => f.Tag == 't'));
                int i = plan.Sum(p => p.Files.Count(f => f.Tag == 'i'));
                int s = plan.Sum(p => p.Files.Count(f => f.Tag == 's'));
                Console.WriteLine($"  {Path.GetFileName(productDir)}  " +
                                  $"({plan.Count} projekt(er): {s} bidrag, {i} interessant, {t} skabelon)");
            }
        }

        if (!options.Quiet)
        {
            Console.WriteLine();
            Console.WriteLine($"✅ {planCount} plan-fil(er) skrevet. Gennemgå dem og ret tags efter behov.");
        }
        return 0;
    }

    // ----- plan-datamodel -----
    private sealed class PlanFileLine
    {
        public char Tag;            // 's', 'i', 't'
        public string Path = "";    // relativ til produktmappen
    }

    private sealed class PlanProject
    {
        public string Name = "";
        public ProjectType Type;
        public string ReferenceName = "";   // tom = ingen reference fundet
        public List<PlanFileLine> Files = new();
    }

    // Indlæs alle reference-filer (*.txt) fra references-mappen.
    // Find shipped references-mappe ved siden af binæren (hvis publish lagde
    // en med). Returnerer null hvis ingen blev shippet.
    private static string? GetShippedReferencesDir()
    {
        try
        {
            var exeDir = AppContext.BaseDirectory;
            if (string.IsNullOrWhiteSpace(exeDir)) return null;
            var candidate = Path.Combine(exeDir, "references");
            return Directory.Exists(candidate) ? candidate : null;
        }
        catch { return null; }
    }

    // Indlæs alle reference-filer. Lokale (i refDir) vinder over shipped (ved
    // siden af binæren), så en bruger der laver en lokal reference med samme
    // navn overstyrer den shippede. Tæller pr. kilde rapporteres ud.
    private static Dictionary<string, Reference> LoadReferences(
        string refDir, out int localCount, out int shippedCount)
    {
        var refs = new Dictionary<string, Reference>(StringComparer.OrdinalIgnoreCase);

        // Lokale først (vinder).
        localCount = LoadReferencesFromDir(refDir, refs);

        // Derefter shipped som fallback - kun navne der ikke allerede er indlæst.
        var shipped = GetShippedReferencesDir();
        shippedCount = shipped is null ? 0 : LoadReferencesFromDir(shipped, refs, skipExisting: true);

        return refs;
    }

    // Bagudkompatibel overload (uden statistik) - bruges af RunPrerun.
    private static Dictionary<string, Reference> LoadReferences(string refDir)
        => LoadReferences(refDir, out _, out _);

    private static int LoadReferencesFromDir(
        string refDir, Dictionary<string, Reference> refs, bool skipExisting = false)
    {
        if (!Directory.Exists(refDir)) return 0;

        int count = 0;
        foreach (var file in Directory.GetFiles(refDir, "*.txt"))
        {
            if (Path.GetFileName(file).Equals(MappingFileName, StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                var r = new Reference { Name = Path.GetFileNameWithoutExtension(file) };
                foreach (var raw in File.ReadAllLines(file, Encoding.UTF8))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;

                    if (line.StartsWith("name;", StringComparison.OrdinalIgnoreCase))
                    { r.Name = line.Substring(5).Trim(); continue; }
                    if (line.StartsWith("type;", StringComparison.OrdinalIgnoreCase))
                    { r.Type = ParseProjectType(line.Substring(5).Trim()); continue; }
                    if (line.StartsWith("schema;", StringComparison.OrdinalIgnoreCase)) continue;

                    // Datalinje: 'tag;sti' (nyt) eller bar sti (gammelt -> 't').
                    // Tags: t=skabelon, i=skabelon-interessant, s=altid-bidrag.
                    char tag = 't';
                    string path = line;
                    if (line.Length > 1 && line[1] == ';' &&
                        (char.ToLowerInvariant(line[0]) is 't' or 'i' or 's'))
                    {
                        tag = char.ToLowerInvariant(line[0]);
                        path = line.Substring(2).Trim();
                    }
                    if (path.Length > 0)
                        r.Paths[path] = tag;
                }
                if (r.Paths.Count > 0)
                {
                    if (skipExisting && refs.ContainsKey(r.Name)) continue;
                    refs[r.Name] = r;
                    count++;
                }
            }
            catch { /* korrupt reference springes over */ }
        }
        return count;
    }

    // Byg planen for ét produkt: klassificér hvert projekts filer.
    private static List<PlanProject> BuildProductPlan(
        string productDir, List<ProjectSpec> projects,
        Dictionary<string, Reference> references, Options options)
    {
        var result = new List<PlanProject>();

        foreach (var spec in projects)
        {
            var pp = new PlanProject { Name = spec.ProjectName, Type = spec.Type };

            // Vælg reference: marker kan navngive den eksplicit, ellers gæt ud fra type+overlap.
            var reference = ResolveReference(spec, references, options);
            pp.ReferenceName = reference?.Name ?? "";

            // Elevprojektets faktiske filer (samme profil-handler som generering bruger).
            var files = ProfileFilesFor(spec.ProjectRoot, spec.Type, options);

            // Token til at oversætte elevens projektnavn -> {PROJECT}.
            var token = ComputeProjectTokenForGroup(projects, spec);

            foreach (var file in files)
            {
                var relProduct = Path.GetRelativePath(productDir, file)
                    .Replace(Path.DirectorySeparatorChar, '/');
                if (Path.GetFileName(file).Equals(PlanFileName, StringComparison.OrdinalIgnoreCase))
                    continue;

                char tag = 's';
                if (reference is not null)
                {
                    var relProject = Path.GetRelativePath(spec.ProjectRoot, file)
                        .Replace(Path.DirectorySeparatorChar, '/');
                    var tokenized = TokenizePath(relProject, token);
                    if (reference.Paths.TryGetValue(tokenized, out var refTag))
                        tag = refTag;   // 't', 'i' eller 's' (sidstnævnte = "altid-bidrag")
                }

                pp.Files.Add(new PlanFileLine { Tag = tag, Path = relProduct });
            }

            pp.Files = pp.Files
                .OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            result.Add(pp);
        }

        return result;
    }

    // Token for ét projekt i konteksten af sine søskende (multiprojekt -> fælles basis).
    private static string ComputeProjectTokenForGroup(List<ProjectSpec> projects, ProjectSpec spec)
    {
        var asTuples = projects.Select(p => (p.ProjectRoot, p.Type)).ToList();
        var tokens = ComputeProjectTokens(asTuples);
        return tokens.TryGetValue(Path.GetFullPath(spec.ProjectRoot), out var tk)
            ? tk : Path.GetFileName(spec.ProjectRoot);
    }

    // Vælg reference til et projekt. Marker med name= der matcher en reference
    // vinder; ellers vælges den reference af samme type med størst sti-overlap.
    private static Reference? ResolveReference(
        ProjectSpec spec, Dictionary<string, Reference> references, Options options)
    {
        // 1) Eksplicit via marker-navn.
        if (spec.Marker is not null && !string.IsNullOrWhiteSpace(spec.Marker.Name)
            && references.TryGetValue(spec.Marker.Name!, out var named))
            return named;

        // 2) Gæt: bedste sti-overlap blandt referencer af samme type.
        var candidates = references.Values.Where(r => r.Type == spec.Type).ToList();
        if (candidates.Count == 0) return null;

        var files = ProfileFilesFor(spec.ProjectRoot, spec.Type, options);
        if (files.Count == 0) return null;

        var token = Path.GetFileName(spec.ProjectRoot);
        var projectPaths = files
            .Select(f => TokenizePath(
                Path.GetRelativePath(spec.ProjectRoot, f).Replace(Path.DirectorySeparatorChar, '/'),
                token))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Reference? best = null;
        double bestScore = 0;
        foreach (var r in candidates)
        {
            int overlap = r.Paths.Keys.Count(p => projectPaths.Contains(p));
            // Andel af referencens filer der genfindes i projektet.
            double score = (double)overlap / r.Paths.Count;
            if (score > bestScore)
            {
                bestScore = score;
                best = r;
            }
        }

        // Kræv et minimum af overlap, ellers er gættet for usikkert.
        return bestScore >= 0.5 ? best : null;
    }

    // Skriv (eller flet) plan-filen for ét produkt.
    private static void WritePlanFile(
        string planPath, List<PlanProject> plan, bool overwrite, Options options)
    {
        // Additiv fletning: bevar tags fra en eksisterende plan, tilføj kun nye filer.
        Dictionary<string, char>? existing = null;
        if (!overwrite && File.Exists(planPath))
            existing = ReadPlanTags(planPath);

        var sb = new StringBuilder();
        sb.AppendLine($"# code2web-plan: {Path.GetFileName(Path.GetDirectoryName(planPath))}");
        sb.AppendLine($"# genereret {DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine("# format: tag;sti   s=bidrag  i=skabelon-interessant  t=skabelon");
        sb.AppendLine("# stier er relative til denne produktmappe; ret tags frit.");

        foreach (var pp in plan)
        {
            sb.AppendLine();
            sb.AppendLine($"[projekt] {pp.Name} ; reference={pp.ReferenceName} ; type={pp.Type.ToString().ToLowerInvariant()}");

            int merged = 0;
            foreach (var f in pp.Files)
            {
                var tag = f.Tag;
                // Bevar en håndsat tag fra forrige plan, hvis filen fandtes der.
                if (existing is not null && existing.TryGetValue(f.Path, out var prev))
                {
                    if (prev != tag) merged++;
                    tag = prev;
                }
                sb.AppendLine($"{tag};{f.Path}");
            }
        }

        File.WriteAllText(planPath, sb.ToString(), Encoding.UTF8);
    }

    // Læs sti->tag fra en eksisterende plan-fil (på tværs af alle projekter).
    private static Dictionary<string, char> ReadPlanTags(string planPath)
    {
        var tags = new Dictionary<string, char>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var raw in File.ReadAllLines(planPath, Encoding.UTF8))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("[")) continue;
                if (line.Length > 1 && line[1] == ';' &&
                    (char.ToLowerInvariant(line[0]) is 's' or 'i' or 't'))
                {
                    var tag = char.ToLowerInvariant(line[0]);
                    var path = line.Substring(2).Trim();
                    if (path.Length > 0)
                        tags[path] = tag;
                }
            }
        }
        catch { /* korrupt plan ignoreres - alt klassificeres da forfra */ }
        return tags;
    }


    // Find projekter til reference-udtræk. I modsætning til elev-discovery
    // KOLLAPSES ikke: hvert .csproj-/Unity-/package.json-projekt bliver sin
    // egen reference, så en solution med flere skabeloner giver flere filer.
    private static List<(string Root, ProjectType Type)> DiscoverProjectsForReference(string root)
    {
        var found = new List<(string, ProjectType)>();
        DiscoverForReferenceInternal(root, found);
        return found
            .GroupBy(r => Path.GetFullPath(r.Item1), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(r => r.Item1, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void DiscoverForReferenceInternal(string dir, List<(string, ProjectType)> found)
    {
        var name = Path.GetFileName(dir);
        if (DiscoveryExcludedFolders.Contains(name)) return;
        if (name.Equals("code2web-references", StringComparison.OrdinalIgnoreCase)) return;

        // Unity / Node: projektet er roden -> registrér og stop.
        if (Directory.Exists(Path.Combine(dir, "Assets")) &&
            Directory.Exists(Path.Combine(dir, "ProjectSettings")))
        {
            found.Add((dir, ProjectType.Unity));
            return;
        }
        if (File.Exists(Path.Combine(dir, "package.json")))
        {
            found.Add((dir, IsNextProject(dir) ? ProjectType.Next : ProjectType.Node));
            return;
        }

        // C#: en mappe der direkte indeholder en .csproj er ét projekt.
        bool hasCsproj = false;
        try { hasCsproj = Directory.GetFiles(dir, "*.csproj").Length > 0; } catch { }
        if (hasCsproj)
            found.Add((dir, ProjectType.CSharp));

        // Arduino: en mappe med .ino.
        bool hasIno = false;
        try { hasIno = Directory.GetFiles(dir, "*.ino").Length > 0; } catch { }
        if (hasIno && !hasCsproj)
            found.Add((dir, ProjectType.Arduino));

        // Gå videre ned (en solution-mappe uden .csproj indeholder projekt-undermapper).
        string[] subs;
        try { subs = Directory.GetDirectories(dir); }
        catch { return; }
        foreach (var sub in subs)
            DiscoverForReferenceInternal(sub, found);
    }

    // Kør den relevante profil-handler og få dens fil-liste.
    private static List<string> ProfileFilesFor(string root, ProjectType type, Options options)
        => type switch
        {
            ProjectType.Unity => HandleUnityProject(root, options, null),
            ProjectType.Next => HandleNextProject(root, options, null),
            ProjectType.Node => HandleNodeProject(root, options, null),
            ProjectType.CSharp => HandleCSharpProject(root, options, null),
            ProjectType.Arduino => HandleArduinoProject(root, options, null),
            _ => HandleGenericProject(root, options, null)
        };

    // Erstat projektnavnet i hvert sti-segment med {PROJECT}, så referencen
    // matcher elevprojekter med andre navne. Håndterer både "Navn" som helt
    // segment og "Navn.csproj" / "Navn.Desktop" (filnavn / mappe-head).
    private static string TokenizePath(string relPath, string token)
    {
        if (string.IsNullOrEmpty(token)) return relPath;

        var segments = relPath.Split('/');
        for (int i = 0; i < segments.Length; i++)
        {
            var seg = segments[i];
            if (seg.Equals(token, StringComparison.OrdinalIgnoreCase))
                segments[i] = ProjectToken;
            else if (seg.StartsWith(token + ".", StringComparison.OrdinalIgnoreCase))
                segments[i] = ProjectToken + seg.Substring(token.Length);
        }
        return string.Join('/', segments);
    }

    // Bestem tokeniserings-token pr. projekt. For et multiprojekt – flere
    // søskende-projekter i samme mappe der deler navne-basis (fx App,
    // App.Desktop, App.Android) – bruges den fælles basis som token, så
    // referencen matcher uanset hvad eleven kalder sit projekt. Et enkelt-
    // stående projekt får sit eget mappenavn som token.
    private static Dictionary<string, string> ComputeProjectTokens(
        List<(string Root, ProjectType Type)> projects)
    {
        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var siblingGroup in projects.GroupBy(p =>
        {
            var parent = Path.GetDirectoryName(Path.GetFullPath(p.Root));
            return parent ?? Path.GetFullPath(p.Root);
        }, StringComparer.OrdinalIgnoreCase))
        {
            var members = siblingGroup.ToList();
            var names = members.Select(m => Path.GetFileName(m.Root)).ToList();

            string? commonBase = members.Count >= 2 ? FindCommonProjectBase(names) : null;

            foreach (var m in members)
                tokens[Path.GetFullPath(m.Root)] = commonBase ?? Path.GetFileName(m.Root);
        }

        return tokens;
    }

    // Find længste basis B, så hvert navn enten ER B eller starter med B + "."
    // (og mindst ét navn har et suffiks). Ellers null = ikke et multiprojekt.
    private static string? FindCommonProjectBase(List<string> names)
    {
        if (names.Count < 2) return null;

        string lcp = names[0];
        foreach (var n in names.Skip(1))
        {
            int i = 0, max = Math.Min(lcp.Length, n.Length);
            while (i < max && char.ToLowerInvariant(lcp[i]) == char.ToLowerInvariant(n[i])) i++;
            lcp = lcp.Substring(0, i);
            if (lcp.Length == 0) return null;
        }

        // Hvis basis ikke selv er et helt navn, trim tilbage til sidste '.'.
        if (!names.Any(n => n.Equals(lcp, StringComparison.OrdinalIgnoreCase)))
        {
            int dot = lcp.LastIndexOf('.');
            if (dot <= 0) return null;
            lcp = lcp.Substring(0, dot);
        }
        if (lcp.Length == 0) return null;

        // Validér: hvert navn skal være B eller B + "." + rest.
        foreach (var n in names)
        {
            bool ok = n.Equals(lcp, StringComparison.OrdinalIgnoreCase)
                   || n.StartsWith(lcp + ".", StringComparison.OrdinalIgnoreCase);
            if (!ok) return null;
        }

        // Mindst ét navn skal have et suffiks – ellers er det ikke et multiprojekt.
        if (!names.Any(n => n.Length > lcp.Length)) return null;

        return lcp;
    }

    private static string SanitizeReferenceName(string name)
    {
        var clean = new string((name ?? "")
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '-')
            .ToArray()).Trim('-', '.');
        return string.IsNullOrWhiteSpace(clean) ? "reference" : clean;
    }

    // Læs en reference-fils sti->tag. Tolererer både nyt format 'tag;sti'
    // og gammelt format med bar sti (tolkes som 't').
    private static Dictionary<string, char> ReadReferenceTags(string refPath)
    {
        var tags = new Dictionary<string, char>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(refPath)) return tags;

        try
        {
            foreach (var raw in File.ReadAllLines(refPath, Encoding.UTF8))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                // Header-linjer (schema/name/type) springes over.
                if (line.StartsWith("schema;", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("name;", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("type;", StringComparison.OrdinalIgnoreCase))
                    continue;

                char tag = 't';
                string path = line;

                var idx = line.IndexOf(';');
                if (idx == 1 && (line[0] is 't' or 'i' or 's' or 'T' or 'I' or 'S'))
                {
                    tag = char.ToLowerInvariant(line[0]);
                    path = line.Substring(2).Trim();
                }

                if (path.Length > 0)
                    tags[path] = tag;
            }
        }
        catch { /* korrupt reference ignoreres - alt bliver da 't' */ }

        return tags;
    }

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

        // Vis aldrig selve marker- eller plan-filen i outputtet.
        files = files
            .Where(f =>
            {
                var n = Path.GetFileName(f);
                return !n.Equals(options.MarkerFileName, StringComparison.OrdinalIgnoreCase)
                    && !n.Equals(PlanFileName, StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        // Klassificer hver fil via plan-filen i produktmappen (groupDir).
        // Stier i planen er relative til produktmappen.
        var planTags = ReadPlanTags(Path.Combine(groupDir, PlanFileName));

        // Advarsel hvis en plan-fil ligger fejlplaceret i en projektmappe.
        var strayPlan = Path.Combine(project.ProjectRoot, PlanFileName);
        if (!Path.GetFullPath(project.ProjectRoot).Equals(Path.GetFullPath(groupDir), StringComparison.OrdinalIgnoreCase)
            && File.Exists(strayPlan))
        {
            Console.Error.WriteLine(
                $"⚠️  Plan-fil ignoreret (forkert placeret): {strayPlan}\n" +
                $"    Plan-filer hører til i produktmappen ({groupDir}), ikke i projektmapper.");
        }

        var bucketContrib = new List<string>();   // 's'
        var bucketInteresting = new List<string>(); // 'i'
        var bucketTemplate = new List<string>();  // 't'

        foreach (var file in files)
        {
            var relProduct = Path.GetRelativePath(groupDir, file).Replace(Path.DirectorySeparatorChar, '/');
            // Default uden plan = alt er bidrag (sikker retning).
            char tag = 's';
            if (planTags.TryGetValue(relProduct, out var t)) tag = t;

            switch (tag)
            {
                case 'i': bucketInteresting.Add(file); break;
                case 't': bucketTemplate.Add(file); break;
                default:  bucketContrib.Add(file); break;
            }
        }

        // Find ressourcer (kun navne, intet indhold).
        var resources = FindResourceFiles(project.ProjectRoot, project.Type, options);

        bool anyShown = bucketContrib.Count + bucketInteresting.Count + bucketTemplate.Count + resources.Count > 0;
        if (!anyShown)
        {
            html.AppendLine("<p><em>Ingen relevante filer fundet.</em></p>");
            return;
        }

        // Render sektionerne i prioritetsorden: Bidrag øverst, derefter Interessant,
        // til sidst Skabelon (kollapset som default). Hvis der ingen plan er, vises kun "Kildefiler".
        bool planUsed = planTags.Count > 0;

        if (!planUsed)
        {
            WriteFilesBlock(html, project.ProjectRoot, bucketContrib, openByDefault: false, sectionId: $"{anchor}-src", sectionTitle: "Kildefiler");
        }
        else
        {
            if (bucketContrib.Count > 0)
                WriteFilesBlock(html, project.ProjectRoot, bucketContrib, openByDefault: false,
                                sectionId: $"{anchor}-bidrag", sectionTitle: $"Bidrag ({bucketContrib.Count})");
            if (bucketInteresting.Count > 0)
                WriteFilesBlock(html, project.ProjectRoot, bucketInteresting, openByDefault: false,
                                sectionId: $"{anchor}-interessant", sectionTitle: $"Skabelon – interessant ({bucketInteresting.Count})");
            if (bucketTemplate.Count > 0)
                WriteFilesBlock(html, project.ProjectRoot, bucketTemplate, openByDefault: false,
                                sectionId: $"{anchor}-skabelon", sectionTitle: $"Skabelon ({bucketTemplate.Count})");
        }

        if (resources.Count > 0)
            WriteResourcesBlock(html, project.ProjectRoot, resources, $"{anchor}-resources");
    }

    // Render én sektions filer som <details>-blokke under en H3.
    private static void WriteFilesBlock(
        StringBuilder html, string projectRoot, List<string> files,
        bool openByDefault, string sectionId, string sectionTitle)
    {
        html.AppendLine($"<h3 id=\"{sectionId}\" style=\"margin-top:1.2rem;\">{EscapeHtml(sectionTitle)}</h3>");
        foreach (var file in files)
        {
            string relative = Path.GetRelativePath(projectRoot, file)
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

            var openAttr = openByDefault ? " open" : "";
            html.AppendLine($"<details{openAttr}>");
            html.AppendLine($"<summary><strong>{EscapeHtml(relative)}</strong></summary>");
            html.AppendLine($"<pre><code class=\"{lang}\">");
            html.AppendLine(System.Net.WebUtility.HtmlEncode(content));
            html.AppendLine("</code></pre>");
            html.AppendLine("</details>");
        }
    }

    // Render ressource-listen (kun navne, intet indhold) som en enkelt liste under en H3.
    private static void WriteResourcesBlock(
        StringBuilder html, string projectRoot, List<string> resources, string sectionId)
    {
        html.AppendLine($"<h3 id=\"{sectionId}\" style=\"margin-top:1.2rem;\">Ressourcer ({resources.Count})</h3>");
        html.AppendLine("<details>");
        html.AppendLine($"<summary><strong>Vis fil-liste ({resources.Count})</strong></summary>");
        html.AppendLine("<ul style=\"font-family:monospace; font-size:0.9rem;\">");
        foreach (var file in resources)
        {
            var rel = Path.GetRelativePath(projectRoot, file).Replace(Path.DirectorySeparatorChar, '/');
            html.AppendLine($"  <li>{EscapeHtml(rel)}</li>");
        }
        html.AppendLine("</ul>");
        html.AppendLine("</details>");
    }

    // ----- Ressource-scanner -----
    // Lister filNAVNE (ikke indhold) for assets/ressourcer. Almindelige projekter:
    // billed-/lyd-filer. Unity-projekter: scener, prefabs, materialer, texturer,
    // 3D-modeller, lyd - men aldrig fra Assets/Settings eller TextMesh Pro, og
    // aldrig .meta- eller (default) .asset-filer.
    private static readonly string[] GeneralResourceExtensions = {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".svg", ".ico", ".webp",
        ".wav", ".mp3", ".ogg"
    };

    private static readonly string[] UnityResourceExtensions = {
        ".prefab", ".unity", ".mat",
        ".png", ".jpg", ".jpeg", ".tga", ".psd",
        ".fbx", ".obj",
        ".wav", ".mp3", ".ogg"
    };

    private static readonly HashSet<string> UnityResourceExcludedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Settings",           // skabelon-render-pipeline-assets
        "TextMesh Pro",
        "Plugins",
        "Standard Assets",
        "Library", "Temp", "Logs", "Build", "UserSettings", "__MACOSX",
        "obj", "bin", ".vs", ".idea"
    };

    private static List<string> FindResourceFiles(string projectRoot, ProjectType type, Options options)
    {
        var result = new List<string>();
        if (type == ProjectType.Unity)
        {
            var assets = Path.Combine(projectRoot, "Assets");
            if (Directory.Exists(assets))
                CollectResources(assets, UnityResourceExtensions, UnityResourceExcludedFolders, result);
        }
        else
        {
            var excl = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "node_modules", "obj", "bin", "out", "dist",
                ".git", ".svn", ".hg", ".vs", ".idea", ".vscode", ".fleet",
                ".next", ".turbo", ".cache", ".vercel",
                "__MACOSX"
            };
            CollectResources(projectRoot, GeneralResourceExtensions, excl, result);
        }

        return result
            .OrderBy(f => Path.GetRelativePath(projectRoot, f), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void CollectResources(
        string dir, string[] allowedExtensions, HashSet<string> excludedFolders, List<string> result)
    {
        var name = Path.GetFileName(dir);
        if (excludedFolders.Contains(name)) return;

        string[] files;
        try { files = Directory.GetFiles(dir); }
        catch { return; }

        foreach (var f in files)
        {
            // Aldrig .meta uanset hvad.
            if (Path.GetExtension(f).Equals(".meta", StringComparison.OrdinalIgnoreCase)) continue;
            var ext = Path.GetExtension(f).ToLowerInvariant();
            foreach (var allowed in allowedExtensions)
            {
                if (ext == allowed)
                {
                    result.Add(f);
                    break;
                }
            }
        }

        string[] subs;
        try { subs = Directory.GetDirectories(dir); }
        catch { return; }
        foreach (var sub in subs)
            CollectResources(sub, allowedExtensions, excludedFolders, result);
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
        if (AlwaysNoiseFolders.Contains(name)) yield break;
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
  <button onclick='toggleTemplate()'>Vis/skjul skabelonkode</button>
</div>
<script>
  function expandAll() {
    document.querySelectorAll('details').forEach(d => d.open = true);
  }
  function collapseAll() {
    document.querySelectorAll('details').forEach(d => d.open = false);
  }
  function toggleTemplate() {
    document.querySelectorAll('h3[id$=""-skabelon""]').forEach(h => {
      var hidden = h.style.display === 'none';
      h.style.display = hidden ? '' : 'none';
      var n = h.nextElementSibling;
      while (n && n.tagName !== 'H3' && n.tagName !== 'HR') {
        n.style.display = hidden ? '' : 'none';
        n = n.nextElementSibling;
      }
    });
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
