# Changelog

## June 2026 — Stratum JSON parsing optimization

Swapped Newtonsoft.Json deserialization in the stratum request pipeline to `System.Text.Json.Utf8JsonReader`, reading directly from `ReadOnlySequence<byte>` off the pipe without intermediate string allocations. Also switched `SendMessage` to use `RecyclableMemoryStream` instead of `StringBuilder` → `string` → `byte[]`. Added `TieredPGO`, `OptimizationPreference=Speed`, `InvariantGlobalization` compiler flags, and `[MethodImpl(AggressiveOptimization|AggressiveInlining)]` on the four hottest methods.

**Results**: 2,288 ns → 550 ns (4.16× faster), 7,110 B → 752 B allocated (9.4× less). Cumulative vs upstream .NET 6: **5.36× faster, 9.4× less memory.**

See: [Optimization: Stratum JSON Parsing](optimizations/02-stratum-utf8reader.md), [Benchmark: Stratum Processing](benchmarks/stratum-comparison.md)

## May 2026 — Benchmark framework + .NET 6 comparison

Added BenchmarkDotNet-based stratum benchmarks, established baseline comparison between upstream .NET 6 and MCCE .NET 8. The runtime upgrade alone yielded **22% improvement** from JIT/Dynamic PGO improvements in .NET 8. Added CI workflow running benchmarks in Release configuration on every push.

## May 2026 — GitHub Actions CI

Added CI workflow for build + test automation on Ubuntu 24.04 with .NET 8. Fixed C++ native library compilation failures with GCC 13 on Linux.

## April 2026 — REST API: MVC → Minimal APIs

Migrated all REST API endpoints from ASP.NET Core MVC Controllers to .NET 8 Minimal APIs. Deleted `PoolApiController.cs`, `AdminApiController.cs`, `ClusterApiController.cs`, `ApiControllerBase.cs`. Consolidated everything into `ApiEndpoints.cs` using `IEndpointRouteBuilder` with native parameter binding (`[FromQuery]`, `[FromServices]`, `[FromBody]`).

**Results**: API throughput went from ~36 RPS (MVC, sequential) to ~2,478 RPS — a **67× improvement**.

See: [Optimization: REST API Migration](optimizations/01-api-minimal-migration.md), [Benchmark: API Throughput](benchmarks/api-comparison.md)

## March 2026 — C# 12 modernization pass

Upgraded codebase to C# 12 idioms: replaced mutable dictionaries with `FrozenDictionary`/`FrozenSet`, used collection expressions (`[]` syntax), primary constructors where appropriate. Removed leftover relic code that referenced deleted subsystems.

## February 2026 — Branding & .NET 8 baseline

Forked from `blackmennewstyle/miningcore`. Rebranded as Miningcore Community Edition (MCCE). Bumped target framework to `net8.0`, upgraded all NuGet dependencies, updated build scripts for Ubuntu 24.04. Fixed nullability warnings, cleaned up console output formatting.

## 2022 — Original Miningcore archived

Oliverw Miningcore was archived and discontinued. No further updates from upstream.
