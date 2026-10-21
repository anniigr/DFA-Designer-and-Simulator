using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutomatonEditor.Core;

/// <summary>JSON document format of the editor (file → canvas). Property names are the on-disk contract.</summary>
public sealed class DfaSaveData
{
    public StateSaveData[]? States { get; set; }

    public TransitionSaveData[]? Transitions { get; set; }
}

public sealed class StateSaveData
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public double X { get; set; }

    public double Y { get; set; }

    public bool IsInitial { get; set; }

    public bool IsAccepting { get; set; }
}

public sealed class TransitionSaveData
{
    public string? FromId { get; set; }

    public string? ToId { get; set; }

    /// <summary>One or more single-character symbols separated by commas, e.g. "0,1".</summary>
    public string? Symbol { get; set; }
}

public static class DfaJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static string Serialize(DfaSaveData data) => JsonSerializer.Serialize(data, Options);

    /// <summary>Parses the document. Returns null (never throws) when the text is not valid JSON of the expected shape.</summary>
    public static DfaSaveData? TryDeserialize(string json, out string? error)
    {
        error = null;
        try
        {
            var data = JsonSerializer.Deserialize<DfaSaveData>(json, Options);
            if (data is null)
            {
                error = "The file is empty.";
            }

            return data;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return null;
        }
    }
}
