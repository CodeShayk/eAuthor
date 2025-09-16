using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using eAuthor.Services;
using Ganss.Xss;
using eAuthor.Models;

namespace eAuthor.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TemplatesController : ControllerBase
{
    private readonly TemplateService _svc;
    private readonly HtmlSanitizer _sanitizer;

    public TemplatesController(TemplateService svc, HtmlSanitizer sanitizer)
    {
        _svc = svc;
        _sanitizer = sanitizer;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _svc.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var t = await _svc.GetAsync(id);
        return t == null ? NotFound() : Ok(t);
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] Template template)
    {
        if (template.HtmlBody.Length > 500_000)
            return BadRequest("Template too large");
        template.HtmlBody = _sanitizer.Sanitize(template.HtmlBody);
        var id = await _svc.SaveAsync(template);
        return Ok(new { id });
    }
}