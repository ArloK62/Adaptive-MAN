using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Phase 8+: Worker for retention, alert evaluation, ingestion-queue draining.
// Empty for Phase 0/1 — present so the project compiles and the layering is in place.

var host = builder.Build();
await host.RunAsync();
