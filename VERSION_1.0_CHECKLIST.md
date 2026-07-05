# Nickeltown Finance — Version 1.0 Checklist

Last updated: July 2026  
Recommended release: **1.0.0**

Status key: **Complete** | **Needs Testing** | **Known Issue** | **Future Enhancement**

---

## Core bookkeeping

| Feature | Status | Notes |
|---------|--------|-------|
| Dashboard overview | Complete | |
| Transaction ledger (income/expense) | Complete | |
| Transaction editor | Complete | Validates description, category, amount, date |
| Financial year auto-detection | Complete | Created from transaction/import dates |
| Financial year management | Complete | Lock, archive, opening balance |
| Category create / rename / merge / archive | Complete | Duplicate names prevented |
| Last category per supplier | Complete | Via supplier profile defaults |

---

## Bank import (ANZ)

| Feature | Status | Notes |
|---------|--------|-------|
| CSV import | Complete | |
| Excel import | Complete | |
| Duplicate file detection | Complete | |
| Duplicate transaction detection | Complete | |
| Import preview & categorisation | Complete | |
| Import history & undo | Complete | |
| Import statistics (new/duplicate/skipped) | Complete | |
| Drag & drop on Import hub | Complete | CSV/Excel → ANZ; images → Receipt Inbox |

---

## Receipt workflow

| Feature | Status | Notes |
|---------|--------|-------|
| Receipt Inbox | Complete | |
| Desktop / folder import | Complete | |
| Mobile QR upload | Complete | Same Wi‑Fi |
| OCR (Windows) | Complete | Requires Windows 10 19041+ |
| Image enhancement (OpenCV) | Complete | |
| Supplier / date / amount extraction | Complete | |
| ANZ match tiers (Excellent / Likely / Possible) | Complete | One-click attach ≥97% |
| Receipt → transaction attach | Complete | |
| Create expense from receipt | Complete | |
| Receipt viewer (zoom, rotate, fit, nav) | Complete | Double-click from Transactions / Inbox |
| Drag & drop onto transactions | Complete | |
| Duplicate receipt detection | Complete | |

---

## Search & navigation

| Feature | Status | Notes |
|---------|--------|-------|
| Transaction search (supplier, amount, OCR, ABN, invoice) | Complete | |
| Receipt Inbox search | Complete | |
| Sidebar navigation | Complete | |
| Global financial year selector | Complete | |
| Keyboard: Ctrl+Z undo delete (Transactions) | Complete | |
| Keyboard: F5 refresh, Ctrl+E export | Complete | Transactions page |

---

## Reports

| Feature | Status | Notes |
|---------|--------|-------|
| Monthly report | Complete | PDF + Excel |
| AGM / annual income & expense report | Complete | PDF + Excel |
| Category summary (Excel) | Complete | Quick export from AGM tab |
| GST summary (Excel) | Complete | From OCR receipt data |
| Receipt audit (Excel) | Complete | Missing receipt flagging |
| Report signature (Settings) | Complete | |

---

## Settings & administration

| Feature | Status | Notes |
|---------|--------|-------|
| Club name & logo | Complete | |
| User management | Complete | Admin only |
| Receipt processing toggles | Complete | OCR, matching, duplicates, thumbnails |
| Theme (light / dark / system) | Complete | |
| Backup folder | Complete | Auto-creates if missing |
| Manual backup | Complete | |
| Restore backup | Complete | Validates zip/db; closes DB before restore |
| Automatic shutdown backup | Complete | Skipped after restore restart |
| Financial year start month | Complete | |

---

## Reliability & quality (v1.0 stabilisation)

| Item | Status | Notes |
|------|--------|-------|
| Global UI exception handler | Complete | Friendly dialog + Serilog |
| Unobserved task exception logging | Complete | |
| LiteDB thread-safe access | Complete | Lock on repository operations |
| Backup corrupt-file detection | Complete | Empty zip / invalid LiteDB |
| Restore without file-lock failure | Complete | DatabaseShutdownService |
| Dead supplier-intelligence code removed | Complete | Profiles retained for categorisation |
| Version display | Complete | v1.0.0 in main window |

---

## Known issues

| Issue | Status | Notes |
|-------|--------|-------|
| Category drag-and-drop assignment | Future Enhancement | Not in v1.0 scope |
| PDF export for category/GST/audit quick reports | Future Enhancement | Excel only |
| Broad undo (beyond delete) | Future Enhancement | |
| Scanner / TWAIN capture | Future Enhancement | Stub service only |
| Supplier purchase history UI | Future Enhancement | DB collections reserved |
| Automated test suite | Needs Testing | Manual QA recommended before release |

---

## Pre-release manual test plan

- [ ] First-run setup wizard → login → dashboard
- [ ] Import ANZ CSV with duplicates → verify counts
- [ ] Drop receipt PDF on Import hub → inbox → match → attach
- [ ] Edit transaction, replace receipt, open viewer
- [ ] Create duplicate category name → verify error
- [ ] Generate monthly + AGM reports → open PDF and Excel
- [ ] Manual backup → restore → verify data intact after restart
- [ ] Change theme and receipt settings → restart → verify persisted
- [ ] Sign out → sign in as different user

---

## Recommended version number

**1.0.0** — first stable release for everyday club bookkeeping.
