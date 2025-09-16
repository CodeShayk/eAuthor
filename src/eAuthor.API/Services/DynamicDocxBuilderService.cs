using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.MarkupCompatibility;
using DocumentFormat.OpenXml.Office2010.Word;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using eAuthor.Models;

using Lock = DocumentFormat.OpenXml.Wordprocessing.Lock;

namespace eAuthor.Services.Expressions
{
    /// <summary>
    /// Builds a DOCX with SDTs (content controls) for all template controls.
    /// For Grid / Repeater controls, attempts to emit raw w15:repeatingSection markup
    /// using OpenXmlUnknownElement so this works even when the strongly typed
    /// RepeatingSection / RepeatingSectionItem classes are NOT available in the
    /// installed OpenXML package.
    /// </summary>
    public class DynamicDocxBuilderService
    {
        private readonly StyleRenderer _styleRenderer;

        // Toggle if you want to fall back to a legacy non-repeating SDT table
        private readonly bool useRealRepeatingSections = true;

        private const string NsW = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        private const string NsW15 = "http://schemas.microsoft.com/office/word/2012/wordml";
        private const string NsMc = "http://schemas.openxmlformats.org/markup-compatibility/2006";

        public DynamicDocxBuilderService(StyleRenderer styleRenderer)
        {
            _styleRenderer = styleRenderer;
        }

        public byte[] Build(Template template)
        {
            if (template == null)
                throw new ArgumentNullException(nameof(template));

            using var ms = new MemoryStream();
            using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
            {
                var main = doc.AddMainDocumentPart();
                main.Document = new Document(new Body());

                // Add required namespaces
                main.Document.AddNamespaceDeclaration("w", NsW);
                main.Document.AddNamespaceDeclaration("w15", NsW15);
                main.Document.AddNamespaceDeclaration("mc", NsMc);
                main.Document.MCAttributes = new MarkupCompatibilityAttributes { Ignorable = "w15" };

                var body = main.Document.Body!;
                body.AppendChild(new Paragraph(new Run(new Text(template.Name ?? "Template"))));

                if (template.Controls != null)
                {
                    foreach (var ctrl in template.Controls)
                    {
                        try
                        {
                            var element = BuildControl(ctrl);
                            body.AppendChild(element);
                            body.AppendChild(SpacerParagraph());
                        }
                        catch (Exception ex)
                        {
                            body.AppendChild(new Paragraph(new Run(new Text($"[Control Render Error: {ctrl?.Label ?? ctrl?.ControlType} - {ex.Message}]"))));
                        }
                    }
                }

                main.Document.Save();
            }

            return ms.ToArray();
        }

        private OpenXmlElement BuildControl(TemplateControl ctrl)
        {
            if (ctrl == null)
                throw new ArgumentNullException(nameof(ctrl));

            return ctrl.ControlType switch
            {
                "TextBox" or "TextArea" => BuildTextControl(ctrl),
                "CheckBox" => BuildCheckBoxControl(ctrl),
                "RadioGroup" => BuildRadioGroup(ctrl),
                "Grid" or "Repeater" => BuildRepeatingControl(ctrl),
                _ => new Paragraph(new Run(new Text($"[Unsupported Control: {ctrl.ControlType}]")))
            };
        }

        #region Simple Controls

        private SdtBlock BuildTextControl(TemplateControl ctrl)
        {
            var tagVal = $"{ctrl.Id}|{ctrl.ControlType}|{ctrl.DataPath}";
            var label = ctrl.Label ?? ctrl.DataPath ?? "Field";
            var (rp, pp) = _styleRenderer.Build(ctrl.StyleJson);

            var para = StyledParagraph($"[{label}]", rp, pp);

            return new SdtBlock(
                new SdtProperties(
                    new SdtAlias { Val = label },
                    new Tag { Val = tagVal },
                    new Lock { Val = LockingValues.SdtContentLocked }
                ),
                new SdtContentBlock(para)
            );
        }

        private SdtBlock BuildCheckBoxControl(TemplateControl ctrl)
        {
            var tagVal = $"{ctrl.Id}|CheckBox|{ctrl.DataPath}";
            var label = ctrl.Label ?? ctrl.DataPath ?? "CheckBox";
            var (rp, pp) = _styleRenderer.Build(ctrl.StyleJson);

            var run = new Run(new Text($"{label}: [ ]"));
            if (rp != null)
                run.PrependChild(rp.DeepClone());
            var p = new Paragraph();
            if (pp != null)
                p.PrependChild(pp.DeepClone());
            p.Append(run);

            return new SdtBlock(
                new SdtProperties(
                    new SdtAlias { Val = label },
                    new Tag { Val = tagVal },
                    new Lock { Val = LockingValues.SdtContentLocked },
                    new CheckBox(
                        new DocumentFormat.OpenXml.Office2010.Word.Checked { Val = OnOffValues.False },
                        new CheckedState { Val = "2612", Font = "Wingdings" },
                        new UncheckedState { Val = "2610", Font = "Wingdings" }
                    )
                ),
                new SdtContentBlock(p)
            );
        }

        private SdtBlock BuildRadioGroup(TemplateControl ctrl)
        {
            var baseTag = $"{ctrl.Id}|RadioGroup|{ctrl.DataPath}";
            var label = ctrl.Label ?? ctrl.DataPath ?? "RadioGroup";
            var (rp, pp) = _styleRenderer.Build(ctrl.StyleJson);
            var options = ParseStringList(ctrl.OptionsJson);

            var container = new SdtContentBlock();
            container.Append(StyledParagraph(label, rp, pp));

            var idx = 0;
            foreach (var opt in options)
            {
                var optTag = $"{baseTag}|{idx}|{opt}";
                var para = StyledParagraph($"( ) {opt}", rp, pp);
                var optSdt = new SdtBlock(
                    new SdtProperties(
                        new SdtAlias { Val = $"{label}:{opt}" },
                        new Tag { Val = optTag }
                    ),
                    new SdtContentBlock(para)
                );
                container.Append(optSdt);
                idx++;
            }

            return new SdtBlock(
                new SdtProperties(
                    new SdtAlias { Val = label },
                    new Tag { Val = baseTag + "|GROUP" }
                ),
                container
            );
        }

        #endregion Simple Controls

        #region Repeating (Grid / Repeater)

        private OpenXmlElement BuildRepeatingControl(TemplateControl ctrl)
        {
            if (!useRealRepeatingSections)
                return BuildLegacyRepeatingTableSdt(ctrl);

            try
            {
                return BuildRepeatingSectionUnknown(ctrl);
            }
            catch
            {
                return BuildLegacyRepeatingTableSdt(ctrl);
            }
        }

        /// <summary>
        /// Builds a real repeating section using raw unknown elements for w15:repeatingSection / w15:repeatingSectionItem.
        /// </summary>
        private OpenXmlElement BuildRepeatingSectionUnknown(TemplateControl ctrl)
        {
            var tagVal = $"{ctrl.Id}|Repeat|{ctrl.DataPath}";
            var label = ctrl.Label ?? ctrl.DataPath ?? "Repeater";
            var (rp, pp) = _styleRenderer.Build(ctrl.StyleJson);

            var table = BuildRepeatingTableTemplate(ctrl, rp, pp);

            // Inner repeatingSectionItem SDT (template)
            // We wrap the table in an SDT because Word's internal model uses an item container.
            var itemSdt = new SdtBlock(
                new SdtProperties(
                    // Unknown element for w15:repeatingSectionItem
                    OpenXmlUnknownElement.CreateOpenXmlUnknownElement(
                        $"<w15:repeatingSectionItem xmlns:w15=\"{NsW15}\" />")
                ),
                new SdtContentBlock(table)
            );

            // Outer repeatingSection SDT
            var outerProps = new SdtProperties(
                new SdtAlias { Val = label },
                new Tag { Val = tagVal },
                // Unknown element for w15:repeatingSection (sectionTitle optional)
                OpenXmlUnknownElement.CreateOpenXmlUnknownElement(
                    $"<w15:repeatingSection xmlns:w15=\"{NsW15}\" />")
            );

            outerProps.MCAttributes = new MarkupCompatibilityAttributes { Ignorable = "w15" };

            var outer = new SdtBlock(
                outerProps,
                new SdtContentBlock(itemSdt)
            );

            return outer;
        }

        private OpenXmlElement BuildLegacyRepeatingTableSdt(TemplateControl ctrl)
        {
            var tagVal = $"{ctrl.Id}|Repeat|{ctrl.DataPath}";
            var label = ctrl.Label ?? ctrl.DataPath ?? "Repeater";
            var (rp, pp) = _styleRenderer.Build(ctrl.StyleJson);
            var table = BuildRepeatingTableTemplate(ctrl, rp, pp);

            return new SdtBlock(
                new SdtProperties(
                    new SdtAlias { Val = label },
                    new Tag { Val = tagVal }
                ),
                new SdtContentBlock(table)
            );
        }

        private Table BuildRepeatingTableTemplate(TemplateControl ctrl, RunProperties? rp, ParagraphProperties? pp)
        {
            var table = new Table(
                new TableProperties(
                    new TableBorders(
                        new TopBorder { Val = BorderValues.Single, Size = 4 },
                        new BottomBorder { Val = BorderValues.Single, Size = 4 },
                        new LeftBorder { Val = BorderValues.Single, Size = 4 },
                        new RightBorder { Val = BorderValues.Single, Size = 4 },
                        new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                        new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }
                    )
                )
            );

            var bindings = (ctrl.Bindings != null && ctrl.Bindings.Count > 0)
                ? ctrl.Bindings
                : new List<TemplateControlBinding>
                {
                    new TemplateControlBinding
                    {
                        Id = Guid.NewGuid(),
                        ColumnHeader = "Value",
                        DataPath = "Value"
                    }
                };

            // Header
            var headerRow = new TableRow();
            foreach (var b in bindings)
            {
                headerRow.Append(new TableCell(StyledParagraph(b.ColumnHeader ?? "Column", rp, pp)));
            }
            table.Append(headerRow);

            // Template row (tokens)
            var row = new TableRow();
            foreach (var b in bindings)
            {
                row.Append(new TableCell(StyledParagraph($"{{{{ {b.DataPath} }}}}", rp, pp)));
            }
            table.Append(row);

            return table;
        }

        #endregion Repeating (Grid / Repeater)

        #region Helpers

        private Paragraph SpacerParagraph() => new Paragraph(new Run(new Text("")));

        private Paragraph StyledParagraph(string text, RunProperties? rp, ParagraphProperties? pp)
        {
            var p = new Paragraph();
            if (pp != null)
                p.PrependChild(pp.DeepClone());
            var run = new Run(new Text(text));
            if (rp != null)
                run.PrependChild(rp.DeepClone());
            p.Append(run);
            return p;
        }

        private List<string> ParseStringList(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new();
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    return doc.RootElement
                        .EnumerateArray()
                        .Where(e => e.ValueKind == JsonValueKind.String)
                        .Select(e => e.GetString() ?? "")
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToList();
                }
            }
            catch
            {
                // ignore parse errors
            }
            return new();
        }

        #endregion Helpers
    }

    internal static class OpenXmlCloneExtensions
    {
        public static RunProperties DeepClone(this RunProperties rp) =>
            (RunProperties)rp.CloneNode(true);

        public static ParagraphProperties DeepClone(this ParagraphProperties pp) =>
            (ParagraphProperties)pp.CloneNode(true);
    }
}