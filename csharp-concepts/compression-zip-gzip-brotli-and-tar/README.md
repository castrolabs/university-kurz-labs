# Compression: ZIP, GZip, Brotli and tar

## Goal

Compress and decompress with the stream codecs, build and read ZIP and tar archives in memory, and extract untrusted archives without letting them write outside a folder or expand without bound.

## Prerequisites

- `Stream` and `MemoryStream`
- `using` and `IDisposable`
- .NET 10 SDK

## Task

`Codecs`, `ZipTools` and `TarTools` are stubbed. Everything runs in memory except the extraction tests, which use a unique temp folder that the tests delete. The tests never assert exact compressed sizes (they vary by runtime), only round trips, "smaller than the input" and behavior.

## Instructions

Complete the following TODOs:

- TODO-00: `Gzip` / `Gunzip`: the compressor only finishes the file when disposed, and `leaveOpen` keeps the `MemoryStream` alive for `ToArray()`.
- TODO-01: `BrotliCompress` / `BrotliDecompress`: same decorator pattern, with a `CompressionLevel` that the codec interprets.
- TODO-02: `CreateZip`: build a ZIP in memory with `ZipArchiveMode.Create`.
- TODO-03: `ReadZip`: read entries back and enforce a total size limit by counting bytes while copying (decompression bomb guard).
- TODO-04: `ExtractSafely`: zip-slip protection with `GetFullPath` plus a trailing separator, validating all entries before writing any.
- TODO-05: `CreateTarGz` / `ReadTarGz`: `System.Formats.Tar` stacked on a `GZipStream`.

Run the tests until they all pass.

## Running the Lab

From the lab directory:

```bash
dotnet test
```

## Bonus (Optional)

- TODO-06 (optional): Truncate a valid gzip stream in half and decompress it with `CopyTo`. Does it throw? What would you carry alongside the data (length, hash) to detect a cut-off download?
- TODO-07 (optional): Reimplement `CreateZip` with the .NET 10 async API (`ZipArchive.CreateAsync`, `OpenAsync`) and compare with `ZipArchiveMode.Update`: delete one entry from an existing archive and add another.
- TODO-08 (optional): Add Zstandard, new in the .NET 11 BCL: implement `ZstdCompress` / `ZstdDecompress` in `Codecs` with `ZstandardStream` (same decorator pattern as GZip and Brotli). Then read the corrupted ZIP test next to the Zstandard tests: it needs your `ReadZip` from TODO-03 and shows that .NET 11 checks the entry CRC32 while reading, so a flipped byte throws `InvalidDataException`; .NET 10 would hand back the damaged bytes. This task needs the .NET 11 SDK: with SDK 10 the lab builds only for `net10.0` and the task does not appear (with SDK 11 run `dotnet test -f net11.0`).
