using eAuthor.Models;
using eAuthor.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eAuthor.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BaseDocxTemplatesController : ControllerBase
{
    private readonly IBaseDocxTemplateRepository _repo;

    public BaseDocxTemplatesController(IBaseDocxTemplateRepository repo)
    {
        _repo = repo;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(20_000_000)] // 20MB
    public async Task<IActionResult> Upload([FromForm] IFormFile file, [FromForm] string name)
    {
        if (file == null || file.Length == 0)
            return BadRequest("File required");
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var template = new BaseDocxTemplate
        {
            Name = name,
            FileContent = ms.ToArray(),
            CreatedUtc = DateTime.UtcNow
        };
        var id = await _repo.InsertAsync(template);
        return Ok(new { id });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var t = await _repo.GetAsync(id);
        if (t == null)
            return NotFound();
        return File(t.FileContent, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", t.Name + ".docx");
    }
}