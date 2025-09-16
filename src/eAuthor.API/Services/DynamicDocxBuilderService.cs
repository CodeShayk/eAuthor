using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Office2010.Word;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using eAuthor.Models;
using CheckBox = DocumentFormat.OpenXml.Wordprocessing.CheckBox;
using Checked = DocumentFormat.OpenXml.Wordprocessing.Checked;
using Lock = DocumentFormat.OpenXml.Wordprocessing.Lock;

namespace eAuthor.Services
{
    /// <summary>
    /// Dynamic DOCX builder that emits Content Controls (SDTs) for all template controls.
    /// Because the strongly typed Office 2013 repeating section classes (RepeatingSection / RepeatingSectionItem)
    /// are not available in your build of DocumentFormat.OpenXml v3.3.0, this implementation:
    /// 1. Tries to inject raw w15:repeatingSection / w15:repeatingSectionItem via OpenXmlUnknownElement.
    /// 2. If that fails (CreateOpenXmlUnknownElement exception), it falls back to a normal SDT with a static table (no Word UI for adding rows).
    ///
    /// If you decide you do NOT want raw repeating sections at all, set _enableRawRepeatingSections = false.
    /// </summary>
    public class DynamicDocxBuilderService
    {
        private readonly StyleRenderer _styleRenderer;

        // Toggle to disable attempting raw w15 injection.
        private const bool _enableRawRepeatingSections = true;

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

                // Declare namespaces (mc ignorable w15)
                main.Document.AddNamespaceDeclaration("w", NsW);
                main.Document.AddNamespaceDeclaration("w15", NsW15);
                main.Document.AddNamespaceDeclaration("mc", NsMc);
                main.Document.MCAttributes = new MarkupCompatibilityAttributes { Ignorable = "w15" };

                var body = main.Document.Body!;

                body.AppendChild(new Paragraph(new Run(new Text(template.Name ?? "Template"))));

                if (template.Controls != null)
                    foreach (var ctrl in template.Controls)
                        try
                        {
                            var el = BuildControl(ctrl);
                            body.AppendChild(el);
                            body.AppendChild(SpacerParagraph());
                        }
                        catch (Exception ex)
                        {
                            body.AppendChild(new Paragraph(new Run(new Text($"[Control Error {ctrl?.ControlType}: {ex.Message}]"))));
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
            var para = new Paragraph();
            if (pp != null)
                para.PrependChild(pp.DeepClone());
            para.Append(run);

            return new SdtBlock(
                new SdtProperties(
                    new SdtAlias { Val = label },
                    new Tag { Val = tagVal },
                    new Lock { Val = LockingValues.SdtContentLocked },
                    new CheckBox(
                        new Checked { Val = new OnOffValue(false) },
                        new CheckedState { Val = "2612", Font = "Wingdings" },
                        new UncheckedState { Val = "2610", Font = "Wingdings" }
                    )
                ),
                new SdtContentBlock(para)
            );
        }

        private SdtBlock BuildRadioGroup(TemplateControl ctrl)
        {
            var baseTag = $"{ctrl.Id}|RadioGroup|{ctrl.DataPath}";
            var label = ctrl.Label ?? ctrl.DataPath ?? "RadioGroup";
            var (rp, pp) = _styleRenderer.Build(ctrl.StyleJson);
            var options = ParseStringList(ctrl.OptionsJson);

            var content = new SdtContentBlock();
            content.Append(StyledParagraph(label, rp, pp));

            var i = 0;
            foreach (var opt in options)
            {
                var optTag = $"{baseTag}|{i}|{opt}";
                var optPara = StyledParagraph($"( ) {opt}", rp, pp);
                var optSdt = new SdtBlock(
                    new SdtProperties(
                        new SdtAlias { Val = $"{label}:{opt}" },
                        new Tag { Val = optTag }
                    ),
                    new SdtContentBlock(optPara)
                );
                content.Append(optSdt);
                i++;
            }

            return new SdtBlock(
                new SdtProperties(
                    new SdtAlias { Val = label },
                    new Tag { Val = baseTag + "|GROUP" }
                ),
                content
            );
        }

        #endregion Simple Controls

        #region Repeating

        private OpenXmlElement BuildRepeatingControl(TemplateControl ctrl)
        {
            if (!_enableRawRepeatingSections)
            {
            }

            try
            {
                return BuildRawRepeatingSection(ctrl);
            }
            catch
            {
                // Fallback if unknown element creation fails
                return BuildLegacyRepeating(ctrl);
            }
        }

        /// <summary>
        /// Creates a real Word repeating section using unknown elements for w15:repeatingSection and w15:repeatingSectionItem.
        /// If creation of the unknown elements fails (e.g. method throws), the caller will fallback.
        /// </summary>
        private OpenXmlElement BuildRawRepeatingSection(TemplateControl ctrl)
        {
            var tagVal = $"{ctrl.Id}|Repeat|{ctrl.DataPath}";
            var label = ctrl.Label ?? ctrl.DataPath ?? "Repeater";
            var (rp, pp) = _styleRenderer.Build(ctrl.StyleJson);

            var table = BuildRepeatingTableTemplate(ctrl, rp, pp);

            // IMPORTANT: Do NOT redeclare xmlns on these unknown elements. Just use prefix; root doc has w15 declared.
            // Using a minimal self-closing tag. Including the namespace again inside here can cause 'duplicate prefix' issues.
            var repeatingSectionUnknown = new OpenXmlUnknownElement("<w15:repeatingSection/>");
            var repeatingSectionItemUnknown = new OpenXmlUnknownElement("<w15:repeatingSectionItem/>");

            // Inner item SDT
            var itemSdt = new SdtBlock(
                new SdtProperties(
                    // w15:repeatingSectionItem property
                    repeatingSectionItemUnknown
                ),
                new SdtContentBlock(table)
            );

            // Outer repeating section SDT
            var outerProps = new SdtProperties(
                new SdtAlias { Val = label },
                new Tag { Val = tagVal },
                repeatingSectionUnknown
            );
            outerProps.MCAttributes = new MarkupCompatibilityAttributes { Ignorable = "w15" };

            return new SdtBlock(
                outerProps,
                new SdtContentBlock(itemSdt)
            );
        }

        /// <summary>
        /// Fallback: simple SDT with static table (no UI add-row support).
        /// </summary>
        private OpenXmlElement BuildLegacyRepeating(TemplateControl ctrl)
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

            var bindings = ctrl.Bindings != null && ctrl.Bindings.Count > 0
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

            // Header row
            var headerRow = new TableRow();
            foreach (var b in bindings)
                headerRow.Append(new TableCell(StyledParagraph(b.ColumnHeader ?? "Column", rp, pp)));
            table.Append(headerRow);

            // Template row with placeholders
            var dataRow = new TableRow();
            foreach (var b in bindings)
            {
                var placeholder = $"{{{{ {b.DataPath} }}}}";
                dataRow.Append(new TableCell(StyledParagraph(placeholder, rp, pp)));
            }
            table.Append(dataRow);

            return table;
        }

        #endregion Repeating

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
                    return doc.RootElement
                        .EnumerateArray()
                        .Where(e => e.ValueKind == JsonValueKind.String)
                        .Select(e => e.GetString() ?? "")
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToList();
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