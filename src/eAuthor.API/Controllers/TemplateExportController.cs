using eAuthor.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eAuthor.Controllers;

[ApiController]
[Route("api/templates")]
[Authorize]
public class TemplateExportController : ControllerBase
{
    private readonly TemplateService _templateService;
    private readonly DynamicDocxBuilderService _builder;

    public TemplateExportController(TemplateService templateService, DynamicDocxBuilderService builder)
    {
        _templateService = templateService;
        _builder = builder;
    }

    [HttpGet("{id:guid}/export-dynamic-docx")]
    public async Task<IActionResult> ExportDynamic(Guid id)
    {
        var template = await _templateService.GetAsync(id);
        if (template == null)
            return NotFound();
        var bytes = _builder.Build(template);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            $"{template.Name}-dynamic.docx");
    }
}