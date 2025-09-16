using NUnit.Framework;
using System.Text.Json;
using eAuthor.Services;
using eAuthor.Services.Expressions;

namespace eAuthor.API.Tests;

public class ConditionalElseIfTests
{
    private ConditionalBlockProcessor _proc = null!;
    private IExpressionParser _parser = null!;
    private IExpressionEvaluator _eval = null!;
    private JsonElement _data;

    [SetUp]
    public void Setup()
    {
        _parser = new ExpressionParser();
        _eval = new ExpressionEvaluator();
        _proc = new ConditionalBlockProcessor(_parser, _eval);
        var json = """
        {
          "A": false,
          "B": true,
          "C": false
        }
        """;
        _data = JsonDocument.Parse(json).RootElement;
    }

    [Test]
    public void PicksElseIf()
    {
        var input = "{{ if /A }}X{{ elseif /B }}Y{{ elseif /C }}Z{{ else }}W{{ end }}";
        var output = _proc.ProcessConditionals(input, _data);
        Assert.That(output, Is.EqualTo("Y"));
    }

    [Test]
    public void FallsThroughToElse()
    {
        var input = "{{ if /A }}X{{ elseif /C }}Y{{ else }}W{{ end }}";
        var output = _proc.ProcessConditionals(input, _data);
        Assert.That(output, Is.EqualTo("W"));
    }
}