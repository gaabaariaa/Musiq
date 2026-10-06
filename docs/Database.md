# Musiq Database

Phase 2 uses SQLite as the local library store.

## Principles

- Database access is isolated in Infrastructure.
- WAL mode is enabled for responsive reads/writes.
- Song paths are unique case-insensitively.
- Artist, album and modification indexes support common library queries.
- Scanner writes are idempotent through an upsert keyed by path.
- Existing file size and modification time are checked before metadata work.

## Current schema

- Songs: core song metadata, file information and lyrics.
- ScanLocations: configured library roots.

The schema is created on first startup. A migration framework can be introduced when schema evolution becomes necessary.

## Scanner

The scanner recursively discovers supported audio files, skips unchanged files, upserts changed/new files, and removes database rows for files that disappeared under the scanned root.

Metadata access is behind IAudioFileMetadataReader. The current reader intentionally uses only filename and file-system information; TagLib# integration belongs to the metadata phase.

## File-system monitoring

FileSystemLibraryChangeMonitor wraps FileSystemWatcher and batches rapid changes with a short debounce window. It reports affected audio paths without touching the database or UI.
