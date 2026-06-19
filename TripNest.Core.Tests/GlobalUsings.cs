// Global usings shared by every test file. xUnit isn't part of the SDK's
// implicit usings, so without this every [Fact]/Assert reference fails to compile.
global using Xunit;
global using System.Net.Http.Json;
