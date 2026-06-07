# Implementation Plan & Walkthrough: REST API Modernization

## What was Changed

1. **Removed MVC Controller Overhead**:
   - Deleted the legacy controllers: `AdminApiController.cs`, `ClusterApiController.cs`, `PoolApiController.cs`, and `ApiControllerBase.cs`.
   - Removed MVC routing registrations (`services.AddMvc()` and `app.UseMvc()`) from `Program.cs`.

2. **Consolidated into Minimal APIs**:
   - Created a modern routing configuration class: [ApiEndpoints.cs](file:///g:/github/tipu/abasada/src/Miningcore/Api/ApiEndpoints.cs) using `IEndpointRouteBuilder`.
   - Configured Endpoint routing and JSON serialisation in `Program.cs` via `services.AddRouting()`, `services.ConfigureHttpJsonOptions()`, and `app.UseEndpoints()`.
   - Enabled OpenAPI generation via `services.AddEndpointsApiExplorer()`.

3. **Optimized Route Bindings**:
   - Used ASP.NET Core native parameter binding (`[FromQuery]`, `[FromServices]`, `[FromBody]`, `HttpContext`) on route lambdas.
   - Fixed C# compiler rules (CS1737) by placing optional parameters (`page = 0`, `pageSize = 15`, etc.) at the end of parameter lists.
   - Handled potential `NullReferenceException` in `PayoutSchemeConfig` bindings by checking for `null` in `PaymentProcessing`.

## Why We Did This

- **Performance**: ASP.NET Core MVC controllers introduce massive overhead through reflection, routing pipelines, action filters, and model binding. Minimal APIs map routes directly to compiled delegates, removing the MVC pipeline and significantly reducing memory allocations per request.
- **Developer Simplicity**: Eliminating the controller layer flattens the API structure, making it easier to read and maintain. Endpoints resolve their dependencies directly in the method signature, reducing boilerplate code.
- **Modernization**: Moving to Minimal APIs aligns Miningcore with modern .NET 8 best practices for high-performance REST microservices.
