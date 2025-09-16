# eAuthor

A full-featured, extensible platform for designing, managing, and generating Microsoft Word (`.docx`) documents using XSD‑defined data structures, rich token expressions, dynamic repeaters, and Word Content Controls (SDTs), including true `w14:repeatingSection` support.

---

## Key Features
- XSD → interactive data tree (drag & drop)
- Control types: TextBox, TextArea, CheckBox, RadioGroup, Grid, Repeater
- Real Word content controls (SDTs) + true `w14:repeatingSection` support
- Expressions: formatting filters, array indexing, relative paths
- Conditional logic: `if /path ... elseif ... else ... end`
- Repeaters with loop metadata: `index`, `first`, `last`, `odd`, `even`, `count`
- StyleJson → Word Run/Paragraph styling (font size, bold, alignment, colors)
- HTML token → dynamic control conversion (auto‑generate DOCX)
- Batch job queue for large-scale document generation
- JWT auth, HTML sanitization, input size limits

---

## Quick Start

### 1. Backend
```bash
sqlcmd -S . -i database/schema.sql
sqlcmd -S . -i database/seed-data.sql
dotnet run --project backend/src/WordTemplating.Api
```

### 2. Frontend
```bash
cd frontend
npm install
npm run dev
```

### 3. Auth
Generate a JWT (HS256) with claim `sub` and future `exp`, sign with `Jwt:Key` from `appsettings.json`, send:
```
Authorization: Bearer <token>
```

---

## Core Workflow
1. Upload XSD: POST /api/xsd  
2. Build template in UI (drag data → creates controls)  
3. (Optional) Insert tokens like `{{ /Customer/Name }}`  
4. Save template: POST /api/templates  
5. (Optional) Convert tokens → dynamic SDTs: POST /api/templates/{id}/convert-html-to-dynamic  
6. Generate:
   - Single: POST /api/generate
   - Batch: POST /api/BatchGeneration/enqueue  

---

## Expression Examples
```
{{ /Order/Date | date:yyyy-MM-dd }}
{{ /Order/Total | number:#,##0.00 }}
{{ /Customer/IsPremium | bool:Yes:No }}
{{ /Orders/Order[0]/OrderNumber }}
```

### Repeater
```
{{ repeat /Orders/Order }}
  {{ index }}: {{ OrderNumber }} (First? {{ first }})
{{ endrepeat }}
```

### Conditional
```
{{ if /Status/Open }}Open
{{ elseif /Status/Closed }}Closed
{{ else }}Unknown
{{ end }}
```

---

## Minimal API Reference (Highlights)
| Action | Endpoint |
|--------|----------|
| Upload XSD | POST /api/xsd |
| Get XSD Tree | GET /api/xsd/{id} |
| Save Template | POST /api/templates |
| Export Dynamic DOCX | GET /api/templates/{id}/export-dynamic-docx |
| Convert HTML → Controls | POST /api/templates/{id}/convert-html-to-dynamic |
| Generate Single | POST /api/generate |
| Enqueue Batch | POST /api/BatchGeneration/enqueue |
| Poll Batch | GET /api/BatchGeneration/correlation/{cid} |
| Download Result | GET /api/BatchGeneration/{jobId}/result |

---

## Data Shapes (Conceptual)
```
Template
  ├─ Controls[]
  │    ├─ ControlType (TextBox|Grid|Repeater...)
  │    ├─ DataPath (/Root/Sub/Field or /Collection)
  │    ├─ Bindings[] (columns for Grid/Repeater)
  │    └─ StyleJson
```

---

## Styling (StyleJson)
```json
{
  "fontSize": "11pt",
  "bold": true,
  "color": "#2E74B5",
  "alignment": "center"
}
```

---

## Batch Generation
```json
POST /api/BatchGeneration/enqueue
{
  "templateId": "GUID",
  "dataArray": [{ "Customer": {"Name":"Alice"} }, { "Customer": {"Name":"Bob"} }],
  "BatchGroup": "Run1"
}
```

---

## Contributing
1. Fork → branch → implement feature + tests  
2. Follow existing service & repository patterns  
3. Submit PR with clear description  

---

## Roadmap (Abbrev.)
- Additional filters (math/string)
- Reverse DOCX → JSON extraction
- Partial/Include support
- PDF export
- Multi-tenant + RBAC

---

## License
Add a LICENSE (e.g., MIT) before distribution.

---

Happy templating!
