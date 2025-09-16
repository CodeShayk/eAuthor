using eAuthor.Services.Expressions;
using NUnit.Framework;
using System.Text.Json;

namespace eAuthor.API.Tests;

public class ExpressionEvaluatorTests
{
    private ExpressionParser _parser = null!;
    private ExpressionEvaluator _evaluator = null!;
    private JsonElement _data;

    [SetUp]
    public void Setup()
    {
        _parser = new ExpressionParser();
        _evaluator = new ExpressionEvaluator();
        var json = """
        {
          "Customer": {
            "Name": "Alice",
            "IsPremium": true
          },
          "Order": {
            "Date":"2025-09-16",
            "Total": 1234.5
          }
        }
        """;
        _data = JsonDocument.Parse(json).RootElement;
    }

    [Test]
    public void EvaluatesUpperFilter()
    {
        var expr = _parser.Parse("/Customer/Name | upper");
        var result = _evaluator.Evaluate(expr, _data);
        Assert.That(result, Is.EqualTo("ALICE"));
    }

    [Test]
    public void EvaluatesDateFormat()
    {
        var expr = _parser.Parse("/Order/Date | date:yyyy/MM/dd");
        var result = _evaluator.Evaluate(expr, _data);
        Assert.That(result, Is.EqualTo("2025/09/16"));
    }

    [Test]
    public void EvaluatesNumberFormat()
    {
        var expr = _parser.Parse("/Order/Total | number:#,##0.00");
        var result = _evaluator.Evaluate(expr, _data);
        Assert.That(result, Is.EqualTo("1,234.50"));
    }

    [Test]
    public void EvaluatesBoolFilter()
    {
        var expr = _parser.Parse("/Customer/IsPremium | bool:Yes:No");
        var result = _evaluator.Evaluate(expr, _data);
        Assert.That(result, Is.EqualTo("Yes"));
    }
}