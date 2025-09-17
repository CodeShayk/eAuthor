using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using eAuthor;
using eAuthor.Repositories;
using eAuthor.Repositories.Impl;
using eAuthor.Services;
using eAuthor.Services.Background;
using eAuthor.Services.Expressions;
using eAuthor.Services.Impl;
using Ganss.Xss;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

// using WordTemplating.Core.Repositories;   // Uncomment when repository interfaces are present
// using WordTemplating.Core.Background;     // Uncomment if you have a batch worker hosted service

// NOTE: This Program.cs is a consolidated "final" version aligned with the documented architecture.
// Adjust namespaces to match your actual project structure if they differ.

var builder = WebApplication.CreateBuilder(args);

// ------------------------------------------------------------
// Configuration & Constants
// ------------------------------------------------------------
var jwtKey = builder.Configuration["Jwt:Key"] ?? "DEV_INSECURE_KEY_CHANGE_ME";
var allowAnyCors = builder.Configuration.GetValue("Cors:AllowAny", true);

// ------------------------------------------------------------
// JSON Options
// ------------------------------------------------------------
builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Return problem details for model validation errors
builder.Services.Configure<ApiBehaviorOptions>(opt =>
{
    opt.InvalidModelStateResponseFactory = ctx =>
    {
        var problem = new ValidationProblemDetails(ctx.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Request validation failed",
        };
        return new BadRequestObjectResult(problem);
    };
});

// ------------------------------------------------------------
// CORS
// ------------------------------------------------------------
builder.Services.AddCors(opt =>
{
    if (allowAnyCors)
    {
        opt.AddPolicy("DevOpen",
            p => p.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod());
    }
    else
    {
        opt.AddPolicy("Restricted", p =>
        {
            var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
            p.WithOrigins(origins)
             .AllowAnyHeader()
             .AllowAnyMethod();
        });
    }
});

// ------------------------------------------------------------
// Authentication (JWT)
// ------------------------------------------------------------
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.RequireHttpsMetadata = false;
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });

// ------------------------------------------------------------
// Authorization (basic – extend with policies/roles as needed)
// ------------------------------------------------------------
builder.Services.AddAuthorization();

// ------------------------------------------------------------
// Swagger / OpenAPI
// ------------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(cfg =>
{
    cfg.SwaggerDoc("v1", new()
    {
        Title = "eAuthor API",
        Version = "v1",
        Description = "Dynamic document generation & templating platform"
    });

    // JWT header
    cfg.AddSecurityDefinition("Bearer", new()
    {
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey
    });
    cfg.AddSecurityRequirement(new()
    {
        {
            new() { Reference = new() { Id = "Bearer", Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme } },
            Array.Empty<string>()
        }
    });
});

// ------------------------------------------------------------
// Core / Domain Services Registration
// ------------------------------------------------------------
builder.Services.AddSingleton<IDocumentGenerationService, DocumentGenerationService>();
builder.Services.AddSingleton<ITemplateService, TemplateService>();
builder.Services.AddSingleton<IHtmlToDynamicConverter, HtmlToDynamicConverter>();
builder.Services.AddSingleton<IHtmlSanitizer, HtmlSanitizer>();
builder.Services.AddSingleton<IXsdService, XsdService>();
builder.Services.AddSingleton<IMemoryCache, MemoryCache>();

// Expression + Conditional system
builder.Services.AddSingleton<IExpressionParser, ExpressionParser>();
builder.Services.AddSingleton<IExpressionEvaluator, ExpressionEvaluator>();
builder.Services.AddSingleton<IConditionalBlockProcessor, ConditionalBlockProcessor>();

// Rendering / DOCX build
builder.Services.AddSingleton<IStyleRenderer, StyleRenderer>();
builder.Services.AddSingleton<IDynamicDocxBuilderService, DynamicDocxBuilderService>();

// If you have repeater / conditional processors beyond the interface above, register them here.
builder.Services.AddSingleton<IRepeaterBlockProcessor, RepeaterBlockProcessor>();

// Repositories & Data Access: register your custom implementations (uncomment & adjust):
builder.Services.AddSingleton<ITemplateRepository, TemplateRepository>();
builder.Services.AddSingleton<IXsdRepository, XsdRepository>();
builder.Services.AddSingleton<IDocumentGenerationJobRepository, DocumentGenerationJobRepository>();
builder.Services.AddSingleton<IBaseDocxTemplateRepository, BaseDocxTemplateRepository>();
builder.Services.AddSingleton<IDapperContext, DapperContext>();

// Batch job queue / worker (uncomment when implemented):
builder.Services.AddSingleton<IDocumentJobQueue, InMemoryDocumentJobQueue>();
builder.Services.AddHostedService<DocumentGenerationWorker>();

// ------------------------------------------------------------
// Health & Monitoring
// ------------------------------------------------------------
builder.Services.AddHealthChecks();

// ------------------------------------------------------------
// Build App
// ------------------------------------------------------------
var app = builder.Build();

// ------------------------------------------------------------
// Middleware Pipeline
// ------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Security headers (basic – extend as needed)
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    ctx.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    ctx.Response.Headers.TryAdd("X-XSS-Protection", "1; mode=block");
    await next();
});

// Global exception handling (lightweight)
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        var problem = new
        {
            error = "Unhandled server error",
            traceId = context.TraceIdentifier
        };
        await context.Response.WriteAsJsonAsync(problem);
    });
});

app.UseCors(allowAnyCors ? "DevOpen" : "Restricted");
app.UseAuthentication();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Minimal health endpoint(s)
app.MapHealthChecks("/health");
app.MapGet("/ready", () => Results.Ok(new { status = "ready" }));

// Example minimal endpoint (can remove if all logic is via controllers)
app.MapGet("/", () => Results.Ok(new
{
    name = "Word Templating & Dynamic DOCX Platform",
    version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "dev"
}));

// ------------------------------------------------------------
// Start
// ------------------------------------------------------------
app.Run();

/// <summary>
/// Dummy Program class to satisfy certain hosting scenarios / tests.
/// </summary>
public partial class Program
{
    // Intentionally left blank – used for WebApplicationFactory in integration tests.
}