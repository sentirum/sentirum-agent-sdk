// Sentirum Agent SDK — RAG / Knowledge Base sample.
//
// Demonstrates WithKnowledgeBase(...) injecting the top-k matching
// snippets from a tiny FAQ-style knowledge base on every turn. The
// in-process InMemoryKnowledgeBase is good enough for samples; production
// callers plug a vector store / search service into IKnowledgeBase.
//
// Run with:
//   ZAI_API_KEY=... dotnet run --project samples/06-Rag

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sentirum.Agent;
using Sentirum.Agent.Context;

var zaiKey = Environment.GetEnvironmentVariable("ZAI_API_KEY");
if (string.IsNullOrWhiteSpace(zaiKey))
{
    Console.Error.WriteLine("Set ZAI_API_KEY to run this sample.");
    return 1;
}

var faq = new InMemoryKnowledgeBase(new[]
{
    new KnowledgeBaseSnippet(
        "İade politikası",
        "Sentirum'da satın aldığın ürünleri 30 gün içinde iade edebilirsin. " +
        "İade onayı sonrası tutar 5-7 iş günü içinde aynı ödeme yöntemine geri yatar.",
        Score: 0,
        SourceUrl: "https://sentirum.example/help/refunds"),

    new KnowledgeBaseSnippet(
        "Kargo süreleri",
        "Standart kargo İstanbul içi 1-2 iş günü, yurt içi 3-5 iş günü, " +
        "yurt dışı 7-14 iş günüdür. Express kargo bir gün daha hızlıdır.",
        Score: 0,
        SourceUrl: "https://sentirum.example/help/shipping"),

    new KnowledgeBaseSnippet(
        "Pro plan",
        "Sentirum Pro plan aylık 199 TL'dir, sınırsız agent ve 10K mesaj/ay içerir. " +
        "Yıllık ödeme %20 indirimlidir.",
        Score: 0,
        SourceUrl: "https://sentirum.example/pricing"),

    new KnowledgeBaseSnippet(
        "Garanti",
        "Sentirum elektronik ürünlerinde 2 yıl garanti standarttır. " +
        "Yazılım abonelikleri için memnuniyetsizlik durumunda ilk 14 gün koşulsuz iade hakkın var.",
        Score: 0,
        SourceUrl: "https://sentirum.example/help/warranty"),
});

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<IKnowledgeBase>(faq);

builder.Services.AddSentirumAgent("kb", b => b
    .UseZAI("glm-4.6", apiKey: zaiKey)
    .WithInstructions("""
        Sen Sentirum'un Türkçe konuşan bilgi tabanı asistanısın.
        Sana sağlanan "Relevant knowledge-base entries" başlığındaki bilgileri
        kullanarak cevap ver. Cevabın sonunda kullandığın kaynakların adlarını
        kısa bir "Kaynaklar" başlığı altında listele.
        """)
    .WithKnowledgeBase<IKnowledgeBase>(maxResults: 2,
        heading: "Relevant knowledge-base entries (kullan ve kaynak göster):"));

using var host = builder.Build();
var registry = host.Services.GetRequiredService<ISentirumAgentRegistry>();
var store = host.Services.GetRequiredService<ISentirumSessionStore>();
var agent = registry.Find("kb")!;
var session = await store.CreateAsync(agent.Id);

foreach (var question in new[]
{
    "Sentirum'da ürün iadesi nasıl yapılır, kaç günde param geri gelir?",
    "İstanbul'a kaç günde kargo geliyor?",
})
{
    Console.WriteLine($"> {question}");
    var response = await agent.RunAsync(session, new ChatMessage(ChatRole.User, question));
    Console.WriteLine(response.Text);
    Console.WriteLine();
}

return 0;
