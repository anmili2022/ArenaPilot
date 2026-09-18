using System.Text.Json;

namespace ArenaPilot;

public sealed class SnapshotExporter
{
    private readonly string directory;

    public SnapshotExporter(string directory)
        => this.directory = directory;

    public string Export(ArenaSnapshot snapshot)
    {
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, $"snapshot-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.json");
        File.WriteAllText(filePath, JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
        return filePath;
    }
}
