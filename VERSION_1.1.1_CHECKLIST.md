# Nickeltown Finances v1.1.1 — Document System

## Root causes (confirmed)

### 1. STA — Completed then Failed (same run)

| | |
|---|---|
| **Primary cause** | Pipeline finishes (`Ready` + timeline `Completed`), then `RaiseUpdated` fires `ItemUpdated` subscribers on the **MTA queue worker**. `ReceiptImportNotificationBridge` calls WPF `ISnackbarService.Show` without marshaling → STA exception → outer `catch` in `ProcessItemAsync` sets `Failed`. |
| **File** | `ReceiptImportNotificationBridge.cs` → `NotificationService.cs` |
| **Secondary** | `WindowsMediaOcrService.IsAvailable` accessed `OcrEngine.TryCreateFromUserProfileLanguages()` on MTA thread during OCR stage. |
| **Fix** | `SafeRaiseUpdated` (subscriber exceptions never downgrade status); notification bridge uses `Dispatcher.BeginInvoke`; `IsAvailable` checked on STA thread; post-success exceptions logged as `PostProcessing` only. |

### 2. Poor preview quality (B&W, grainy)

| | |
|---|---|
| **Cause** | Single `processed.jpg` used for both UI and OCR. `NormalizeBackground` converted to grayscale via divide/blur; heavy denoise applied. |
| **Fix** | Split pipeline: **`preview.jpg`** (colour, deskew, CLAHE, mild sharpen) for all UI; **`ocr.jpg`** (grayscale + adaptive threshold) internal only. |

### 3. PDF “Open PDF” only

| | |
|---|---|
| **Cause** | PDFs had no rasterized pages; UI fell back to PDF icon when `PreviewFullPaths` empty. |
| **Fix** | `Docnet.Core` (PDFium) renders `page1.jpg`, `page2.jpg`, …; viewer and inbox use page JPEGs. Re-process existing inbox PDFs via Retry. |

## Image outputs (per receipt)

| File | Purpose |
|------|---------|
| `original{ext}` | Unmodified upload — always retained |
| `preview.jpg` | Colour-enhanced — **default everywhere in UI** |
| `ocr.jpg` | OCR-only preprocessing — never shown by default |
| `thumbnail.jpg` | Generated once from `preview.jpg` |
| `pageN.jpg` | PDF page previews |

## OCR

Runs on original, preview, and ocr images (plus PDF page 1). Picks best result by confidence score via `ExtractBestAsync`.

## Build

```
dotnet build NickeltownFinance.slnx
0 Errors, 0 Warnings
```

## Manual verification

See checklist items in original v1.1.1 spec. **Important:** existing receipts processed before this refactor still have old `processed.jpg` — use **Retry Processing** to regenerate `preview.jpg` / `ocr.jpg`.

## Libraries

- `Docnet.Core` 2.6.0 (PDFium) — Infrastructure
