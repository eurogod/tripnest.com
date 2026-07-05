// Global usings shared by every test file. xUnit isn't part of the SDK's
// implicit usings, so without this every [Fact]/Assert reference fails to compile.
global using Xunit;
global using System.Net.Http.Json;

// Every test class boots a full in-process API host (WebApplicationFactory) including the real
// hosted background services. Under xUnit's default parallelism those concurrent host startups
// contend and intermittently fail at CreateHost, causing flaky (false-negative) CI runs — so the
// suite runs test classes serially. See must_fix.md "Tester's note".
[assembly: CollectionBehavior(DisableTestParallelization = true)]
