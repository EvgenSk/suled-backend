using Azure.Core.Serialization;
using System.Text.Json;

namespace SuledFunctions.Tests.Helpers;

public class TestJsonSerializer : ObjectSerializer
{
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public override object? Deserialize(Stream stream, Type returnType, CancellationToken cancellationToken)
    {
        return JsonSerializer.Deserialize(stream, returnType, _options);
    }

    public override async ValueTask<object?> DeserializeAsync(Stream stream, Type returnType, CancellationToken cancellationToken)
    {
        return await JsonSerializer.DeserializeAsync(stream, returnType, _options, cancellationToken);
    }

    public override void Serialize(Stream stream, object? value, Type inputType, CancellationToken cancellationToken)
    {
        JsonSerializer.Serialize(stream, value, inputType, _options);
    }

    public override async ValueTask SerializeAsync(Stream stream, object? value, Type inputType, CancellationToken cancellationToken)
    {
        await JsonSerializer.SerializeAsync(stream, value, inputType, _options, cancellationToken);
    }
}
