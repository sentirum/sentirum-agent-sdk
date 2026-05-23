// Sentirum Agent SDK — Hello Agent sample.
//
// This sample is intentionally minimal at M0. It will grow as we land
// Sentirum.Agent.Core (M1) and the first provider (also M1). For now it
// simply prints the SDK abstractions surface so we can verify the build.

using Sentirum.Agent;

Console.WriteLine("Sentirum Agent SDK — Hello Agent");
Console.WriteLine($"Abstractions assembly loaded: {typeof(ISentirumAgent).Assembly.GetName().Name}");
Console.WriteLine("Core runtime arrives in M1. Stay tuned.");
