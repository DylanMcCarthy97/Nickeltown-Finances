# Club internet sync (home ↔ club)

Nickeltown Finance stores books in a local LiteDB database. To keep **the same logins and the same data** on a home PC and a club PC **without USB sticks or OneDrive**, both apps talk to a small **sync server** over the internet.

## How it feels day-to-day

1. Someone hosts the sync server once (Docker compose below).
2. On **both** computers open **Settings → Club sync** and enter the same:
   - Sync server URL  
   - Club id (e.g. `nickeltown-flounderers`)  
   - Sync code (a shared secret, 8+ characters)
3. Press **Sync now**.

That is the whole routine. **Sync now** downloads when the other computer has newer books, and uploads otherwise. It also turns **Sync this computer** on, so:

- Opening the app offers to download when the other computer is newer.
- Closing the app uploads this computer.

If both computers changed the books before either one synced, the app asks which copy to keep.

Packages are **encrypted with the sync code** before upload. The server stores an opaque blob; it cannot read club finances without the code.

Books, receipts, and backups are stored on each PC under `%LOCALAPPDATA%\NickeltownFinance`, not in Documents. Documents is often OneDrive, and OneDrive was denying access to the backups folder (the app would not start) and to receipt preview folders (upload failed).

## Run the sync server

From the repo root:

```bash
docker compose -f docker-compose.sync.yml up -d
```

Local URL: `http://localhost:5088`  
Point both PCs at a **public HTTPS URL** that reaches that container (reverse proxy, cloud VM, etc.).

Or run without Docker:

```bash
dotnet run --project src/NickeltownFinance.SyncServer
```

## API (for operators)

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/health` | Liveness |
| GET | `/v1/clubs/{clubId}/status` | Manifest (needs sync code header) |
| GET | `/v1/clubs/{clubId}/package` | Download encrypted package |
| PUT | `/v1/clubs/{clubId}/package` | Upload package (creates club on first push) |

Header: `X-Club-Sync-Code: <code>`

## Why not OneDrive / USB?

- Putting the live books, receipts, or backups in OneDrive/Google Drive risks **corruption**, and those folders often **deny access** while the cloud client is syncing. The app will not use them.
- USB sticks get **lost** and are easy to forget.
- A sync server keeps one encrypted copy online that both PCs can reach when they have internet.
