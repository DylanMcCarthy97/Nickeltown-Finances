# Club internet sync (home ↔ club)

Nickeltown Finance stores books in a local LiteDB database. To keep **the same logins and the same data** on a home PC and a club PC **without USB sticks or OneDrive**, both apps talk to a small **sync server** over the internet.

## How it feels day-to-day

1. Someone hosts the sync server once (Docker compose below).
2. On **both** computers open **Settings → Club sync** and enter the same:
   - Sync server URL  
   - Club id (e.g. `nickeltown-flounderers`)  
   - Sync code (a shared secret, 8+ characters)
3. Turn **Enable internet sync** on and tap **Save**.
4. On the computer you just used: **Upload** (or simply close the app — it uploads on shutdown).
5. On the other computer: open the app — it offers to **Download** if newer data is online, or use **Download** in Settings.

Use **one computer at a time**. Upload before you leave; download when you arrive.

Packages are **encrypted with the sync code** before upload. The server stores an opaque blob; it cannot read club finances without the code.

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

- Putting the live LiteDB file in OneDrive/Google Drive risks **corruption**.
- USB sticks get **lost** and are easy to forget.
- A sync server keeps one encrypted copy online that both PCs can reach when they have internet.
