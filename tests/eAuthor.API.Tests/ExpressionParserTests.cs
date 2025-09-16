using eAuthor.Services.Expressions;
using NUnit.Framework;

namespace eAuthor.API.Tests;

public class ExpressionParserTests
{
    private IExpressionParser _parser = null!;

    [SetUp]
    public void Setup()
    {
        _parser = new ExpressionParser();
    }

    [Test]
    public void ParsesPathOnly()
    {
        var p = _parser.Parse("/Customer/Name");
        Assert.That(p.DataPath, Is.EqualTo("/Customer/Name"));
        Assert.That(p.Filters, Is.Empty);
    }

    [Test]
    public void ParsesFilters()
    {
        var p = _parser.Parse("/Order/Date | date:yyyy-MM-dd | upper");
        Assert.That(p.DataPath, Is.EqualTo("/Order/Date"));
        Assert.That(p.Filters.Count, Is.EqualTo(2));
        Assert.That(p.Filters[0].Name, Is.EqualTo("date"));
        Assert.That(p.Filters[0].Args[0], Is.EqualTo("yyyy-MM-dd"));
    }
}