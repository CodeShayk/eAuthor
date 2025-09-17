using eAuthor.Services.Expressions;
using NUnit.Framework;
using System.Text.Json;

namespace eAuthor.API.Tests.Services;

public class ArrayIndexExpressionTests
{
    private IExpressionParser _parser = null!;
    private IExpressionEvaluator _eval = null!;
    private JsonElement _data;

    [SetUp]
    public void Setup()
    {
        _parser = new ExpressionParser();
        _eval = new ExpressionEvaluator();
        var json = """
        {
          "Orders": {
            "Order": [
              { "OrderNumber": "A100" },
              { "OrderNumber": "B200" }
            ]
          }
        }
        """;
        _data = JsonDocument.Parse(json).RootElement;
    }

    [Test]
    public void ResolvesFirstOrder()
    {
        var expr = _parser.Parse("/Orders/Order[0]/OrderNumber");
        var val = _eval.Evaluate(expr, _data);
        Assert.That(val, Is.EqualTo("A100"));
    }

    [Test]
    public void ResolvesSecondOrder()
    {
        var expr = _parser.Parse("/Orders/Order[1]/OrderNumber");
        var val = _eval.Evaluate(expr, _data);
        Assert.That(val, Is.EqualTo("B200"));
    }

    [Test]
    public void OutOfRangeReturnsEmpty()
    {
        var expr = _parser.Parse("/Orders/Order[5]/OrderNumber");
        var val = _eval.Evaluate(expr, _data);
        Assert.That(val, Is.EqualTo(""));
    }
}