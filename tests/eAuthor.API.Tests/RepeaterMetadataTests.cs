using NUnit.Framework;
using System.Text.Json;
using eAuthor.Services;
using eAuthor.Services.Expressions;

namespace eAuthor.API.Tests;

public class RepeaterMetadataTests
{
    private RepeaterBlockProcessor _rep = null!;
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
        _rep = new RepeaterBlockProcessor(_parser, _eval, _cond);
        var json = """
        {
          "Items": {
             "Item": [
               { "Name": "Alpha" },
               { "Name": "Beta" },
               { "Name": "Gamma" }
             ]
          }
        }
        """;
        _data = JsonDocument.Parse(json).RootElement;
    }

    [Test]
    public void InsertsIndexAndFlags()
    {
        var input = "{{ repeat /Items/Item }}({{ index }}:{{ first }}:{{ last }}:{{ odd }}) {{ Name }} {{ endrepeat }}";
        var output = _rep.ProcessRepeaters(input, _data).Trim();
        // (1:true:false:false) Alpha (2:false:false:true) Beta (3:false:true:false) Gamma
        Assert.That(output.Contains("(1:true:false:false) Alpha"));
        Assert.That(output.Contains("(2:false:false:true) Beta"));
        Assert.That(output.Contains("(3:false:true:false) Gamma"));
    }
}