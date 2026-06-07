# Stratum Connection Benchmark - MCCE Old Baseline

This file stores the baseline benchmark result for the Stratum connection request processing in Miningcore Community Edition (MCCE) before any custom optimizations are applied.

## Environment Details
- **OS**: Ubuntu 24.04 (via Docker / act)
- **CPU**: Intel Core i7-12700K (12th Gen), 1 CPU, 20 logical and 10 physical cores
- **.NET SDK**: 8.0.421
- **Runtime**: .NET 8.0.27 (8.0.2726.22922), X64 RyuJIT AVX2
- **Configuration**: Release

## Benchmark Target
- **Class**: `Miningcore.Tests.Benchmarks.Stratum.StratumConnectionBenchmarks`
- **Method**: `ProcessRequest_Handle_Valid_Request`
- **Description**: Simulates parsing and routing a raw incoming TCP JSON-RPC request (`mining.authorize`) from a miner.

## Results (MCCE Old Baseline)

| Method | Mean | Error | StdDev | Gen 0 (per 1k ops) | Allocated Memory |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **ProcessRequest_Handle_Valid_Request** | **2.288 μs** | 0.0453 μs | 0.1137 μs | 0.5569 | **7.11 KB** |

### Histogram
```
[1.984 μs ; 2.068 μs) | @@
[2.068 μs ; 2.134 μs) | @@@
[2.134 μs ; 2.205 μs) | @@@@@@@@@@@@@@
[2.205 μs ; 2.310 μs) | @@@@@@@@@@@@@@@@@@@@@@@@
[2.310 μs ; 2.387 μs) | @@@@@@@@@@@@@@@@@
[2.387 μs ; 2.458 μs) | @@@@@@@@@@
[2.458 μs ; 2.545 μs) | @@@
[2.545 μs ; 2.620 μs) | @
```
