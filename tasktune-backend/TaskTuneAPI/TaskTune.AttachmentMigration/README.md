# Legacy attachment migration utility

This standalone operator tool migrates legacy `TaskAttachments.FilePath = /Uploads/{fileName}` rows to the active API's private storage-key format:

`tasks/{taskId}/legacy_{attachmentId}_{guid}.{extension}`

It is not part of API request handling and never deletes legacy source files.

## Configure

Set these environment variables in the operator shell:

- `TASKTUNE_SQLSERVER_CONNECTION`: connection string for the target TaskTune database.
- `TASKTUNE_LEGACY_UPLOAD_ROOT`: physical directory mapped to the old `/Uploads/` URL path.
- `TASKTUNE_PRIVATE_UPLOAD_ROOT`: active API private root, normally `<API ContentRoot>/App_Data/tasktune-private/uploads`.

Do not put credentials in source control or command history. Use a least-privileged migration identity that can read attachment rows and update `FilePath`/`FileSize`.

## Dry run (default)

```powershell
dotnet run --project tasktune-backend/TaskTuneAPI/TaskTune.AttachmentMigration/TaskTune.AttachmentMigration.csproj
```

The dry run inventories eligible records, validates each path and physical file, compares the stored file size, and calculates SHA-256. No file copies or database changes occur.

## Apply

Back up the database and both upload roots. Keep the append-only JSONL manifest outside both storage roots in an access-controlled location.

```powershell
dotnet run --project tasktune-backend/TaskTuneAPI/TaskTune.AttachmentMigration/TaskTune.AttachmentMigration.csproj -- --apply --manifest C:\secure-ops\tasktune-attachments.jsonl
```

For each attachment, the tool writes a `prepared` manifest event, copies to a unique private path, verifies the destination SHA-256, and updates the row only if the database still contains the inventoried legacy path. It then appends a `committed` event. A SQL Server application lock prevents concurrent invocations of the migration tool.

## Roll back metadata

```powershell
dotnet run --project tasktune-backend/TaskTuneAPI/TaskTune.AttachmentMigration/TaskTune.AttachmentMigration.csproj -- --rollback --manifest C:\secure-ops\tasktune-attachments.jsonl
```

Rollback verifies the private copy and restores the old database path only while the row still points to this migration's new private key. It does not delete either copy. A `prepared` event is sufficient for crash recovery because rollback first checks the current database path.

## Operational boundary

- Only flat legacy `/Uploads/{fileName}` paths and the legacy extension allowlist are accepted.
- Path traversal, nested names, unsupported extensions and symbolic-link/reparse-point components are rejected.
- Size mismatches, missing files, checksum failures and concurrent metadata changes fail closed for the affected row.
- The tool does not remove old or unreferenced files. Review the manifest and app behavior before any later cleanup.
- The utility has only been added to source and CI; it has not been run against a real deployment storage root or database.
