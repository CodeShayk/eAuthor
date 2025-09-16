// Conceptual snippet: Attach dynamic docx as base
/*
[HttpPost("{id:guid}/attach-dynamic-base")]
public async Task<IActionResult> AttachDynamicBase(Guid id,
    [FromServices] DynamicDocxBuilderService builder,
    [FromServices] IBaseDocxTemplateRepository baseRepo,
    [FromServices] ITemplateRepository templateRepo) {

    var template = await _svc.GetAsync(id);
    if (template == null) return NotFound();
    var fileBytes = builder.Build(template);
    var baseDoc = new BaseDocxTemplate {
        Id = Guid.NewGuid(),
        Name = $"{template.Name}-auto",
        FileContent = fileBytes,
        CreatedUtc = DateTime.UtcNow
    };
    await baseRepo.InsertAsync(baseDoc);
    // Update template with BaseDocxTemplateId (implement patch)
    template.GetType().GetProperty("BaseDocxTemplateId")?.SetValue(template, baseDoc.Id);
    await templateRepo.UpsertAsync(template);
    return Ok(new { baseDocId = baseDoc.Id });
}
*/