using NUnit.Framework;
using System.Text.Json;
using eAuthor.Services;
using eAuthor.Services.Expressions;

namespace eAuthor.API.Tests;

public class RepeaterBlockTests
{
    private RepeaterBlockProcessor _repeater = null!;
    private ExpressionParser _parser = null!;
    private ExpressionEvaluator _eval = null!;
    private ConditionalBlockProcessor _cond = null!;
    private JsonElement _data;

    [SetUp]
    public void Setup()
    {
        _parser = new ExpressionParser();
        _eval = new ExpressionEvaluator();
        _cond = new ConditionalBlockProcessor(_parser, _eval);
        _repeater = new RepeaterBlockProcessor(_parser, _eval, _cond);
        var json = """
        {
          "Orders": {
            "Order": [
              { "OrderNumber": "A100", "Total": 10.5, "Vip": true },
              { "OrderNumber": "B200", "Total": 20.0, "Vip": false }
            ]
          }
        }
        """;
        _data = JsonDocument.Parse(json).RootElement;
    }

    [Test]
    public void ExpandsBasicRepeater()
    {
        var input = "{{ repeat /Orders/Order }}#{{ OrderNumber }};{{ endrepeat }}";
        var output = _repeater.ProcessRepeaters(input, _data);
        Assert.That(output, Is.EqualTo("#A100;#B200;"));
    }

    [Test]
    public void HandlesRelativeFilters()
    {
        var input = "{{ repeat /Orders/Order }}{{ Total | number:#0.00 }} {{ endrepeat }}";
        var output = _repeater.ProcessRepeaters(input, _data).Trim();
        Assert.That(output, Is.EqualTo("10.50 20.00"));
    }

    [Test]
    public void NestedConditionalsInsideRepeater()
    {
        var input = "{{ repeat /Orders/Order }}{{ if Vip }}VIP:{{ end }}{{ OrderNumber }} {{ endrepeat }}";
        var output = _repeater.ProcessRepeaters(input, _data).Trim();
        Assert.That(output, Is.EqualTo("A100 B200"));
    }
}