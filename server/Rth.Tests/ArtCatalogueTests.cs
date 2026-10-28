using System.Text.Json;
using System.Text.RegularExpressions;

namespace Rth.Tests;

/// <summary>
/// The art's layout and its catalogue (docs/assets.md section 10, tools/art/catalogue.json): every
/// model the families export has an entry, and every model the world or the content names exists.
/// </summary>
public class ArtCatalogueTests
{
    private static string Root => Path.GetDirectoryName(Repo.ServerDirectory)!;
    private static string ArtFolder => Path.Combine(Root, "client", "Assets", "Resources", "Art");
    private static readonly string[] Layers = { "Parts", "Assets" };

    private static List<JsonElement> Entries()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "tools", "art", "catalogue.json")));
        return doc.RootElement.GetProperty("assets").EnumerateArray().Select(e => e.Clone()).ToList();
    }

    /// <summary>"Art/Assets/Trees/Common/Alder" for the entry "assets/trees/common/Alder".</summary>
    private static string ResourcePath(string id)
    {
        string[] p = id.Split('/');
        return "Art/" + string.Join("/", p.Take(3).Select(s => char.ToUpperInvariant(s[0]) + s.Substring(1))) + "/" + p[3];
    }

    [Fact]
    public void EveryEntryHasAKnownStatusAndAFamilyPath()
    {
        var statuses = new[] { "missing", "blockout", "first pass", "in review", "approved" };
        var ids = new HashSet<string>();
        foreach (JsonElement e in Entries())
        {
            string id = e.GetProperty("id").GetString()!;
            Assert.True(ids.Add(id), $"{id} is in the catalogue twice");
            Assert.Matches("^(parts|assets)/[a-z]+/[a-z]+/[A-Z][A-Za-z0-9]*$", id);
            Assert.Contains(e.GetProperty("status").GetString(), statuses);
        }
    }

    [Fact]
    public void EveryExportedModelIsInTheCatalogue()
    {
        var ids = Entries().Select(e => ResourcePath(e.GetProperty("id").GetString()!)).ToHashSet();
        var missing = new List<string>();
        foreach (string layer in Layers)
            foreach (string fbx in Directory.EnumerateFiles(Path.Combine(ArtFolder, layer), "*.fbx", SearchOption.AllDirectories))
            {
                string rel = "Art/" + Path.GetRelativePath(ArtFolder, fbx).Replace('\\', '/');
                rel = rel.Substring(0, rel.Length - ".fbx".Length);
                if (!ids.Contains(rel)) missing.Add(rel);
            }
        Assert.Empty(missing);
    }

    [Fact]
    public void ACatalogueEntryThatIsMissingHasNoModel()
    {
        var made = Entries().Where(e => e.GetProperty("status").GetString() == "missing")
            .Select(e => ResourcePath(e.GetProperty("id").GetString()!))
            .Where(r => File.Exists(Path.Combine(Root, "client", "Assets", "Resources", r + ".fbx")));
        Assert.Empty(made);
    }

    [Fact]
    public void EveryModelTheWorldAndTheContentNameExists()
    {
        var named = new List<(string File, string Path)>();
        var assetPath = new Regex("\"(Assets/Resources/Art/[^\"]+\\.fbx)\"");
        var resourcePath = new Regex("\"(Art/(?:Parts|Assets)/[^\"]+)\"");
        foreach (string f in Directory.EnumerateFiles(Path.Combine(Root, "world", "regions"), "*.json")
                     .Concat(Directory.EnumerateFiles(Path.Combine(Root, "world", "kits"), "*.json")))
            named.AddRange(assetPath.Matches(File.ReadAllText(f)).Select(m => (f, m.Groups[1].Value)));
        foreach (string f in Directory.EnumerateFiles(Path.Combine(Repo.ServerDirectory, "data", "content"), "*.json"))
            named.AddRange(resourcePath.Matches(File.ReadAllText(f)).Select(m => (f, "Assets/Resources/" + m.Groups[1].Value + ".fbx")));
        Assert.NotEmpty(named);
        var gone = named.Where(n => !File.Exists(Path.Combine(Root, "client", n.Path)))
            .Select(n => $"{Path.GetFileName(n.File)}: {n.Path}").Distinct().ToList();
        Assert.Empty(gone);
    }
}
