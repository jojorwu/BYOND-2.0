using System.Text.Json;
using System.Text.Json.Serialization;
using Shared;

namespace Shared.Serialization;

public class GameObjectConverter : JsonConverter<GameObject>
{
    public override GameObject Read(ref Utf8JsonReader reader, System.Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        var obj = new GameObject();
        if (root.TryGetProperty("Id", out var idProp)) obj.Id = idProp.GetInt64();

        // Note: Full reconstruction requires access to IObjectTypeManager and GameState
        // This is primarily for DTO-style transfers for now.

        if (root.TryGetProperty("Properties", out var props))
        {
            foreach (var prop in props.EnumerateObject())
            {
                // We should ideally deserialize into DreamValue here
            }
        }

        return obj;
    }

    public override void Write(Utf8JsonWriter writer, GameObject value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("Id", value.Id);
        writer.WriteString("TypeName", value.TypeName);

        writer.WriteStartObject("Transform");
        writer.WriteNumber("X", value.X);
        writer.WriteNumber("Y", value.Y);
        writer.WriteNumber("Z", value.Z);
        writer.WriteNumber("Dir", value.Dir);
        writer.WriteEndObject();

        writer.WriteStartObject("Visuals");
        writer.WriteString("Icon", value.Icon);
        writer.WriteString("IconState", value.IconState);
        writer.WriteString("Color", value.Color);
        writer.WriteNumber("Alpha", value.Alpha);
        writer.WriteNumber("Layer", value.Layer);
        writer.WriteEndObject();

        writer.WriteStartObject("Properties");
        if (value.ObjectType != null)
        {
            for (int i = 0; i < value.ObjectType.VariableNames.Count; i++)
            {
                var name = value.ObjectType.VariableNames[i];
                // Skip built-ins already serialized above
                if (name == "x" || name == "y" || name == "z" || name == "dir" ||
                    name == "icon" || name == "icon_state" || name == "color" ||
                    name == "alpha" || name == "layer") continue;

                var val = value.GetVariable(i);
                writer.WritePropertyName(name);
                val.WriteTo(writer, options);
            }
        }
        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}
