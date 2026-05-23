// Sentirum Agent SDK — Tool Calling sample.
//
// Demonstrates [Tool]-decorated methods registered via WithTools<T>(),
// driving a Z.AI (glm-4.6) or OpenAI customer-support agent in Turkish.
//
// Run with:
//   ZAI_API_KEY=...    dotnet run --project samples/03-ToolCalling
//   OPENAI_API_KEY=... dotnet run --project samples/03-ToolCalling

using System.ComponentModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sentirum.Agent;
using Sentirum.Agent.Tools;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<OrderTools>();

var zaiKey = Environment.GetEnvironmentVariable("ZAI_API_KEY");
var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

if (!string.IsNullOrWhiteSpace(zaiKey))
{
    builder.Services.AddSentirumAgent("support", b => b
        .UseZAI("glm-4.6", apiKey: zaiKey)
        .WithTools<OrderTools>()
        .WithInstructions("""
            Sen Sentirum'un Türkçe konuşan müşteri destek asistanısın.
            Sipariş durumu sorulduğunda mutlaka GetOrderStatus aracını çağır.
            Aracın döndürdüğü bilgileri kibar ve kısa şekilde aktar.
            """));
}
else if (!string.IsNullOrWhiteSpace(openAiKey))
{
    builder.Services.AddSentirumAgent("support", b => b
        .UseOpenAI("gpt-4o-mini", apiKey: openAiKey)
        .WithTools<OrderTools>()
        .WithInstructions("Sen Sentirum müşteri destek asistanısın. Sipariş durumu sorulduğunda GetOrderStatus aracını kullan."));
}
else
{
    Console.WriteLine("Set ZAI_API_KEY or OPENAI_API_KEY to run this sample.");
    return;
}

using var host = builder.Build();
var agent = host.Services.GetRequiredService<ISentirumAgentRegistry>().Find("support")!;
var session = await host.Services.GetRequiredService<ISentirumSessionStore>().CreateAsync(agent.Id);

const string question = "Merhaba! ORD-42 numaralı siparişimin durumu nedir?";

Console.WriteLine($"> {question}");
Console.WriteLine();

// Use RunAsync (non-streaming) so we get the final response after tool
// invocation completes. Streaming + tool calls returns chunks from both the
// pre-tool and post-tool turns; non-streaming gives us the aggregate.
var response = await agent.RunAsync(session, new ChatMessage(ChatRole.User, question));
Console.WriteLine(response.Text);

internal sealed class OrderTools
{
#pragma warning disable CA1822 // Instance method so it can take DI dependencies in real toolsets.
    [Tool(Description = "Look up the shipment status of a Sentirum customer order. Returns a short human-readable summary.")]
    public Task<string> GetOrderStatusAsync(
        [Description("Order id in the format 'ORD-<digits>', e.g. ORD-42")] string orderId,
        CancellationToken cancellationToken = default)
    {
        // Pretend we hit a database.
        var summary = orderId switch
        {
            "ORD-42" => "ORD-42: 10 Mayıs 2026'da kargoya verildi, 12 Mayıs'ta teslim edildi. Yurtiçi Kargo, takip no 4242-4242.",
            "ORD-7" => "ORD-7: hâlâ hazırlanıyor, tahmini sevk tarihi yarın.",
            _ => $"{orderId}: bu sipariş bulunamadı.",
        };

        return Task.FromResult(summary);
    }
#pragma warning restore CA1822
}
