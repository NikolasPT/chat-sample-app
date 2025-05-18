using System.Text;
using System.Collections.Concurrent;
using System.ComponentModel;
using AngleSharp.Html.Parser;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.Connectors.OpenAI; // ToolCallBehavior enum

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Plugins.Web;
using Microsoft.SemanticKernel.Plugins.Web.Google;
using Microsoft.SemanticKernel.Text;

namespace SemanticKernelSamples;

/// <summary>
/// Demonstrates an “Agentic RAG” pattern that can:
///   1. Check the current date/time.
///   2. Search the web via Google programmable search.
///   3. Ingest the returned pages into an in‑memory vector store.
///   4. Retrieve the ingested knowledge to answer user questions.
/// Includes robust error logging so you can see service‑side problems.
/// </summary>
internal static class Sample07
{
    public static async Task<bool> RunAsync(IConfiguration config)
    {
        // ────────── Azure OpenAI configuration ──────────
        string deploymentName      = config["AzureAIFoundry:DeploymentName"]!;
        string endpoint            = config["AzureAIFoundry:GPT41:Endpoint"]!;
        string apiKey              = config["AzureAIFoundry:GPT41:APIKey"]!;

        string embeddingDeployment = config["AzureAIFoundry:EmbeddingDeploymentName"]!;
        string embeddingEndpoint   = config["AzureAIFoundry:TextEmbedding3Large:Endpoint"]!;
        string embeddingApiKey     = config["AzureAIFoundry:TextEmbedding3Large:APIKey"]!;

        // ────────── Google Search configuration ──────────
        string searchEngineId      = config["Google:SearchEngineId"]!;      // CX id
        string searchConsoleAPIKey = config["Google:SearchConsoleAPIKey"]!; // API key

        const string MemoryCollection = "DynamicRAGMemory";

        // ────────── Build kernel & services ──────────
        var builder = Kernel.CreateBuilder();
        builder.AddAzureOpenAIChatCompletion(deploymentName, endpoint, apiKey);

        // Invocation logging so we can watch tool calls (defined elsewhere in the project)
        builder.Services.AddSingleton<IFunctionInvocationFilter, InvocationLogger>();

        Kernel kernel = builder.Build();
        IChatCompletionService chatService = kernel.GetRequiredService<IChatCompletionService>();

        // Text‑embedding generation service + in‑memory store
        var embeddingService = new AzureOpenAITextEmbeddingGenerationService(
            embeddingDeployment, embeddingEndpoint, embeddingApiKey);
        var memoryStore = new VolatileMemoryStore();
        ISemanticTextMemory memory = new MemoryBuilder()
            .WithMemoryStore(memoryStore)
            .WithTextEmbeddingGeneration(embeddingService)
            .Build();

        // ────────── Date / time helper plugin ──────────
        kernel.Plugins.AddFromFunctions("DateTimeHelpers", "Provides date and time information", new[]
        {
            KernelFunctionFactory.CreateFromMethod(
                method:       () => DateTime.UtcNow.ToString("r"),
                functionName: "Now",
                description:  "Gets the current UTC date and time in RFC1123 format")
        });

        // ────────── Google web‑search plugin ──────────
        GoogleConnector googleConnector = new(searchConsoleAPIKey, searchEngineId);
        WebSearchEnginePlugin searchPlugin = new(googleConnector);
        kernel.ImportPluginFromObject(searchPlugin, "SearchPlugin");

        // ────────── Web ingestion + RAG plugins ──────────
        kernel.Plugins.AddFromObject(new WebIngestionPlugin(memory, MemoryCollection), "WebIngestionPlugin");
        kernel.Plugins.AddFromObject(new RAGPlugin(memory, MemoryCollection), "RAGPlugin");

        // ────────── System prompt ──────────
        ChatHistory chatHistory = new(systemMessage:
            "You are an AI assistant. Today's date and time is {{ DateTimeHelpers.Now }}. " +
            "When a question needs up‑to‑date info, follow these steps: 1) SearchPlugin.GetSearchResultsAsync (count 3). 2) Ingest URLs via WebIngestionPlugin.IngestAndIndexAsync. 3) Retrieve context with RAGPlugin.LookupAsync. " +
            "Always narrate the steps ('Searching…', 'Ingesting…', 'Looking up…') and cite source URLs.");

        // Enable automatic tool invocation
        var settings = new AzureOpenAIPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
        };

        // ────────── Quick sanity check (optional) ──────────
        try
        {
            var sanity = new ChatHistory("You are a helpful assistant.");
            sanity.AddUserMessage("Say hello");
            var hello = await chatService.GetChatMessageContentAsync(sanity, kernel: kernel);
            Console.WriteLine($"\n[Sanity‑check] Model replied: {hello.Content}\n");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[Startup ERROR] {ex.Message}\n");
            Console.ResetColor();
            return false;
        }

        // ────────── Interactive chat loop ──────────
        Console.WriteLine("Agentic RAG chat (type 'exit' to quit):\n");
        while (true)
        {
            Console.Write("Me: ");
            string question = Console.ReadLine() ?? string.Empty;
            if (question.Equals("exit", StringComparison.OrdinalIgnoreCase)) return true;

            chatHistory.AddUserMessage(question);
            Console.Write("AI: ");
            var buffer = new StringBuilder();
            bool received = false;

            try
            {
                await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(chatHistory, settings, kernel))
                {
                    if (chunk.Role == AuthorRole.Assistant && !string.IsNullOrEmpty(chunk.Content))
                    {
                        Console.Write(chunk.Content);
                        buffer.Append(chunk.Content);
                        received = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] {ex.Message}");
                Console.ResetColor();
            }

            if (!received)
            {
                try
                {
                    var reply = await chatService.GetChatMessageContentAsync(chatHistory, kernel: kernel);
                    if (!string.IsNullOrEmpty(reply.Content))
                    {
                        Console.Write(reply.Content);
                        buffer.Append(reply.Content);
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[FALLBACK ERROR] {ex.Message}");
                    Console.ResetColor();
                }
            }

            if (buffer.Length > 0)
            {
                chatHistory.AddAssistantMessage(buffer.ToString());
            }
            Console.WriteLine();
            Console.WriteLine();
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Plugins
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Downloads web pages, extracts readable text and stores paragraphs in semantic memory.</summary>
public sealed class WebIngestionPlugin
{
    private readonly ISemanticTextMemory _memory;
    private readonly string _collection;
    private readonly HttpClient _http = new();
    private readonly HtmlParser _parser = new();
    private static int _paragraphIdCounter;

    public WebIngestionPlugin(ISemanticTextMemory memory, string collection)
    {
        _memory = memory;
        _collection = collection;
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
    }

    [KernelFunction, Description("Fetches the URLs, chunks their text and stores it in the vector store.")]
    public async Task<string> IngestAndIndexAsync(
        [Description("List of URLs to ingest")] List<string> urls,
        Kernel kernel)
    {
        if (urls is null || urls.Count == 0) return "No URLs provided.";

        var paragraphs = new ConcurrentBag<string>();
        var success    = new ConcurrentBag<string>();
        var failed     = new ConcurrentBag<string>();

        await Task.WhenAll(urls.Select(async url =>
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                string html = await _http.GetStringAsync(url, cts.Token);
                var doc = await _parser.ParseDocumentAsync(html, cts.Token);
                var main = doc.QuerySelector("article") ?? doc.QuerySelector("main") ?? doc.QuerySelector("[role='main']") ?? doc.Body;
                if (main is null) { failed.Add(url); return; }

                var lines = TextChunker.SplitPlainTextLines(main.TextContent, 128);
                var chunks = TextChunker.SplitPlainTextParagraphs(lines, 768, 128);
                foreach (var p in chunks)
                {
                    if (!string.IsNullOrWhiteSpace(p)) paragraphs.Add(p.Trim());
                }
                success.Add(url);
            }
            catch (Exception)
            {
                failed.Add(url);
            }
        }));

        foreach (var p in paragraphs)
        {
            await _memory.SaveInformationAsync(_collection, p, $"doc[{Interlocked.Increment(ref _paragraphIdCounter)}]");
        }

        return $"Indexed {paragraphs.Count} paragraphs from {success.Count} URLs. Failed: {failed.Count}.";
    }
}

/// <summary>Retrieval plugin that surfaces relevant paragraphs from the vector store.</summary>
public sealed class RAGPlugin
{
    private readonly ISemanticTextMemory _memory;
    private readonly string _collection;

    public RAGPlugin(ISemanticTextMemory memory, string collection)
    {
        _memory = memory;
        _collection = collection;
    }

    [KernelFunction, Description("Search the indexed knowledge base for passages relevant to the query.")]
    public async Task<string> LookupAsync(
        [Description("Natural language query")] string query,
        Kernel kernel)
    {
        var sb = new StringBuilder();
        await foreach (var r in _memory.SearchAsync(_collection, query, limit: 5, minRelevanceScore: 0.35, withEmbeddings: false, kernel: kernel))
        {
            sb.AppendLine(r.Metadata.Text);
        }
        return sb.Length == 0 ? "No relevant information found. Consider searching the web first." : sb.ToString();
    }
}
