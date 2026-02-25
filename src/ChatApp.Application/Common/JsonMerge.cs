using System.Text.Json;
using System.Text.Json.Nodes;

namespace ChatApp.Api.Common;

public static class JsonMerge
{
    // Deep-merge objects; arrays & primitives are replaced.
    public static string Merge(string originalJson, JsonElement patch)
    {
        var originalNode = JsonNode.Parse(string.IsNullOrWhiteSpace(originalJson) ? "{}" : originalJson) as JsonObject
                           ?? new JsonObject();

        var patchNode = JsonNode.Parse(patch.GetRawText());
        if (patchNode is null) return originalNode.ToJsonString();

        if (patchNode is not JsonObject patchObj)
            return patchNode.ToJsonString(); // replace whole document if patch isn't an object

        DeepMerge(originalNode, patchObj);
        return originalNode.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static void DeepMerge(JsonObject target, JsonObject patch)
    {
        foreach (var (key, value) in patch)
        {
            if (value is null)
            {
                target[key] = null; // explicit null
                continue;
            }

            if (value is JsonObject patchChild && target[key] is JsonObject targetChild)
            {
                DeepMerge(targetChild, patchChild);
            }
            else
            {
                target[key] = value.DeepClone(); // replace
            }
        }
    }
}