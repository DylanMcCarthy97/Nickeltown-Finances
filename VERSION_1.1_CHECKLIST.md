# Version 1.1 Checklist

Manual verification checklist for the receipt upload and processing polish release.

## Transaction-targeted mobile upload

- [ ] Mobile upload from an **existing saved transaction** does not appear in Receipt Inbox
- [ ] Mobile upload from a **new unsaved transaction** does not appear in Receipt Inbox
- [ ] Saving a new transaction attaches pending mobile receipt(s) automatically
- [ ] Cancelling a new transaction with pending uploads asks **Keep in inbox** / **Discard** (default: keep)
- [ ] Choosing **Keep in inbox** promotes the receipt to the general Receipt Inbox
- [ ] Choosing **Discard** removes the pending upload

## General inbox uploads

- [ ] Mobile upload from **Imports → Mobile Receipt Upload** appears in Receipt Inbox
- [ ] Desktop browse import appears in Receipt Inbox
- [ ] Drag/drop import appears in Receipt Inbox
- [ ] ZIP/folder import appears in Receipt Inbox

## Processing reliability

- [ ] OCR runs without **"The calling thread must be STA"** error
- [ ] Original receipt file is preserved if processing fails
- [ ] `processed.jpg` is created (enhanced or copied original)
- [ ] Thumbnail is created when possible; missing thumbnail is a warning only
- [ ] Failed OCR on transaction-targeted upload still attaches original to transaction
- [ ] **Retry processing** works from Edit Expense and mobile PWA

## Status behaviour

- [ ] Duplicate detection shows a warning but does **not** mark receipt as Failed
- [ ] Image enhancement fallback shows **Completed with warnings**, not Failed
- [ ] OCR failure on inbox upload shows **Completed with warnings** or **Needs review**, not Failed (unless original lost)
- [ ] True fatal failures (missing original file) show **Failed**
- [ ] Transaction attachment panel shows: Uploaded, Processing, Ready, Attached, Processing failed (original saved), Duplicate warning

## UI

- [ ] Edit Expense toolbar: Mobile, Browse, Scan, Paste, View, Remove
- [ ] Large receipt preview with zoom/rotate/navigation works
- [ ] Mobile PWA shows **Receipt uploaded** immediately after HTTP upload succeeds
- [ ] Mobile PWA shows processing timeline while desktop pipeline runs
- [ ] Mobile PWA never shows **Upload Failed** when upload succeeded but processing failed

## Build

- [ ] Solution builds with **0 errors, 0 warnings**
