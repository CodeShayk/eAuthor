using eAuthor.Repositories;
using eAuthor.Services;
using Microsoft.AspNetCore.Mvc;

namespace eAuthor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class XsdController : ControllerBase {
    private readonly XsdService _xsdService;
    private readonly IXsdRepository _repo;
    public XsdController(XsdService svc, IXsdRepository repo) {
        _xsdService = svc; _repo = repo;
    }

    public record UploadXsdRequest(string Name, string Xsd);

    [HttpPost]
    public async Task<IActionResult> Upload([FromBody] UploadXsdRequest req) {
        var id = await _repo.InsertAsync(req.Name, req.Xsd);
        return Ok(new { id });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id) {
        var xsd = await _repo.GetAsync(id);
        if (xsd == null) return NotFound();
        var root = _xsdService.ParseXsd(xsd.RawXsd);
        return Ok(new { xsd.Id, xsd.Name, Root = root });
    }
}