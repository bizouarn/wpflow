using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace DocsSite.Docs;

/// <summary>A heading of the page, for the table of contents.</summary>
public sealed record Heading(int Level, string Id, string Text);

/// <summary>A piece of a page: HTML, then optionally an interactive example to insert after it.</summary>
public sealed record DocSegment(string Html, string? ExampleId);

public sealed record DocPage(string File, string Route, string Title, IReadOnlyList<DocSegment> Segments,
    IReadOnlyList<Heading> Headings);

public sealed record NavGroup(string Title, IReadOnlyList<DocPage> Pages);

/// <summary>
///     The repository documentation (<c>doc/**/*.md</c>, embedded in the assembly) converted into site pages.
///     Relative links become routes when they point to a page of <c>doc/</c>, and links to GitHub otherwise.
///     In a Markdown file, the line <c>&lt;!-- exemple-blazor: id --&gt;</c> (invisible on GitHub) inserts the
///     interactive example <c>id</c> at that place.
/// </summary>
public sealed partial class DocLibrary
{
    public const string Repository = "https://github.com/bizouarn/wpflow";
    private const string Prefix = "repo/";
    // Folder of the documentation in the repository, and the route root under which the site serves it.
    private const string Folder = "doc";
    private const string Root = "doc";
    private const string Index = Folder + "/README.md";

    private static readonly Assembly Assembly = typeof(DocLibrary).Assembly;

    // Les ancres suivent la règle de GitHub : les liens « page.md#section » marchent aux deux endroits.
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
        .UsePipeTables()
        .UseAutoLinks()
        .Build();

    // Chemin dans le dépôt -> nom de la ressource (sous Windows, %(RecursiveDir) garde des « \ »).
    private readonly Dictionary<string, string> _files = Assembly.GetManifestResourceNames()
        .Where(n => n.StartsWith(Prefix, StringComparison.Ordinal))
        .ToDictionary(n => n[Prefix.Length..].Replace('\\', '/'), n => n, StringComparer.Ordinal);

    private readonly Dictionary<string, DocPage> _pages = new(StringComparer.Ordinal);
    private IReadOnlyList<NavGroup>? _navigation;

    /// <summary>The page of a route (« doc », « doc/blazor », « doc/learn/05-edges »), or <c>null</c>.</summary>
    public DocPage? Find(string route)
    {
        route = route.Trim('/');
        var path = (route == Root ? Folder : Folder + route[Root.Length..]).TrimEnd('/');
        var file = _files.ContainsKey(path + ".md") ? path + ".md" : path + "/README.md";
        return _files.ContainsKey(file) ? Load(file) : null;
    }

    /// <summary>The page of a repository file (« doc/learn/05-edges.md »).</summary>
    public DocPage? FindFile(string file) => _files.ContainsKey(file) ? Load(file) : null;

    /// <summary>
    ///     The menu: one group per folder, pages in the order in which <c>doc/README.md</c> cites them, titles taken
    ///     from the first heading of each file. A page added to <c>doc/</c> appears without touching the site.
    /// </summary>
    public IReadOnlyList<NavGroup> Navigation => _navigation ??= BuildNavigation();

    private List<NavGroup> BuildNavigation()
    {
        var index = ReadText(Index);

        int Rank(string file)
        {
            if (file == Index) return -1;
            var at = index.IndexOf("](" + file[(Folder.Length + 1)..], StringComparison.Ordinal);
            return at < 0 ? int.MaxValue : at;
        }

        var pages = _files.Keys.Where(f => f.EndsWith(".md", StringComparison.Ordinal))
            .OrderBy(Rank).ThenBy(f => f, StringComparer.Ordinal)
            .Select(Load)
            .ToList();

        static bool InFolder(DocPage page, string folder) =>
            page.File.StartsWith(Folder + "/" + folder + "/", StringComparison.Ordinal);

        NavGroup Group(string title, Func<DocPage, bool> filter) => new(title, pages.Where(filter).ToList());

        return new[]
        {
            Group("Blazor", p => p.File == Folder + "/blazor.md"),
            Group("Guides", p => InFolder(p, "learn")),
            Group("API reference", p => InFolder(p, "reference")),
            Group("Other pages", p => p.File != Folder + "/blazor.md" && !InFolder(p, "learn") && !InFolder(p, "reference"))
        }.Where(g => g.Pages.Count > 0).ToList();
    }

    // « doc/learn/05-edges.md » -> « doc/learn/05-edges » ; « doc/README.md » -> « doc ».
    private static string RouteOf(string file)
    {
        var route = file[..^".md".Length];
        route = route.EndsWith("/README", StringComparison.Ordinal) ? route[..^"/README".Length] : route;
        return Root + route[Folder.Length..];
    }

    private DocPage Load(string file)
    {
        if (_pages.TryGetValue(file, out var cached)) return cached;

        var route = RouteOf(file);
        var document = Markdown.Parse(ReadText(file), Pipeline);

        foreach (var link in document.Descendants<LinkInline>().ToList())
            link.Url = Rewrite(file, route, link.Url ?? "", link.IsImage);

        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        Pipeline.Setup(renderer);
        renderer.Render(document);

        // Après le rendu : les identifiants des titres sont alors tous attribués.
        var headings = document.Descendants<HeadingBlock>()
            .Select(h => new Heading(h.Level, h.GetAttributes().Id ?? "", PlainText(h.Inline)))
            .ToList();

        // Les tableaux larges défilent seuls, sans élargir la page.
        var html = writer.ToString()
            .Replace("<table>", "<div class=\"table-scroll\"><table>")
            .Replace("</table>", "</table></div>");

        // Découpage avec un groupe de capture : html, id, html, id, …, html.
        var parts = ExampleMarker().Split(html);
        var segments = new List<DocSegment>();
        for (var i = 0; i < parts.Length; i += 2)
            segments.Add(new DocSegment(parts[i], i + 1 < parts.Length ? parts[i + 1] : null));

        var title = headings.FirstOrDefault(h => h.Level == 1)?.Text ?? route;
        return _pages[file] = new DocPage(file, route, title, segments, headings);
    }

    private string Rewrite(string file, string route, string url, bool image)
    {
        if (url.Length == 0 || SchemeUrl().IsMatch(url)) return url;

        var hash = url.IndexOf('#');
        var fragment = hash < 0 ? "" : url[hash..];
        var path = hash < 0 ? url : url[..hash];

        // Avec <base href>, une ancre seule viserait l'accueil : on remet la route de la page.
        if (path.Length == 0) return route + fragment;

        var target = Resolve(file, path);
        if (image)
        {
            return _files.ContainsKey(target) && target.EndsWith(".png", StringComparison.Ordinal)
                ? "data:image/png;base64," + Convert.ToBase64String(ReadBytes(target))
                : $"{Repository}/raw/main/{target}";
        }

        if (target.EndsWith(".md", StringComparison.Ordinal) && _files.ContainsKey(target))
            return RouteOf(target) + fragment;

        var kind = path.EndsWith('/') || !Path.HasExtension(target) ? "tree" : "blob";
        return $"{Repository}/{kind}/main/{target}{fragment}";
    }

    private static string Resolve(string file, string relative)
    {
        var parts = file.Split('/').SkipLast(1).ToList();
        foreach (var part in relative.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".") continue;
            if (part != "..") parts.Add(part);
            else if (parts.Count > 0) parts.RemoveAt(parts.Count - 1);
        }

        return string.Join('/', parts);
    }

    private static string PlainText(ContainerInline? inline)
    {
        if (inline == null) return "";
        var text = new StringBuilder();
        foreach (var child in inline.Descendants())
        {
            if (child is LiteralInline literal) text.Append(literal.Content.AsSpan());
            else if (child is CodeInline code) text.Append(code.Content);
        }

        return text.ToString();
    }

    private byte[] ReadBytes(string file)
    {
        using var stream = Assembly.GetManifestResourceStream(_files[file])!;
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private string ReadText(string file)
    {
        using var reader = new StreamReader(Assembly.GetManifestResourceStream(_files[file])!);
        return reader.ReadToEnd();
    }

    // The marker keeps its French name: it is written in the Markdown files.
    [GeneratedRegex(@"<!--\s*exemple-blazor:\s*([a-z0-9-]+)\s*-->")]
    private static partial Regex ExampleMarker();

    [GeneratedRegex("^[a-z][a-z0-9+.-]*:", RegexOptions.IgnoreCase)]
    private static partial Regex SchemeUrl();
}
