# Minimal API Migration Benchmark Results

## Performance Comparison (Before vs After)

We ran a benchmark of 5,000 requests against the `/api/health-check` endpoint (which is safe to run repeatedly without affecting DB stats or pool state).

| Metric | Before (.NET 8 MVC Controllers) | After (.NET 8 Minimal APIs) | Improvement |
| :--- | :--- | :--- | :--- |
| **Total Requests** | 5,000 | 5,000 | - |
| **Total Time** | 10.14 seconds | 2.56 seconds | **~4x faster** (reduced by 7.58s) |
| **Throughput (RPS)** | ~493 req/sec | **~1,956 req/sec** | **+296.7% increase** |
| **Success Rate** | 100% (5000/5000) | 100% (5000/5000) | - |
| **Errors/Failures** | 0 | 0 | - |

## Test Verification of API Endpoints

All mapped REST API endpoints were manually tested using `curl` and verified for correctness:

1. **`GET /api/help`**: Successfully returns all routes bound via `IEndpointRouteBuilder` in a readable text format.
2. **`GET /api/health-check`**: Correctly returns `👍` as plain text.
3. **`GET /api/pools`**: Successfully returns list of configured and enabled pools (such as `btc1` containing full pool info, network stats, and ports) in JSON format.
4. **`GET /api/pools/{poolId}`**: Returns single pool details correctly (tested with `GET /api/pools/btc1`).
5. **`GET /api/blocks`**: Successfully returns list of blocks (returns `[]` under simulation) with all query parameters defaulting correctly.
6. **`GET /api/admin/stats/gc`**: Successfully returns GC memory allocation statistics (e.g. `gcGen0`, `memAllocated`), showing that `[FromServices]` parameter injection works as expected.
