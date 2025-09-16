// Add registrations (only the new lines shown; keep existing ones)
using eAuthor.Services;

builder.Services.AddScoped<StyleRenderer>();
builder.Services.AddScoped<DynamicDocxBuilderService>();
builder.Services.AddScoped<HtmlToDynamicConverter>();