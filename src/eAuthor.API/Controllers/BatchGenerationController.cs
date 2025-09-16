using eAuthor.Models;
using eAuthor.Repositories;
using eAuthor.Services.Background;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace eAuthor.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BatchGenerationController : ControllerBase {
    public record EnqueueBatchRequest(Guid TemplateId, JsonElement DataArray, string? BatchGroup);
    public record EnqueueBatchResponse(Guid CorrelationId, int Count);

    private readonly IDocumentGenerationJobRepository _repo;
    private readonly IDocumentJobQueue _queue;

    public BatchGenerationController(IDocumentGenerationJobRepository repo, IDocumentJobQueue queue) {
        _repo = repo;
        _queue = queue;
    }

    [HttpPost("enqueue")]
    public async Task<IActionResult> Enqueue([FromBody] EnqueueBatchRequest req) {
        if (req.DataArray.ValueKind != JsonValueKind.Array)
            return BadRequest("DataArray must be JSON array of objects");
        var correlationId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var jobs = new List<DocumentGenerationJob>();
        var idx = 0;
        foreach (var item in req.DataArray.EnumerateArray()) {
            if (item.ValueKind != JsonValueKind.Object) continue;
            jobs.Add(new DocumentGenerationJob {
                Id = Guid.NewGuid(),
                TemplateId = req.TemplateId,
                Status = "Pending",
                InputData = item.GetRawText(),
                CreatedUtc = now.AddMilliseconds(idx++),
                CorrelationId = correlationId,
                BatchGroup = req.BatchGroup
            });
        }
        if (jobs.Count == 0) return BadRequest("No valid object items found");
        await _repo.InsertBatchAsync(jobs);
        _queue.SignalNewWork();
        return Ok(new EnqueueBatchResponse(correlationId, jobs.Count));
    }

    [HttpGet("correlation/{id:guid}")]
    public async Task<IActionResult> GetByCorrelation(Guid id) {
        var jobs = await _repo.GetByCorrelationAsync(id);
        return Ok(jobs);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id) {
        var job = await _repo.GetAsync(id);
        if (job == null) return NotFound();
        return Ok(job);
    }

    [HttpGet("{id:guid}/result")]
    public async Task<IActionResult> Download(Guid id) {
        var job = await _repo.GetAsync(id);
        if (job == null) return NotFound();
        if (job.Status != "Completed" || job.ResultFile == null) return BadRequest("Not completed");
        return File(job.ResultFile, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"{job.Id}.docx");
    }
}