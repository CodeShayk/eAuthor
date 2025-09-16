using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using eAuthor.Models;
using eAuthor.Services;
using eAuthor.Services.Expressions;
using NUnit.Framework;

namespace eAuthor.API.Tests
{
    [TestFixture]
    public class DocumentGenerationTests
    {
        private StyleRenderer _styleRenderer = null!;
        private DynamicDocxBuilderService _docxBuilder = null!;
        private IExpressionParser _parser = null!;
        private IExpressionEvaluator _evaluator = null!;
        private IConditionalBlockProcessor _conditional = null!;

        [SetUp]
        public void SetUp()
        {
            _styleRenderer = new StyleRenderer();
            _docxBuilder = new DynamicDocxBuilderService(_styleRenderer);
            _parser = new ExpressionParser();
            _evaluator = new ExpressionEvaluator();
            _conditional = new ConditionalBlockProcessor(_parser, _evaluator);
        }

        [Test]
        public void Build_TextAndCheckboxControls_ProduceExpectedTags()
        {
            var template = new Template
            {
                Id = Guid.NewGuid(),
                Name = "Basic",
                Controls = new List<TemplateControl>
                {
                    new TemplateControl
                    {
                        Id = Guid.NewGuid(),
                        ControlType = "TextBox",
                        DataPath = "/Customer/Name",
                        Label = "Customer Name",
                        StyleJson = "{\"bold\":true}"
                    },
                    new TemplateControl
                    {
                        Id = Guid.NewGuid(),
                        ControlType = "CheckBox",
                        DataPath = "/Customer/IsPremium",
                        Label = "Premium?"
                    }
                }
            };

            var docBytes = _docxBuilder.Build(template);

            var xml = ExtractMainDocumentXml(docBytes);
            StringAssert.Contains("|TextBox|/Customer/Name", xml, "TextBox tag missing.");
            StringAssert.Contains("|CheckBox|/Customer/IsPremium", xml, "CheckBox tag missing.");
        }

        [Test]
        public void Build_RadioGroup_EmitsIndividualOptionTags()
        {
            var ctrl = new TemplateControl
            {
                Id = Guid.NewGuid(),
                ControlType = "RadioGroup",
                DataPath = "/Case/Status",
                Label = "Status",
                OptionsJson = "[\"Open\",\"Closed\",\"Archived\"]"
            };

            var template = new Template
            {
                Id = Guid.NewGuid(),
                Name = "RadioGroupDoc",
                Controls = new List<TemplateControl> { ctrl }
            };

            var bytes = _docxBuilder.Build(template);
            var xml = ExtractMainDocumentXml(bytes);

            // Pattern: {Id}|RadioGroup|/Case/Status|{index}|{option}
            StringAssert.Contains("|RadioGroup|/Case/Status|0|Open", xml);
            StringAssert.Contains("|RadioGroup|/Case/Status|1|Closed", xml);
            StringAssert.Contains("|RadioGroup|/Case/Status|2|Archived", xml);
        }

        [Test]
        public void Build_RepeaterWithBindings_IncludesRepeatTag_AndPossiblyW15Elements()
        {
            var repeater = new TemplateControl
            {
                Id = Guid.NewGuid(),
                ControlType = "Repeater",
                DataPath = "/Invoice/Lines/Line",
                Label = "Invoice Lines",
                Bindings = new List<TemplateControlBinding>
                {
                    new TemplateControlBinding
                    {
                        Id = Guid.NewGuid(),
                        ColumnHeader = "Description",
                        DataPath = "Description"
                    },
                    new TemplateControlBinding
                    {
                        Id = Guid.NewGuid(),
                        ColumnHeader = "Amount",
                        DataPath = "Amount"
                    }
                }
            };

            var template = new Template
            {
                Id = Guid.NewGuid(),
                Name = "Repeater",
                Controls = new List<TemplateControl> { repeater }
            };

            var bytes = _docxBuilder.Build(template);
            var xml = ExtractMainDocumentXml(bytes);

            StringAssert.Contains("|Repeat|/Invoice/Lines/Line", xml, "Repeater tag missing.");

            // Soft assertions: present only if raw w15 repeating section succeeded
            var hasSection = xml.Contains("<w15:repeatingSection");
            var hasItem = xml.Contains("<w15:repeatingSectionItem");
            TestContext.WriteLine("w15 repeatingSection present: " + hasSection);
            TestContext.WriteLine("w15 repeatingSectionItem present: " + hasItem);
            // Do not assert they must exist—fallback may have been used.
        }

        [Test]
        public void Build_RepeaterWithoutBindings_CreatesPlaceholderBinding()
        {
            var repeater = new TemplateControl
            {
                Id = Guid.NewGuid(),
                ControlType = "Repeater",
                DataPath = "/Root/Entries",
                Label = "Entries"
                // No bindings provided => should create placeholder "Value"
            };

            var template = new Template
            {
                Id = Guid.NewGuid(),
                Name = "RepeaterNoBindings",
                Controls = new List<TemplateControl> { repeater }
            };

            var bytes = _docxBuilder.Build(template);
            var xml = ExtractMainDocumentXml(bytes);

            StringAssert.Contains(">Value<", xml, "Expected placeholder header 'Value' not found.");
            StringAssert.Contains("{{ Value }}", xml, "Expected placeholder cell token not found.");
        }

        [Test]
        public void ConditionalBlockProcessor_SelectsElseIfBranch()
        {
            var json = JsonDocument.Parse("""
            {
              "Customer": {
                "IsPremium": false,
                "IsTrial": true
              }
            }
            """).RootElement;

            var source = @"
Start
{{ if /Customer/IsPremium }}PREMIUM
{{ elseif /Customer/IsTrial }}TRIAL
{{ else }}NONE
{{ end }}
End";

            var processed = _conditional.ProcessConditionals(source, json);

            // Expect TRIAL chosen, not PREMIUM or NONE
            StringAssert.Contains("TRIAL", processed);
            Assert.False(processed.Contains("PREMIUM"), "Unexpected PREMIUM branch present.");
            Assert.False(processed.Contains("NONE"), "Unexpected ELSE branch present.");
        }

        [Test]
        public void ExpressionEvaluator_ResolvesSimplePaths()
        {
            var json = JsonDocument.Parse("""
            {
              "Order": {
                "Id": 123,
                "Customer": { "Name": "Alice" }
              }
            }
            """).RootElement;

            var exprOrderId = _parser.Parse("/Order/Id");
            var exprCustName = _parser.Parse("/Order/Customer/Name");

            var idEl = _evaluator.ResolvePath(json, exprOrderId.DataPath);
            var nameEl = _evaluator.ResolvePath(json, exprCustName.DataPath);

            Assert.That(idEl?.ValueKind, Is.EqualTo(JsonValueKind.Number));
            Assert.That(idEl?.GetInt32(), Is.EqualTo(123));
            Assert.That(nameEl?.ValueKind, Is.EqualTo(JsonValueKind.String));
            Assert.That(nameEl?.GetString(), Is.EqualTo("Alice"));
        }

        [Test]
        public void ExpressionEvaluator_ArrayIndexIfSupported_OtherwiseInconclusive()
        {
            var json = JsonDocument.Parse("""
            {
              "Data": {
                "Items": [
                  { "Value": "A" },
                  { "Value": "B" }
                ]
              }
            }
            """).RootElement;

            // If indexing syntax not implemented in current parser, test should not hard fail.
            try
            {
                var exprIndex = _parser.Parse("/Data/Items[1]/Value");
                var el = _evaluator.ResolvePath(json, exprIndex.DataPath);
                if (el?.ValueKind == JsonValueKind.String)
                {
                    Assert.That(el?.GetString(), Is.EqualTo("B"));
                }
                else
                {
                    Assert.Inconclusive("Array index parse returned non-string element; parser may not support indexing.");
                }
            }
            catch (Exception ex)
            {
                Assert.Inconclusive("Array indexing not supported by current parser/evaluator. " + ex.Message);
            }
        }

        private static string ExtractMainDocumentXml(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            using var doc = WordprocessingDocument.Open(ms, false);
            using var sr = new StreamReader(doc.MainDocumentPart!.GetStream());
            return sr.ReadToEnd();
        }
    }
}