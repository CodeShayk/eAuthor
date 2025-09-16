# eAuthor

A full-featured, extensible platform for designing, managing, and generating Microsoft Word (`.docx`) documents using XSD‑defined data structures, rich token expressions, dynamic repeaters, and real Word Content Controls (SDTs), including true `w14:repeatingSection` support.

---

## Table of Contents
1. Overview
2. Key Features
3. Architecture
4. Data & Template Model
5. Expression & Template Syntax
6. Controls & Styling
7. Repeaters & Loop Metadata
8. Conditional Logic (if / elseif / else)
9. Dynamic Word Content Controls
10. Document Generation Modes
11. Batch Generation & Job Queue
12. Security & Hardening
13. API Surface
14. Setup & Running
15. Database Schema (Summary)
16. Testing
17. Frontend Authoring Experience
18. Conversion (HTML Tokens → Dynamic SDTs)
19. Performance & Caching
20. Extensibility Points
21. Limitations / Future Enhancements
22. License / Attribution (placeholder)

---

## 1. Overview
This system enables users to:
- Define data structures via XSD.
- Build templates visually (React UI: drag & drop from XSD tree).
- Insert strongly-typed controls (TextBox, CheckBox, Grid, Repeater…).
- Use token expressions with formatting filters and conditionals.
- Generate Word documents by merging templates with runtime JSON data.
- Export or convert templates into fully dynamic `.docx` with real Word content controls (SDTs) including **true repeating sections** (`w14:repeatingSection / w14:repeatingSectionItem`).
- Run large batch jobs in the background.

---

## 2. Key Features
| Category | Capabilities |
|----------|--------------|
| Data Definition | XSD ingestion & cached parsing to hierarchical tree |
| Controls | TextBox, TextArea, CheckBox, RadioGroup, Grid, Repeater |
| Dynamic DOCX | Automatic SDT generation for all controls, real repeating sections |
| Expressions | `{{ /Path | date:yyyy-MM-dd | number:#,##0.00 | upper }}` + array indexing `/Orders/Order[0]/Total` |
| Conditionals | `if /path`, `elseif /path`, `else`, `end` |
| Repeaters | `{{ repeat /Orders/Order }}...{{ endrepeat }}` + metadata tokens |
| Loop Metadata | `{{ index }}`, `{{ zeroIndex }}`, `{{ first }}`, `{{ last }}`, `{{ odd }}`, `{{ even }}`, `{{ count }}` |
| Style Rendering | `StyleJson` → Run/Paragraph properties (fontSize, bold, color, alignment, spacing, etc.) |
| Base Templates | Optional stored base `.docx` with SDTs or dynamic build fallback |
| Conversion | HTML token template → auto-detected controls → dynamic SDT doc |
| Batch Jobs | Asynchronous generation queue with status & result retrieval |
| Security | JWT auth, HTML sanitization, size limits, safe filter set |
| Testing | NUnit suites for expressions, conditionals, repeaters, generation |
| Storage | Dapper + SQL Server schema (templates, controls, base docs, jobs) |

---

## 3. Architecture
```
+------------------+          +---------------------------+
| React Frontend   |  REST    | .NET 8 Web API            |
| - Drag/drop XSD  +--------->| - Auth (JWT)              |
| - Control editor |          | - XSD Parser + Cache      |
| - Style & props  |          | - Expression Engine       |
+------------------+          | - Conditional & Repeaters |
                               | - Dynamic DOCX Builder    |
                               | - Background Job Worker   |
                               | - Dapper Repos / SQL      |
                               +-------------+-------------+
                                             |
                                             v
                                      +-------------+
                                      | SQL Server  |
                                      +-------------+
```

---

## 4. Data & Template Model
Core entities:
- `XsdDescriptor` → stores raw XSD & parsed tree.
- `Template` → name, HTML body (legacy token storage), associated controls, optional `BaseDocxTemplateId`.
- `TemplateControl` → control type, data path, styling, formatting, options, bindings (for Grid/Repeater columns).
- `TemplateControlBinding` → per column binding for repeating/tabular controls.
- `BaseDocxTemplate` → stored binary `.docx`.
- `DocumentGenerationJob` → batch job tracking.

---

## 5. Expression & Template Syntax
### Basic Expression
```
{{ /Customer/Name }}
```
### Filters
```
{{ /Order/Date | date:yyyy-MM-dd }}
{{ /Order/Total | number:#,##0.00 }}
{{ /Customer/IsPremium | bool:Yes:No }}
{{ /Path | upper | trim }}
```
Filter order = left → right.

### Array Indexing
```
{{ /Orders/Order[0]/OrderNumber }}
{{ /Orders/Order[3]/Items[2]/Sku }}
```

### Relative Paths (inside repeater):
If inside `repeat /Orders/Order`, then:
```
{{ OrderNumber }}  (equivalent to {{ /Orders/Order[index]/OrderNumber }})
```

---

## 6. Controls & Styling
| ControlType | Description | Special Notes |
|-------------|-------------|---------------|
| TextBox | Single-value text/number | Uses SDT with Tag holding path |
| TextArea | Multi-line narrative | Same underlying SDT type |
| CheckBox | Boolean value | Word check box SDT |
| RadioGroup | Enumerated selection | One SDT per option (group tag) |
| Grid | Tabular collection | Real repeating section; columns map to relative paths |
| Repeater | Lightweight repeating rows/sections | Same engine as Grid but conceptually list |

### StyleJson Mapping
Example:
```json
{
  "fontSize": "11pt",
  "bold": true,
  "italic": false,
  "underline": false,
  "color": "#2E74B5",
  "backgroundColor": "#EAF2FA",
  "alignment": "center",
  "spacingBefore": "6",
  "spacingAfter": "6"
}
```
Supported keys:
- Run: `fontSize`, `bold`, `italic`, `underline`, `color`, `backgroundColor`
- Paragraph: `alignment` (left|center|right|justify), `spacingBefore`, `spacingAfter` (points)

---

## 7. Repeaters & Loop Metadata
Block syntax:
```
{{ repeat /Orders/Order }}
  Line {{ index }} of {{ count }}:
  {{ OrderNumber }} (First? {{ first }}) (Odd? {{ odd }})
{{ endrepeat }}
```
Metadata variables inside each repeater iteration:
| Variable | Meaning |
|----------|---------|
| index | 1-based index |
| zeroIndex | 0-based index |
| first | true if first item |
| last | true if last item |
| odd | true if iteration number is odd (1-based) |
| even | true if even |
| count | total item count |

Nested repeaters supported (recursive expansion).

---

## 8. Conditional Logic
```
{{ if /Customer/IsPremium }}
  Welcome premium user!
{{ elseif /Customer/Trial }}
  Thanks for trying us.
{{ else }}
  Please upgrade.
{{ end }}
```
Nesting allowed. Condition expression may include filters.

---

## 9. Dynamic Word Content Controls (SDTs)
All controls can be rendered into a dynamic DOCX (no base upload required):
- Regular controls → standard `w:sdt` blocks.
- Repeater/Grid → real `w14:repeatingSection` + `w14:repeatingSectionItem` containing a table.
- Tags encode control metadata (`controlId|Type|DataPath`).
- Template can be exported via:  
  `GET /api/templates/{id}/export-dynamic-docx`

Optional: Convert HTML token-based template into dynamic controls and attach as base via:
```
POST /api/templates/{id}/convert-html-to-dynamic
{
  "attachAsBase": true
}
```

---

## 10. Document Generation Modes
| Mode | When Used | Description |
|------|-----------|-------------|
| HTML Token Merge | Legacy / quick prototypes | Replaces `{{ ... }}` in stored HTML then constructs basic DOCX |
| Dynamic SDT Merge | With BaseDocxTemplateId or dynamic build | Fills SDT tags based on data paths |
| Repeater/Table Expansion | At generation | Builds rows using binding definitions |
| Batch Merge | Via job queue | Asynchronously processes many data payloads |

---

## 11. Batch Generation & Job Queue
Endpoints:
- `POST /api/BatchGeneration/enqueue`
  ```
  {
    "templateId": "...",
    "dataArray": [ { ... }, { ... } ],
    "BatchGroup": "RunLabel"
  }
  ```
- `GET /api/BatchGeneration/correlation/{correlationId}`
- `GET /api/BatchGeneration/{jobId}`
- `GET /api/BatchGeneration/{jobId}/result`

Worker:
- In-memory signal channel
- Dapper-based row locking (`SELECT ... WITH (UPDLOCK, READPAST)`)
- Status: Pending → Processing → Completed / Failed

---

## 12. Security & Hardening
| Aspect | Implementation |
|--------|----------------|
| Auth | JWT Bearer (symmetric key) |
| Input Limits | Kestrel & IIS body size caps |
| HTML Sanitization | `Ganss.Xss` (allows `data-control-id`) |
| XSD Cache | In-memory with TTL |
| Placeholder Safety | Filters restricted to known set |
| Future (Recommended) | Rate limiting, audit logging, multi-tenant scoping, RBAC |

---

## 13. API Surface (Summary)
| Method | Path | Purpose |
|--------|------|---------|
| POST | /api/xsd | Upload XSD |
| GET | /api/xsd/{id} | Get parsed XSD tree |
| GET | /api/templates | List templates |
| GET | /api/templates/{id} | Get template with controls |
| POST | /api/templates | Create/Update template |
| GET | /api/templates/{id}/export-dynamic-docx | Build dynamic DOCX (SDTs) |
| POST | /api/templates/{id}/convert-html-to-dynamic | Convert tokens to controls |
| POST | /api/BaseDocxTemplates/upload | Upload base `.docx` |
| POST | /api/generate | Generate single document |
| POST | /api/BatchGeneration/enqueue | Enqueue batch jobs |
| GET | /api/BatchGeneration/correlation/{cid} | Correlated job statuses |
| GET | /api/BatchGeneration/{jobId}/result | Download job output |
| (Optional future) | /api/templates/{id}/attach-dynamic-base | Persist dynamic doc as base |

---

## 14. Setup & Running

### Prerequisites
- .NET 8 SDK
- SQL Server (localdb, container, or instance)
- Node.js 18+
- (Optional) Docker for future containerization

### Database
```bash
sqlcmd -S . -i database/schema.sql
sqlcmd -S . -i database/seed-data.sql
sqlcmd -S . -i database/alter-2025-09-extend.sql
sqlcmd -S . -i database/alter-2025-09-batch-jobs.sql
sqlcmd -S . -i database/alter-2025-09-controls-metadata.sql
```

### Backend
```bash
cd backend/src/WordTemplating.Api
dotnet restore
dotnet run
# API at http://localhost:5100
```

### Frontend
```bash
cd frontend
npm install
npm run dev
# UI at http://localhost:5173
```

### Auth (Dev)
Generate a JWT with payload:
```
{
  "sub": "dev-user",
  "exp": <future epoch seconds>
}
```
Sign with secret in `appsettings.json` (Jwt:Key). Add header:
```
Authorization: Bearer <token>
```

---

## 15. Database Schema (Summary)
Tables:
- `Xsds`
- `Templates`
- `TemplateControls`
- `TemplateControlBindings`
- `BaseDocxTemplates`
- `DocumentGenerationJobs`

Key relationships:
```
Template 1---* TemplateControls 1---* TemplateControlBindings
Template --- (optional) BaseDocxTemplate
Template ---* DocumentGenerationJobs
```

---

## 16. Testing
Run unit tests:
```bash
cd backend/tests/WordTemplating.Tests
dotnet test
```
Coverage includes:
- Expression parsing & filtering
- Array indexing
- Conditional logic (if/elseif/else)
- Repeater expansion & metadata
- Document generation smoke tests

---

## 17. Frontend Authoring Experience
Features:
- XSD tree drag & drop → creates controls (auto-type inference).
- Toolbox for manual control insertion.
- Properties pane: label, required, default value, format, width, style JSON, columns, options.
- Inline token insertion for quick prototyping (`{{ /Path }}`).
- Convert existing HTML tokens to dynamic controls later.

---

## 18. Conversion (HTML → Dynamic SDTs)
Workflow:
1. Author quick prototype with tokens directly in HTML editor.
2. Save template.
3. Call:
   ```
   POST /api/templates/{id}/convert-html-to-dynamic
   {
     "attachAsBase": true
   }
   ```
4. System:
   - Scans for `{{ /Absolute/Paths }}`.
   - Creates inferred controls (TextBox or CheckBox).
   - Builds and stores dynamic DOCX as Base.
   - Future merges use content controls.

---

## 19. Performance & Caching
| Layer | Strategy |
|-------|----------|
| XSD Parsing | IMemoryCache keyed by hash |
| Template Retrieval | Lightweight joins via Dapper |
| Expression Evaluation | On-demand parse (can be precompiled future) |
| Batch Jobs | Pessimistic row locking + in-memory signal channel |

Potential optimizations:
- Pre-parse & cache expression AST per template version.
- Output caching for identical (template + data hash) combinations.
- Parallel batch workers (configurable degree).

---

## 20. Extensibility Points
| Concern | Interface / Class |
|---------|------------------|
| Expressions | `IExpressionParser`, `IExpressionEvaluator` |
| Repeaters | `IRepeaterBlockProcessor` |
| Conditionals | `IConditionalBlockProcessor` |
| Styles | `StyleRenderer` |
| Dynamic DOCX | `DynamicDocxBuilderService` |
| Data Access | Repository abstractions (e.g., `ITemplateRepository`) |
| Batch Queue | `IDocumentJobQueue`, worker host service |
| HTML → Controls | `HtmlToDynamicConverter` |

Add new filters:
- Extend `ExpressionEvaluator.ApplyFilter`.

Add new control types:
- Extend UI (Toolbox), data model & `DynamicDocxBuilderService`.

---

## 21. Limitations / Future Enhancements
Potential roadmap:
- Reverse import (read filled DOCX → JSON data).
- ElseIf chains with inline expressions (e.g., comparisons).
- Arithmetic & string functions (substring, replace, join).
- Templated partials / includes.
- Localization / resource dictionaries for labels.
- Role-based authorization & multi-tenant isolation.
- Export bundles: ZIP all batch job results.
- PDF rendering pipeline (LibreOffice headless or third-party service).
- Real-time collaborative template editing.
- Versioning & diff of templates + controls.

---

## 22. License / Attribution
(Choose an appropriate license; MIT recommended for libraries. Add LICENSE file.)

---

## 23. Quick Reference Cheat Sheet

| Task | Action |
|------|--------|
| Upload XSD | POST /api/xsd |
| Fetch XSD Tree | GET /api/xsd/{id} |
| Save Template | POST /api/templates |
| Export Dynamic DOCX | GET /api/templates/{id}/export-dynamic-docx |
| Convert HTML → Dynamic | POST /api/templates/{id}/convert-html-to-dynamic (attachBase optional) |
| Generate Document | POST /api/generate |
| Enqueue Batch | POST /api/BatchGeneration/enqueue |
| Poll Batch | GET /api/BatchGeneration/correlation/{cid} |
| Download Job Output | GET /api/BatchGeneration/{jobId}/result |

---

## 24. Example Snippets

Repeater with Conditionals & Metadata:
```
{{ repeat /Invoice/Lines/Line }}
  {{ index }}. {{ Description }} - {{ Amount | number:#,##0.00 }}
  {{ if DiscountApplied }}(Discount){{ elseif SpecialFlag }}(Special){{ end }}
{{ endrepeat }}
```

Grid/Repeater Column Binding Example:
- DataPath: `/Invoice/Lines/Line`
- Bindings:
  - `Description` → `Description`
  - `Net` → `NetAmount`
  - `Tax` → `TaxAmount`
  - `Total` → `TotalAmount`

StyleJson Example (applied to all cells in a control):
```json
{
  "fontSize": "10pt",
  "bold": true,
  "color": "#222222",
  "alignment": "center",
  "spacingBefore": "4",
  "spacingAfter": "4"
}
```

---

## 25. Troubleshooting
| Issue | Possible Cause | Resolution |
|-------|----------------|------------|
| Tokens not replaced | Path mismatch | Confirm absolute path matches data JSON |
| Repeater empty | Collection not array or wrong path | Inspect JSON structure |
| Batch jobs stuck | Worker not running / no signal | Check hosted service logs |
| Styles ignored | Invalid JSON / unsupported key | Validate with JSON parser |
| CheckBox not toggled | Generator not implementing boolean substitution yet | Extend generation merge logic for runtime states |

---

## 26. Contributing
1. Fork / create feature branch.
2. Add/update unit tests.
3. Follow existing file + dependency patterns.
4. Submit PR with descriptive title + screenshots (if UI changes).

---

## 27. Disclaimer
This implementation focuses on core templating & generation patterns and is not a final production-hardened system. Perform security review, load tests, and compliance checks before deployment in regulated environments.

---

Happy templating! Feel free to request additional automation, PR scaffolding, or advanced formatting features.
