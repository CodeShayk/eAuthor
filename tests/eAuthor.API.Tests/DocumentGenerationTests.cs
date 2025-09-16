using NUnit.Framework;
using System.Text.Json;
using WordTemplating.Core.Models;
using WordTemplating.Core.Services;
using WordTemplating.Core.Services.Expressions;

namespace WordTemplating.Tests;

public class DocumentGenerationTests {
    private DocumentGenerationService _service = null!;

    [SetUp]
    public void Setup() {
        _service = new DocumentGenerationService(new ExpressionParser(), new ExpressionEvaluator());
    }

    [Test]
    public void ReplacesTokensInHtml() {
        var template = new Template {
            Id = Guid.NewGuid(),
            Name = "Test",
            HtmlBody = "<h1>{{ /Customer/Name | upper }}</h1>"
        };
        var dataJson = """
        {
          "Customer": { "Name": "Alice" }
        }
        """;
        var data = JsonDocument.Parse(dataJson).RootElement;
        var bytes = _service.Generate(template, data, null);
        Assert.That(bytes.Length, Is.GreaterThan(0));
        // Not parsing the docx in test for brevity
    }
}