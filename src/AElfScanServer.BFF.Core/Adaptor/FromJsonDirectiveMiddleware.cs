using System.Text.Json;
using System.Threading.Tasks;
using HotChocolate.Resolvers;

namespace AElfScanServer.Common.BFF.Core.Adaptor;

public class FromJsonDirectiveMiddleware
{
    private readonly FieldDelegate _next;

    public FromJsonDirectiveMiddleware(FieldDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(IMiddlewareContext context, FromJsonDirective directive)
    {
        var jsonElement = context.Parent<JsonElement>();
        if (jsonElement is var _)
        {
            var name = directive.Name;
            if (string.IsNullOrEmpty(name))
            {
                name = context.Selection.Field.Name;
            }

            var value = jsonElement.GetProperty(name);
            switch (value.ValueKind)
            {
                case JsonValueKind.Array:
                    context.Result = JsonSerializer.Deserialize<object[]>(value.GetRawText());
                    break;
                case JsonValueKind.String:
                    context.Result = value.GetString();
                    break;
                case JsonValueKind.Number:
                    if (value.TryGetInt32(out var intNum))
                    {
                        context.Result = intNum;
                    }
                    else if (value.TryGetInt64(out var longNum))
                    {
                        context.Result = longNum;
                    }
                    else
                    {
                        context.Result = value.GetDouble();
                    }
                    // if (decimal.TryParse(value.GetRawText(), out var decimalNum))
                    // {
                    //     context.Result = decimalNum;
                    // }
                    break;
                case JsonValueKind.True:
                case JsonValueKind.False:
                    context.Result = value.GetBoolean();
                    break;
                case JsonValueKind.Object:
                    context.Result = value;
                    break;
                case JsonValueKind.Undefined:
                case JsonValueKind.Null:
                default:
                    break;
            }
        }

        await _next(context);
    }
}