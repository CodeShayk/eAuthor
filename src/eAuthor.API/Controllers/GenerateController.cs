using eAuthor.Models;
using eAuthor.Repositories;
using eAuthor.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace eAuthor.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GenerateController : ControllerBase
{
    private readonly TemplateService _templateService;
    private readonly DocumentGenerationService _docService;
    private readonly IBaseDocxTemplateRepository _baseRepo;

    public GenerateController(TemplateService t, DocumentGenerationService d, IBaseDocxTemplateRepository baseRepo)
    {
        _templateService = t;
        _docService = d;
        _baseRepo = baseRepo;
    }

    public record GenerateRequest(Guid TemplateId, JsonElement Data);

    [HttpPost]
    public async Task<IActionResult> Generate([FromBody] GenerateRequest req)
    {
        var template = await _templateService.GetAsync(req.TemplateId);
        if (template == null)
            return NotFound("Template not found");
        BaseDocxTemplate? baseDoc = null;
        if (template.GetType().GetProperty("BaseDocxTemplateId")?.GetValue(template) is Guid baseId && baseId != Guid.Empty)
            baseDoc = await _baseRepo.GetAsync(baseId);
        var bytes = _docService.Generate(template, req.Data, baseDoc);
        return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"{template.Name}.docx");
    }
}