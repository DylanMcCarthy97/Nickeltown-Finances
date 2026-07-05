# Receipt Import System — Architecture

Nickeltown Finances v2.1+ redesign for effortless receipt capture from mobile, desktop, and scanner devices. Goal: phone photo → OCR'd, categorised, transaction-matched receipt in seconds — Hubdoc/Dext-style UX on a local-first desktop app.

---

## Design Principles

1. **Receipt-first inbox** — Receipts arrive before transactions exist. OCR, AI, and matching enrich an inbox item; the user commits to match, ignore, or create new.
2. **Local network only** — Mobile upload uses a temporary embedded HTTP server on the desktop. No public endpoints, no cloud dependency in v1.
3. **Pipeline, not monolith** — Upload → preprocess → OCR → AI parse → match → commit are separate services behind interfaces.
4. **Suggestions never overwrite** — OCR/AI results live on inbox items and attachment metadata until the user accepts them (existing rule preserved).
5. **Future-ready** — Cloud sync, email forwarding, and supplier learning plug in via new `IReceiptImportSource` implementations without changing the pipeline.

---

## System Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         Nickeltown Finance (WPF Desktop)                     │
│  ┌──────────────┐  ┌─────────────────┐  ┌──────────────────────────────┐  │
│  │ Receipt      │  │ Mobile Upload   │  │ Processing Queue             │  │
│  │ Import Page  │──│ Host (Kestrel)  │──│ (background worker)          │  │
│  │ QR + Inbox   │  │ + static PWA    │  │ Upload→OCR→AI→Match→Ready  │  │
│  └──────────────┘  └────────┬────────┘  └──────────────────────────────┘  │
│         │                   │                          │                     │
│         │            LAN only (same subnet)            │                     │
└─────────┼───────────────────┼──────────────────────────┼─────────────────────┘
          │                   │                          │
   Drag/Drop/Paste/     ┌─────▼─────┐              ┌─────▼─────┐
   Scanner (TWAIN/WIA)  │ Phone PWA │              │  LiteDB   │
                        │ Camera +  │              │  + files/ │
                        │ offline   │              │  inbox/   │
                        │ queue     │              └───────────┘
                        └───────────┘
```

---

## Projects & Layers

| Layer | Location | Responsibility |
|-------|----------|----------------|
| **Core** | `NickeltownFinance.Core` | Models, enums, DTOs, service/repository interfaces |
| **Infrastructure** | `NickeltownFinance.Infrastructure` | LiteDB repos, OCR/AI/scanner implementations, queue worker |
| **Mobile Upload** | `NickeltownFinance.MobileUpload` *(new)* | Embedded ASP.NET Core minimal API + PWA static files |
| **UI** | `NickeltownFinance` | Receipt Import page, QR dialog, inbox review, attachment panel |

The Mobile Upload host runs **inside the WPF process** (same DI container). It starts when the user opens "Import Receipt" or enables mobile upload in settings, and stops when idle or on app exit.

---

## Mobile Upload (Priority 1)

### Flow

1. Desktop generates a cryptographically random **upload token** (256-bit, URL-safe base64).
2. Token expires after **5 minutes**. One active session at a time; refreshing QR rotates the token.
3. Embedded server binds to `0.0.0.0:{port}` on a configurable port (default **7842**). Firewall rule prompt on first use.
4. QR encodes `https://{desktop-lan-ip}:{port}/upload?token={token}`.
5. Phone opens PWA or browser page. Page validates token via `GET /api/session`.
6. User captures/selects file → client-side preprocessing (optional) → `POST /api/upload` with `Authorization: Bearer {token}`.
7. Server validates: token valid, not expired, client IP on same /24 subnet as desktop (configurable).
8. File saved to inbox → `ReceiptImportItem` created → pipeline enqueued.
9. Desktop UI receives event via `IReceiptImportQueue.ItemUpdated` → shows "Receipt Received".

### Security

| Control | Implementation |
|---------|----------------|
| Token lifetime | 5 min, in-memory `UploadSession` store |
| Network scope | Reject requests where client IP not in allowed prefix (desktop's LAN subnet) |
| HTTPS | Self-signed cert generated per install; phone trusts on first visit (or HTTP on LAN with token-only auth — configurable) |
| No persistence of tokens | Sessions never written to LiteDB |
| Rate limit | Max 20 uploads per token; max 50 MB per file |

### PWA (`wwwroot/` served by Mobile Upload host)

- **manifest.json** — installable on iOS/Android
- **service-worker.js** — offline queue in IndexedDB; sync on reconnect
- **upload.html** — Take Photo / Choose Photo / Upload PDF
- **camera.js** — MediaDevices API + client-side edge detect preview (OpenCV.js optional; server-side processing is authoritative)
- **history.html** — upload history, retry failed

---

## Receipt Camera Processing

When source is mobile camera, run **server-side** image pipeline after upload (desktop has more CPU; consistent results):

```
Raw bytes → ReceiptImageProcessor
  1. Auto-rotate (EXIF)
  2. Edge detection (Canny + contour → largest quadrilateral)
  3. Perspective transform (crop to receipt)
  4. Deskew (Hough line / minAreaRect)
  5. Adaptive threshold + contrast stretch
  6. Background → white (morphological close + flood fill)
  → Processed JPEG stored alongside original
```

User can **Retake** (mobile), **Rotate**, **Manual crop** (mobile canvas or desktop viewer), **Accept** → triggers OCR stage.

Interface: `IReceiptImageProcessor.ProcessAsync(ReceiptImageProcessRequest)`.

Implementation: `OpenCvSharp` or `ImageSharp` + custom pipeline in Infrastructure.

---

## Processing Queue

Central orchestrator: `IReceiptImportService` + `ReceiptImportQueueWorker` (hosted `BackgroundService` in WPF via `IHost` or `Timer`-based worker).

### States (`ReceiptImportStatus`)

```
Queued → Uploading → Preprocessing → ProcessingOcr → AiParsing → MatchingTransaction → Ready
                                                                                      ↓
                                                                              Failed (retryable)
                                                                                      ↓
                                                                              Committed | Ignored
```

UI shows live status per item. Multiple uploads queue automatically (FIFO with parallel OCR where CPU allows).

### Commit actions

| Action | Behaviour |
|--------|-----------|
| **Match** | Attach processed file to suggested `Transaction`; copy OCR fields to attachment metadata |
| **Create New** | Create expense transaction from OCR suggestions; attach receipt |
| **Ignore** | Mark inbox item ignored; keep file for audit or delete per setting |

---

## OCR

Interface: `IOcrService` (existing, extended).

Extract:

- Supplier, Date, Invoice Number, ABN, Subtotal, GST, Total, Payment Method

Each field includes **confidence 0–100**.

### Provider strategy

| Provider | When |
|----------|------|
| `WindowsOcrService` | Default on Windows 10+ (built-in, offline) |
| `TesseractOcrService` | Fallback / cross-platform future |
| `AzureDocumentIntelligenceOcrService` | Optional cloud (future) |

OCR output stored on `ReceiptImportItem` and copied to `Attachment` on commit. **Never** auto-writes `Transaction` fields.

---

## AI Category Parsing

Interface: `IReceiptAiParser`.

Input: OCR text + supplier history + amount patterns.  
Output: `ReceiptAiSuggestion` with category + confidence per field.

Categories align with existing `Category` seed data:

Fuel, Alcohol, Food, Office, Cleaning, Maintenance, Utilities, Equipment, Membership, Bank Fees, Repairs, Other.

### Provider strategy

| Provider | When |
|----------|------|
| `RuleBasedReceiptAiParser` | v1 — supplier keyword rules + amount heuristics (extends `CategorisationService`) |
| `LocalLlamaReceiptAiParser` | Future — on-device LLM |
| `CloudReceiptAiParser` | Future — OpenAI/Azure with user consent |

Supplier learning (future): `ISupplierProfileRepository` maps ABN/supplier name → preferred category, fed back into parser.

---

## Bank Transaction Matching

Interface: `IReceiptMatchingService`.

Rules (v1):

- ANZ transaction (expense) within **±7 days** of receipt date
- **Same amount** (exact match on `ExpenseAmount` vs OCR total, ±$0.01 tolerance)
- Optional boost: description contains supplier keyword

Output: `ReceiptMatchSuggestion` with transaction ID + confidence.

UI: "Possible Match Found" with Match / Ignore / Create New.

Future: fuzzy amount, split payments, Square deposit linkage.

---

## Attachments (Enhanced)

Every transaction supports **unlimited** attachments of any supported kind (Receipt, Invoice, Photo, PDF, etc.).

Existing `IAttachmentService` extended with:

- `ReplaceAsync(attachmentId, newFile)`
- `GenerateThumbnailAsync(attachmentId)`
- Drag-drop at transaction row level (exists) + inbox level (new)

Stored metadata (extended `Attachment` model):

- Original + processed paths, file hash (SHA-256), upload source/device, full OCR + AI confidence fields

---

## Desktop Import

Interface: `IReceiptImportService.ImportFromDesktopAsync`.

Supports:

- Drag & drop (files, folders)
- Browse multi-select
- Paste (Ctrl+V) — files or clipboard image
- ZIP extraction (one level; skip macOS `__MACOSX`)
- Formats: PDF, JPEG, PNG, HEIC, WEBP, TIFF

Each file → separate `ReceiptImportItem` in queue.

---

## Scanner Support

Interface: `IScannerService`.

| API | Package | Notes |
|-----|---------|-------|
| **WIA** | `NTwain` or COM interop | Primary on Windows |
| **TWAIN** | `NTwain` | ADF, duplex, multi-page |
| Default | 300 DPI, PDF output (multi-page via QuestPDF merge) |

UI: Scanner dialog from Receipt Import page — detect devices, Scan Single / Scan Multiple / ADF / Duplex.

Stub: `NullScannerService` until NTwain integrated.

---

## Database Schema

### New collection: `receipt_import_items`

See `ReceiptImportItem` model in Core. Indexed on: `Status`, `CreatedDate`, `FileHash`, `SuggestedMatchTransactionId`.

### Extended: `attachments`

Additional OCR/AI/provenance fields (nullable for backward compatibility).

### New collection: `receipt_import_batches` *(optional)*

Groups desktop folder/ZIP/scanner sessions for undo (mirrors `ImportBatch` pattern).

### File layout

```
%AppData%/NickeltownFinance/
  files/
    inbox/                    ← uncommitted receipts
      {importItemId}/
        original.{ext}
        processed.jpg         ← after camera pipeline
        thumb.jpg
    attachments/              ← committed (existing)
      {transactionId}/
        {guid}.{ext}
```

---

## UI Surfaces

### Receipt Import Page (new sidebar item or Import hub tab)

1. **Mobile Upload card** — QR code, session timer, "Open on this PC" link
2. **Desktop Import** — drop zone, browse, paste hint
3. **Scanner** — device picker + scan buttons
4. **Inbox queue** — cards with status, thumbnail, OCR summary, match suggestion, actions
5. **History** — committed/ignored with undo

### Transaction Editor (enhanced)

- Attachment list: view, download, replace, delete, drag-reorder
- Show OCR badges when attachment has extracted data

---

## Event Flow (Mobile → Desktop)

```
Phone POST /api/upload
  → MobileUploadController validates token + IP
  → ReceiptImportService.EnqueueFromUploadAsync(bytes, metadata)
  → Save to files/inbox/{id}/original.*
  → Insert ReceiptImportItem (Status=Queued)
  → Queue worker picks up
  → Preprocess → OCR → AI → Match
  → Status=Ready, raise ItemUpdated event
  → WPF inbox refreshes, toast "Receipt Received"
  → User reviews → CommitMatch / CommitCreate / Ignore
  → AttachmentService.AddFromBytesAsync + update Transaction (user confirms)
```

---

## Future Extensions (No Rewrite Required)

| Feature | Extension point |
|---------|-----------------|
| Cloud sync | `IReceiptImportSource` + sync worker; inbox items get `ExternalId` |
| Email forwarding | `EmailReceiptImportSource` polls IMAP, drops `.eml` attachments into pipeline |
| Supplier learning | `ISupplierProfileRepository` + AI parser reads profiles |
| Open Banking | Match service adds `IBankTransactionProvider` beyond imported ANZ rows |
| Multi-club | Upload token scoped to club ID; separate AppData roots |

---

## Implementation Phases

| Phase | Deliverable | Priority |
|-------|-------------|----------|
| **1** | Core models/interfaces, architecture doc, stub services | ✅ Foundation |
| **2** | Mobile Upload host + PWA skeleton + QR dialog | Highest |
| **3** | Processing queue worker + inbox UI | High |
| **4** | Windows OCR + rule-based AI parser | High |
| **5** | Transaction matching + commit flow | High |
| **6** | Image preprocessing pipeline | Medium |
| **7** | Desktop drag/drop/ZIP/paste inbox import | Medium |
| **8** | PWA offline queue + camera UX polish | Medium |
| **9** | TWAIN/WIA scanner | Lower |
| **10** | Thumbnails, PDF preview, attachment replace | Lower |

---

## Key Interfaces (Core)

| Interface | Role |
|-----------|------|
| `IReceiptImportService` | Inbox CRUD, desktop import, commit actions |
| `IReceiptImportQueue` | Enqueue, status updates, events |
| `IMobileUploadHost` | Start/stop server, create session, QR URL |
| `IReceiptImageProcessor` | Edge detect, crop, deskew, contrast |
| `IOcrService` | Text extraction with confidence |
| `IReceiptAiParser` | Category/field suggestions |
| `IReceiptMatchingService` | ANZ transaction match suggestions |
| `IScannerService` | TWAIN/WIA capture |
| `IReceiptImportBatchService` | Batch undo (desktop/scanner sessions) |

---

## Technology Choices

| Concern | Choice | Rationale |
|---------|--------|-----------|
| Mobile server | ASP.NET Core minimal hosting inside WPF | Same DI, no separate process |
| PWA | Vanilla HTML/JS + service worker | Lightweight, works iOS/Android |
| QR | `QRCoder` NuGet | Generate in WPF |
| Image processing | OpenCvSharp4 | Edge detect, perspective warp |
| OCR | Windows.Media.Ocr | Built-in, offline |
| Database | LiteDB (existing) | Local-first, no migration server |
| LAN discovery | Manual IP in QR (mDNS optional later) | Simple, reliable |

---

## Related Files

- Core models: `ReceiptImportItem`, extended `Attachment`
- Core interfaces: `IReceiptImportServices.cs`
- DTOs: `ReceiptImportDtos.cs`
- Infrastructure: `ReceiptImportService`, `MobileUploadHost`, `ReceiptImportQueueWorker`
