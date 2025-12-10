using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace cliCode2Web;

internal class Program
{
    // Filtyper vi KAN håndtere, men .json kan ekskluderes via flag
    static readonly string[] BaseAllowedExtensions = {
            ".cs", ".csh", ".cpp", ".h", ".c", ".ino", ".java", ".py",
            ".csproj", ".xaml", ".html", ".shader", ".cginc",
            ".txt", ".md", ".xml", ".uxml", ".asmdef"
            // .json håndteres via --include-json
        };

    static readonly string JsonExtension = ".json";

    // ---------- Options ----------
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
            HelpText = "Rekursionsdybde i elevmapper (default = 5).")]
        public int MaxDepth { get; set; }

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
            HelpText = "Inkludér .json filer (default: false).")]
        public bool IncludeJson { get; set; }

        [Option('q', "quiet", Required = false,
            HelpText = "Kører uden ekstra statuslinjer.")]
        public bool Quiet { get; set; }
    }

    // ---------- Main ----------
    static int Main(string[] args)
    {
        var parser = new Parser(cfg => cfg.HelpWriter = null);
        var result = parser.ParseArguments<Options>(args);

        return result.MapResult(
            opts => RunWithOptions(opts),
            errs => DisplayHelp(result, errs)
        );
    }

    private static int RunWithOptions(Options options)
    {
        string baseDir = options.BaseDirectory ?? Directory.GetCurrentDirectory();
        if (!Directory.Exists(baseDir))
        {
            Console.Error.WriteLine($"❌ Rodmappen findes ikke: {baseDir}");
            return 1;
        }

        string outputDir;
        if (!string.IsNullOrWhiteSpace(options.OutputDirectory))
        {
            outputDir = options.OutputDirectory!;
        }
        else
        {
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrWhiteSpace(docs))
                docs = baseDir;

            outputDir = Path.Combine(docs, "Code2Web", options.ClassName);
        }

        Directory.CreateDirectory(outputDir);

        if (!options.Quiet)
        {
            Console.WriteLine($"Holdnavn         : {options.ClassName}");
            Console.WriteLine($"Base directory   : {baseDir}");
            Console.WriteLine($"Output directory : {outputDir}");
            Console.WriteLine($"Rekursionsdybde  : {options.MaxDepth}");
            Console.WriteLine($"Include obj      : {options.IncludeObj}");
            Console.WriteLine($"Include bin      : {options.IncludeBin}");
            Console.WriteLine($"Include .idea    : {options.IncludeIdea}");
            Console.WriteLine($"Include .vs      : {options.IncludeVs}");
            Console.WriteLine($"Include .json    : {options.IncludeJson}");
            Console.WriteLine();
        }

        // Filtype-filter
        var allowedExtensions = new HashSet<string>(
            BaseAllowedExtensions,
            StringComparer.OrdinalIgnoreCase
        );

        if (options.IncludeJson)
            allowedExtensions.Add(JsonExtension);

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

        foreach (var dir in subdirs)
        {
            var groupName = Path.GetFileName(dir);
            var groupHtmlName = $"{groupName}.html";

            indexBuilder.AppendLine($"<li><a href=\"{groupHtmlName}\">{groupName}</a></li>");

            var files = EnumerateCodeFiles(
                    dir,
                    options.MaxDepth,
                    options.IncludeObj,
                    options.IncludeIdea,
                    options.IncludeVs,
                    options.IncludeBin)
                .Where(f =>
                {
                    var ext = Path.GetExtension(f).ToLower();
                    return allowedExtensions.Contains(ext);
                })
                .OrderBy(f => Path.GetRelativePath(dir, f), StringComparer.OrdinalIgnoreCase)
                .ToList();

            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html><html lang=\"da\"><head><meta charset=\"UTF-8\">");
            html.AppendLine($"<title>{groupName}</title>");
            html.AppendLine(HeadIncludes());
            html.AppendLine("</head><body>");
            html.AppendLine(BuildTopNav(groupName, subdirs));
            html.AppendLine($"<h1>{groupName}</h1>");
            html.AppendLine(BuildExpandCollapseButtons());

            foreach (var file in files)
            {
                string relative = Path.GetRelativePath(dir, file)
                    .Replace(Path.DirectorySeparatorChar, '/');

                var ext = Path.GetExtension(file).ToLower();
                var lang = GetLanguageClass(ext);
                var content = File.ReadAllText(file);

                if (ext == ".html")
                    content = NeutralizeScriptTags(content);

                html.AppendLine("<details>");
                html.AppendLine($"<summary><strong>{relative}</strong></summary>");
                html.AppendLine($"<pre><code class=\"{lang}\">");
                html.AppendLine(System.Net.WebUtility.HtmlEncode(content));
                html.AppendLine("</code></pre>");
                html.AppendLine("</details>");
            }

            html.AppendLine("</body></html>");
            File.WriteAllText(Path.Combine(outputDir, groupHtmlName), html.ToString(), Encoding.UTF8);
        }

        indexBuilder.AppendLine("</ul></body></html>");
        File.WriteAllText(Path.Combine(outputDir, "index.html"), indexBuilder.ToString(), Encoding.UTF8);

        if (!options.Quiet)
        {
            Console.WriteLine("✅ HTML-generering færdig.");
        }

        return 0;
    }

    // ---------- Help ----------
    private static int DisplayHelp<T>(ParserResult<T> result, IEnumerable<CommandLine.Error> errs)
        where T : class
    {
        bool isHelp = errs.IsHelp() || errs.IsVersion();

        var help = HelpText.AutoBuild(result, h =>
        {
            h.Heading = "cliCode2Web: Udtrækker kode fra solution og viser via HTML-dokumenter";
            h.AddPreOptionsLine("");
            h.AddPreOptionsLine("Syntax: cliCode2Web --class=holdnavn [options]");
            h.AddPreOptionsLine("");
            h.AddPreOptionsLine("Standard output     : <Documents>\\Code2Web\\<Holdnavn>");
            h.AddPreOptionsLine("Standard dybde      : 5");
            h.AddPreOptionsLine("Standard eksklusion : obj/, bin/, .idea/, .vs/, __MACOSX/, .json");
            h.AddPreOptionsLine("Tænd med            : --include-obj, --include-bin, --include-idea, --include-vs, --include-json");
            h.AddPreOptionsLine("");
            return HelpText.DefaultParsingErrorsHandler(result, h);
        }, e => e);

        Console.WriteLine(help);
        return isHelp ? 0 : 1;
    }

    // ---------- Fil-scan ----------
    private static IEnumerable<string> EnumerateCodeFiles(
        string root,
        int maxDepth,
        bool includeObj,
        bool includeIdea,
        bool includeVs,
        bool includeBin)
        => EnumerateCodeFilesInternal(
               root, 0, maxDepth,
               includeObj, includeIdea, includeVs, includeBin);

    private static IEnumerable<string> EnumerateCodeFilesInternal(
        string dir,
        int depth,
        int maxDepth,
        bool includeObj,
        bool includeIdea,
        bool includeVs,
        bool includeBin)
    {
        var name = Path.GetFileName(dir);

        if (IsMacOsXDir(name)) yield break;
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
                         includeObj, includeIdea, includeVs, includeBin))
                yield return f;
    }

    // ---------- Folder helpers ----------
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

    // ---------- Syntax highlighting ----------
    static string GetLanguageClass(string ext) => ext switch
    {
        ".cs" or ".csh" => "language-csharp",
        ".cpp" or ".h" or ".c" or ".ino" => "language-c",
        ".java" => "language-java",
        ".py" => "language-python",
        ".csproj" => "language-xml",
        ".xml" or ".xaml" or ".uxml" => "language-xml",
        ".json" or ".asmdef" => "language-json",
        ".html" => "language-html",
        ".shader" or ".cginc" => "language-cpp",
        ".md" => "language-markdown",
        ".txt" => "language-plaintext",
        _ => "language-plaintext"
    };

    static string BuildTopNav(string current, string[] subdirs)
    {
        var nav = new StringBuilder();
        nav.AppendLine("<div style=\"position:sticky;top:0;background:#eee;padding:0.5rem;border-bottom:1px solid #ccc;overflow-x:auto;white-space:nowrap;\">");

        nav.Append($"<a href=\"index.html\" style=\"margin-right:1rem;font-weight:{(current == "Index" ? "bold" : "normal")}\">Index</a>");

        foreach (var dir in subdirs)
        {
            var name = Path.GetFileName(dir);
            var isCurrent = name.Equals(current, StringComparison.OrdinalIgnoreCase);
            var display = name.Length <= 6 ? name : name[..5] + "…";

            nav.Append($"<a href=\"{name}.html\" style=\"margin-right:1rem;font-weight:{(isCurrent ? "bold" : "normal")}\">{display}</a>");
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

    static string NeutralizeScriptTags(string content)
    {
        return content
            .Replace("<script", "&lt;script", StringComparison.OrdinalIgnoreCase)
            .Replace("</script", "&lt;/script", StringComparison.OrdinalIgnoreCase);
    }
}
}
