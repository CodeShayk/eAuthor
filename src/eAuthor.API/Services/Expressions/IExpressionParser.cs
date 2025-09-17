namespace eAuthor.Services.Expressions;

public interface IExpressionParser {
    ParsedExpression Parse(string raw);
    IEnumerable<string> ExtractRawExpressions(string content);
}
