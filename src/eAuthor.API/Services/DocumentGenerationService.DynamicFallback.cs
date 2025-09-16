// OPTIONAL SNIPPET: integrate dynamic docx fallback
// In your Generate method (where baseDoc is null), instead of HTML rendering you could:
//
// if (baseDoc == null && UseDynamicControlsFallback)
// {
//    var dynBuilder = _dynamicBuilder.Build(template);
//    // Open result docx and run merging (content controls already have placeholders).
//    // For simplicity, still using existing ProcessExpressions, etc., if needed.
// }
//
// This snippet is conceptual; integrate into existing class as needed.