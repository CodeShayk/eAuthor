using System.Text.Json;
using eAuthor.Services.Impl;
using eAuthor.Services.Expressions;
using NUnit.Framework;

namespace eAuthor.API.Tests;

public class ConditionalProcessorTests
{
    private ConditionalBlockProcessor _proc = null!;
    private ExpressionParser _parser = null!;
    private ExpressionEvaluator _eval = null!;
    private JsonElement _data;

    [SetUp]
    public void Setup()
    {
        _parser = new ExpressionParser();
        _eval = new ExpressionEvaluator();
        _proc = new ConditionalBlockProcessor(_parser, _eval);
        var json = """
        {
          "Customer": { "IsPremium": true, "Name": "Alice" },
          "EmptyArray": []
        }
        """;
        _data = JsonDocument.Parse(json).RootElement;
    }

    [Test]
    public void RendersIfTrue()
    {
        var input = "Hello {{ if /Customer/IsPremium }}VIP{{ end }}!";
        var outp = _proc.ProcessConditionals(input, _data);
        Assert.That(outp, Is.EqualTo("Hello VIP!"));
    }

    [Test]
    public void SkipsIfFalse()
    {
        var input = "Hello {{ if /Customer/NotExists }}VIP{{ end }}!";
        var outp = _proc.ProcessConditionals(input, _data);
        Assert.That(outp, Is.EqualTo("Hello !"));
    }

    [Test]
    public void HandlesElse()
    {
        var input = "{{ if /Customer/IsPremium }}A{{ else }}B{{ end }}";
        var outp = _proc.ProcessConditionals(input, _data);
        Assert.That(outp, Is.EqualTo("A"));
    }

    [Test]
    public void HandlesElseFalseBranch()
    {
        var input = "{{ if /Customer/Unknown }}A{{ else }}B{{ end }}";
        var outp = _proc.ProcessConditionals(input, _data);
        Assert.That(outp, Is.EqualTo("B"));
    }

    [Test]
    public void NestedBlocks()
    {
        var input = "{{ if /Customer/IsPremium }}X {{ if /Customer/Name }}Y{{ end }} Z{{ end }}";
        var outp = _proc.ProcessConditionals(input, _data);
        Assert.That(outp.Replace(" ", ""), Is.EqualTo("XYZ"));
    }
}