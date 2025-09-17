using System.Text.Json;

namespace eAuthor.Services.Expressions;

public interface IExpressionEvaluator
{
    string Evaluate(ParsedExpression expression, JsonElement root, JsonElement? relativeContext = null);

    JsonElement? ResolvePath(JsonElement root, string path, JsonElement? relativeContext = null);

    bool EvaluateBoolean(JsonElement root, ParsedExpression expression, JsonElement? relativeContext = null);
}