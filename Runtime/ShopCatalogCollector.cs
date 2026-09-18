using System.Text;

namespace ArenaPilot;

public sealed class ShopCatalogCollector
{
    private sealed record CatalogItem(string Category, uint Id, string Name, bool NameResolved);

    private readonly string filePath;
    private readonly Dictionary<uint, CatalogItem> items = [];
    private bool dirty;

    public string FilePath => filePath;
    public int Count => items.Count;

    public ShopCatalogCollector(string filePath)
    {
        this.filePath = filePath;
        Load();
        MergeBuiltInItems();
    }

    public void Capture(IReadOnlyList<ShopCatalogEntry> entries)
    {
        MergeBuiltInItems();
        foreach (var entry in entries)
            Merge(entry);
        SaveIfNeeded();
    }

    private void MergeBuiltInItems()
    {
        foreach (var item in CrucibleItemCatalog.AllItems)
        {
            var category = CrucibleItemCatalog.GetCategory(item.Id);
            if (items.TryGetValue(item.Id, out var existing)
                && existing.Category == category
                && existing.Name == item.Name)
                continue;
            items[item.Id] = new CatalogItem(category, item.Id, item.Name, true);
            dirty = true;
        }
    }

    private void Merge(ShopCatalogEntry entry)
    {
        if (CrucibleItemCatalog.AllItems.Any(x => x.Id == entry.Id))
            return;

        var entryResolved = IsResolvedName(entry.Name);
        if (!items.TryGetValue(entry.Id, out var existing))
        {
            items[entry.Id] = new CatalogItem(entry.Category, entry.Id, entry.Name, entryResolved);
            dirty = true;
            return;
        }

        if ((!IsResolvedName(existing.Name) && entryResolved)
            || (!string.Equals(existing.Category, entry.Category, StringComparison.Ordinal)
                && existing.Category == "未分类"))
        {
            items[entry.Id] = new CatalogItem(entry.Category, entry.Id, entry.Name, entryResolved);
            dirty = true;
        }
    }

    private static bool IsResolvedName(string name)
        => !string.IsNullOrWhiteSpace(name)
            && !name.StartsWith("未知", StringComparison.Ordinal)
            && !name.StartsWith("奇弈道具 ", StringComparison.Ordinal);

    private void Load()
    {
        if (!File.Exists(filePath))
            return;

        try
        {
            foreach (var line in File.ReadLines(filePath, Encoding.UTF8).Skip(1))
            {
                var fields = ParseCsvLine(line);
                if (fields.Count < 3 || !uint.TryParse(fields[1], out var id) || id == 0)
                    continue;
                var name = fields[2].Trim();
                var resolved = IsResolvedName(name);
                items[id] = new CatalogItem(fields[0].Trim(), id, name, resolved);
            }
        }
        catch (Exception ex)
        {
            DalamudApi.Log.Warning(ex, "Unable to load shop catalog from {Path}.", filePath);
        }
    }

    private void SaveIfNeeded()
    {
        if (!dirty)
            return;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            var lines = new List<string> { "类别,ID,名称" };
            lines.AddRange(items.Values
                .OrderBy(x => CategoryOrder(x.Category))
                .ThenBy(x => x.Id)
                .Select(x => $"{Escape(x.Category)},{x.Id},{Escape(x.Name)}"));
            File.WriteAllLines(filePath, lines, new UTF8Encoding(true));
            dirty = false;
        }
        catch (Exception ex)
        {
            DalamudApi.Log.Warning(ex, "Unable to save shop catalog to {Path}.", filePath);
        }
    }

    private static int CategoryOrder(string category)
        => category switch
        {
            "奇弈道具" => 0,
            "斗兽装备" => 1,
            "食料" => 2,
            _ => 3,
        };

    private static string Escape(string value)
        => value.IndexOfAny([',', '"', '\r', '\n']) < 0
            ? value
            : $"\"{value.Replace("\"", "\"\"")}\"";

    private static IReadOnlyList<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (quoted && index + 1 < line.Length && line[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == ',' && !quoted)
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else
            {
                field.Append(character);
            }
        }
        fields.Add(field.ToString());
        return fields;
    }
}
