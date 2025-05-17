using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Text;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using AngleSharp.Html.Parser;
using AngleSharp.Html.Dom;
using AngleSharp.Dom;
using System.Collections.Concurrent;
using System.ComponentModel;

namespace SemanticKernelSamples;

/// <summary>
/// Demonstrates an "Agentic RAG" pattern where the model can autonomously
/// call a retrieval plugin via Semantic Kernel's automatic function-calling feature.
/// </summary>
internal static class Sample07
{
    public static async Task<bool> RunAsync(IConfiguration config)
    {
        // ---- Azure OpenAI configuration ----
        string deploymentName      = config["AzureAIFoundry:DeploymentName"]!;
        string endpoint            = config["AzureAIFoundry:GPT41:Endpoint"]!;
        string apiKey              = config["AzureAIFoundry:GPT41:APIKey"]!;

        string embeddingDeployment = config["AzureAIFoundry:EmbeddingDeploymentName"]!;
        string embeddingEndpoint   = config["AzureAIFoundry:TextEmbedding3Large:Endpoint"]!;
        string embeddingApiKey     = config["AzureAIFoundry:TextEmbedding3Large:APIKey"]!;

        const string MemoryCollection = "RAG-memory";

        // ---- Build kernel & AI services ----
        var builder = Kernel.CreateBuilder();
        builder.AddAzureOpenAIChatCompletion(deploymentName, endpoint, apiKey);
        Kernel kernel = builder.Build();

        IChatCompletionService chatService = kernel.GetRequiredService<IChatCompletionService>();

        // Text-embedding service and in-memory vector store
        AzureOpenAITextEmbeddingGenerationService embeddingService = new AzureOpenAITextEmbeddingGenerationService(
            embeddingDeployment, embeddingEndpoint, embeddingApiKey);

        VolatileMemoryStore memoryStore = new VolatileMemoryStore();
        ISemanticTextMemory memory = new MemoryBuilder()
            .WithMemoryStore(memoryStore)
            .WithTextEmbeddingGeneration(embeddingService)
            .Build();

        // ---- Ingest content into memory ----
        List<string> articleUrls = new List<string>
        {
            "https://www.dr.dk/event/melodigrandprix/saadan-er-danmarks-chancer-i-eurovision-finalen",
            "https://www.dr.dk/nyheder/indland/ansatte-i-hjemmeplejen-i-nordjysk-kommune-skal-nu-til-arbejde-baade-aften-og-nat"
        };

        HttpClient httpClient = new HttpClient();
        ConcurrentBag<string> paragraphs = new ConcurrentBag<string>();

        await Parallel.ForEachAsync(articleUrls, async (string url, CancellationToken ct) =>
        {
            string html = await httpClient.GetStringAsync(url, ct);
            HtmlParser parser = new HtmlParser();
            IHtmlDocument doc = await parser.ParseDocumentAsync(html, ct);

            IElement? container = doc.QuerySelector("article") ?? doc.QuerySelector("main") ?? doc.Body;
            if (container != null)
            {
                List<string> lines = TextChunker.SplitPlainTextLines(container.TextContent, 64);
                List<string> paras = TextChunker.SplitPlainTextParagraphs(lines, 512);

                foreach (string paragraph in paras)
                {
                    if (!string.IsNullOrWhiteSpace(paragraph))
                    {
                        paragraphs.Add(paragraph.Trim());
                    }
                }
            }
        });

        int id = 0;
        foreach (string paragraph in paragraphs)
        {
            await memory.SaveInformationAsync(MemoryCollection, paragraph, $"paragraph[{id++}]");
        }

        // ---- Register retrieval plugin ----
        SearchMemoryPlugin ragPlugin = new SearchMemoryPlugin(memory, MemoryCollection);
        kernel.Plugins.AddFromObject(ragPlugin, "RAG");

        // ---- Chat loop ----
        ChatHistory chatHistory = new("You are an AI assistant that helps people find information. Use the RAG plugin when needed to answer user questions accurately.");

        AzureOpenAIPromptExecutionSettings settings = new()
        {
            ToolCallBehavior = Microsoft.SemanticKernel.Connectors.OpenAI.ToolCallBehavior.AutoInvokeKernelFunctions,
        };

        Console.WriteLine("Agentic RAG chat (type 'exit' to quit):");
        while (true)
        {
            Console.Write("Me: ");
            string question = Console.ReadLine() ?? string.Empty;
            if (question.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            chatHistory.AddUserMessage(question);

            Console.Write("AI: ");
            await foreach (StreamingChatMessageContent msg in chatService.GetStreamingChatMessageContentsAsync(chatHistory, settings, kernel))
            {
                if (msg.Role == AuthorRole.Assistant)
                {
                    Console.Write(msg.Content);
                }

                // Add all messages (assistant + tool) to history so the model can continue the chain-of-thought.
                // Make sure to check for null or empty content before adding to history.
                if (msg.Content is { Length: > 0 } && msg.Role.HasValue)
                {
                    chatHistory.AddMessage(msg.Role.Value, msg.Content);
                }
            }
            Console.WriteLine();
        }
    }
}

/// <summary>
/// Retrieval plugin that Semantic Kernel can auto-invoke to fetch relevant paragraphs
/// from the semantic memory.
/// </summary>
public sealed class SearchMemoryPlugin
{
    private readonly ISemanticTextMemory _memory;
    private readonly string _collection;

    public SearchMemoryPlugin(ISemanticTextMemory memory, string collection)
    {
        _memory = memory;
        _collection = collection;
    }

    [KernelFunction, Description("Searches the indexed memory for paragraphs relevant to the user's query and returns them as context.")]
    public async Task<string> LookupAsync(
        [Description("The user's natural-language search query")] string query,
        Kernel kernel)
    {
        StringBuilder sb = new StringBuilder();
        await foreach (MemoryQueryResult res in _memory.SearchAsync(_collection, query, limit: 3, minRelevanceScore: 0.4, withEmbeddings: true, kernel: kernel))
        {
            sb.AppendLine(res.Metadata.Text);
        }
        return sb.ToString();
    }
}
