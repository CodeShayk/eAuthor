using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using eAuthor.Repositories;
using eAuthor.Services;
using eAuthor.Models;

namespace eAuthor.Controllers;

[ApiController]
[Route("api/templates")]
[Authorize]
public class TemplateConversionController : ControllerBase
{
    private readonly ITemplateService _templateService;
    private readonly IHtmlToDynamicConverter _converter;
    private readonly IDynamicDocxBuilderService _builder;
    private readonly IBaseDocxTemplateRepository _baseRepo;
    private readonly ITemplateRepository _templateRepo;

    public TemplateConversionController(
        ITemplateService templateService,
        IHtmlToDynamicConverter converter,
        IDynamicDocxBuilderService builder,
        IBaseDocxTemplateRepository baseRepo,
        ITemplateRepository templateRepo)
    {
        _templateService = templateService;
        _converter = converter;
        _builder = builder;
        _baseRepo = baseRepo;
        _templateRepo = templateRepo;
    }

    public record ConvertRequest(bool attachAsBase);

    [HttpPost("{id:guid}/convert-html-to-dynamic")]
    public async Task<IActionResult> Convert(Guid id, [FromBody] ConvertRequest req)
    {
        var template = await _templateService.GetAsync(id);
        if (template == null)
            return NotFound();

        // Convert tokens to controls (additive – keep existing)
        var newControls = _converter.Convert(template.HtmlBody);
        // Merge (avoid duplicates by DataPath)
        var existingPaths = new HashSet<string>(template.Controls.Select(c => c.DataPath), StringComparer.OrdinalIgnoreCase);
        foreach (var c in newControls)
            if (!existingPaths.Contains(c.DataPath))
            {
                c.TemplateId = template.Id;
                template.Controls.Add(c);
            }
        await _templateRepo.UpsertAsync(template);

        if (req.attachAsBase)
        {
            var bytes = _builder.Build(template);
            var baseDoc = new BaseDocxTemplate
            {
                Id = Guid.NewGuid(),
                Name = $"{template.Name}-dynamic",
                FileContent = bytes,
                CreatedUtc = DateTime.UtcNow
            };
            await _baseRepo.InsertAsync(baseDoc);
            // Attach by setting property via reflection (if model extended directly, set normally)
            var prop = template.GetType().GetProperty("BaseDocxTemplateId");
            if (prop != null)
            {
                prop.SetValue(template, baseDoc.Id);
                await _templateRepo.UpsertAsync(template);
            }
            return Ok(new { addedControls = newControls.Count, baseDocId = baseDoc.Id });
        }

        return Ok(new { addedControls = newControls.Count });
    }
}