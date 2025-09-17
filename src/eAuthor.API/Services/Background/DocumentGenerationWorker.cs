using System.Text.Json;
using eAuthor.Models;
using eAuthor.Repositories;
using eAuthor.Services.Impl;

namespace eAuthor.Services.Background;

public class DocumentGenerationWorker : BackgroundService
{
    private readonly ILogger<DocumentGenerationWorker> _logger;
    private readonly IDocumentGenerationJobRepository _jobRepo;
    private readonly ITemplateService _templateService;
    private readonly IDocumentGenerationService _docService;
    private readonly IBaseDocxTemplateRepository _baseRepo;
    private readonly IDocumentJobQueue _queue;

    public DocumentGenerationWorker(
        ILogger<DocumentGenerationWorker> logger,
        IDocumentGenerationJobRepository jobRepo,
        ITemplateService templateService,
        IDocumentGenerationService docService,
        IBaseDocxTemplateRepository baseRepo,
        IDocumentJobQueue queue)
    {
        _logger = logger;
        _jobRepo = jobRepo;
        _templateService = templateService;
        _docService = docService;
        _baseRepo = baseRepo;
        _queue = queue;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DocumentGenerationWorker started");
        while (!stoppingToken.IsCancellationRequested)
        {
            var processedAny = false;

            while (await TryProcessOneAsync(stoppingToken))
                processedAny = true;

            if (!processedAny)
                try
                {
                    await _queue.GetSignalsAsync(stoppingToken).GetAsyncEnumerator(stoppingToken).MoveNextAsync();
                }
                catch (OperationCanceledException) { }
        }
    }

    private async Task<bool> TryProcessOneAsync(CancellationToken ct)
    {
        var (result, job) = await _jobRepo.TryStartNextPendingAsync();
        if (!result)
            return false;

        if (job == null)
            return false;

        try
        {
            var template = await _templateService.GetAsync(job.TemplateId);
            if (template == null)
            {
                await _jobRepo.CompleteFailureAsync(job.Id, "Template not found");
                return true;
            }
            BaseDocxTemplate? baseDoc = null;
            var prop = template.GetType().GetProperty("BaseDocxTemplateId");
            if (prop?.GetValue(template) is Guid guid && guid != Guid.Empty)
                baseDoc = await _baseRepo.GetAsync(guid);

            var docElem = JsonDocument.Parse(job.InputData).RootElement;
            var bytes = _docService.Generate(template, docElem, baseDoc);
            await _jobRepo.CompleteSuccessAsync(job.Id, bytes);
        }
        catch (Exception ex)
        {
            await _jobRepo.CompleteFailureAsync(job.Id, ex.Message);
        }
        return true;
    }
}