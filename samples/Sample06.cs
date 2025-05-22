using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Text;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using AngleSharp.Html.Parser;
using System.Collections.Concurrent;

namespace SemanticKernelSamples;

/// <summary>
/// This sample demonstrates how to addd Retrieval-Augmented Generation (RAG) to a chat service using Semantic Kernel.
/// It defines a list of articles, fetches their content, and indexes them into a memory store. This data is then used
/// for contextual information retrieval during the chat session.
/// </summary>

internal static class Sample06
{
    public static async Task<bool> RunAsync(IConfiguration config)
    {
        string deploymentName           = config["AzureAIFoundry:DeploymentName"]!;
        string endpoint                 = config["AzureAIFoundry:GPT41:Endpoint"]!;
        string apiKey                   = config["AzureAIFoundry:GPT41:APIKey"]!;

        string embeddingDeploymentName  = config["AzureAIFoundry:EmbeddingDeploymentName"]!;
        string embeddingEndpoint        = config["AzureAIFoundry:TextEmbedding3Large:Endpoint"]!;
        string embeddingApiKey          = config["AzureAIFoundry:TextEmbedding3Large:APIKey"]!;

        const string memoryName         = "RAG-memory";

        // Initialize Semantic Kernel and chat service
        var builder = Kernel.CreateBuilder();
        builder.AddAzureOpenAIChatCompletion(deploymentName, endpoint, apiKey);
        Kernel kernel = builder.Build();

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        ChatHistory chatHistory = new (systemMessage: "You are an AI assistant that helps people find information.");

        // Instantiate the embedding generation service
        AzureOpenAITextEmbeddingGenerationService embeddingService = new(
            embeddingDeploymentName,
            embeddingEndpoint,
            embeddingApiKey);

        // Instantiate the memory store for vector storage
        // Use the Semantic Kernel VolatileMemoryStore for in-memory storage, other vector stores could be used as well.
        // For example, you could use a database or a cloud-based vector store for persistent storage.
        VolatileMemoryStore memoryStore = new();

        // The combination of the text embedding generator and the memory store makes up the 'SemanticTextMemory' 
        // object used to store and retrieve memories.
        ISemanticTextMemory memory = new MemoryBuilder()
            .WithMemoryStore(memoryStore)                   // Use the instantiated memory store
            .WithTextEmbeddingGeneration(embeddingService)  // Use the instantiated service
            .Build();

        // Download and index websites into memory
        List<string> articleList = 
        [
            "https://www.dr.dk/event/melodigrandprix/saadan-er-danmarks-chancer-i-eurovision-finalen",
            "https://www.dr.dk/event/melodigrandprix/stemmetallene-afsloeret-saadan-gik-det-danmark-i-semifinalen-ved-eurovision",
            "https://www.dr.dk/event/melodigrandprix/ekspert-om-totalt-uforudsigelig-eurovision-finale-det-er-helt-uden-skiven",
            "https://www.dr.dk/event/melodigrandprix/danmark-fik-kun-point-fra-seerne-i-eurovision-finalen-det-var-en-meget",
            "https://www.dr.dk/nyheder/kultur/oestrig-banker-sverige-og-vinder-eurovision-tredje-gang",
            "https://www.dr.dk/event/melodigrandprix/fraekt-sceneshow-var-meget-finland-tvunget-til-aendre-sin-optraeden-foer"
        ];

        ConcurrentBag<string> allParagraphs = []; // ConcurrentBag to allow concurrent writes
        using HttpClient httpClient = new();

        // Fetch and parse each URL concurrently
        await Parallel.ForEachAsync(articleList, async (url, cancellationToken) =>
        {
            string textContent = await ParseUrlContentAsync(url, httpClient);
            if (!string.IsNullOrEmpty(textContent))
            {
                List<string> paragraphs = ChunkTextContent(textContent);
                foreach (var paragraph in paragraphs)
                {
                    allParagraphs.Add(paragraph);
                }
            }
        });

        // Save each paragraph to the semantic text memory
        for (var i = 0; i < allParagraphs.Count; i++)
        {
            await memory.SaveInformationAsync(memoryName, allParagraphs.ElementAt(i), $"paragraph[{i}]"); 
        }

        // ---------- Chat loop with RAG ----------
        Console.WriteLine("Chat with in-memory RAG (type 'exit' to quit):");
        while (true)
        {
            Console.Write("Me: ");
            string question = Console.ReadLine() ?? "";
            if (string.Equals(question, "exit", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Retrieve relevant context
            IAsyncEnumerable<MemoryQueryResult> results = memory.SearchAsync(
                memoryName,
                question,
                limit: 6,                   // The maximum number of results to return
                minRelevanceScore: 0.2f,    // Minimum relevance score, lower returns more results
                withEmbeddings: true);

            // Create a StringBuilder to store the context
            StringBuilder contextBuilder = new();
            await foreach (var result in results)
            {
                contextBuilder.AppendLine(result.Metadata.Text);
            }

            int contextIndex = -1;
            if (contextBuilder.Length > 0)
            {
                // Output RAG context for debugging
                Console.WriteLine("RAG system provided context:");
                Console.WriteLine(contextBuilder.ToString());
                Console.WriteLine();

                // --------- Add RAG context to chat --------- 
                chatHistory.AddAssistantMessage($" Context: \n{contextBuilder}");

                contextIndex = chatHistory.Count - 1; // Store index of context message
            }

            // Add user question/message to chat history
            chatHistory.AddUserMessage(question);

            // Stream response
            StringBuilder responseBuilder = new();
            Console.Write("AI: ");
            await foreach (var msg in chatService.GetStreamingChatMessageContentsAsync(chatHistory, null, kernel))
            {
                Console.Write(msg.Content);
                responseBuilder.Append(msg.Content);
            }
            Console.WriteLine();
            chatHistory.AddAssistantMessage(responseBuilder.ToString());

            // Remove content added by RAG from the chat history
            if (contextIndex >= 0)
            {
                chatHistory.RemoveAt(contextIndex);
            }

            Console.WriteLine();
        }
        
    }
    

    /// <summary>
    /// Fetches the HTML content from a given URL and parses it to extract the text content.
    /// </summary>
    private static async Task<string> ParseUrlContentAsync(string url, HttpClient httpClient)
    {
        try
        {
            string html = await httpClient.GetStringAsync(url);
            HtmlParser parser = new();
            var doc = await parser.ParseDocumentAsync(html);

            // Target article or main container, fallback to body
            var container = doc.QuerySelector("article")
                           ?? doc.QuerySelector("main")
                           ?? doc.Body;

            return container?.TextContent ?? "";
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Error fetching URL {url}: {ex.Message}");
            return "";
        }
    }


    /// <summary>
    /// Splits the text content into smaller chunks for better indexing and retrieval 
    /// using the TextChunker in Semantic Kernel.
    /// </summary>
    private static List<string> ChunkTextContent(string text)
    {
        int maxTokensPerLine = 64;
        int maxTokensPerParagraph = 512;

        // Split text into lines
        List<string> lines = TextChunker.SplitPlainTextLines(text, maxTokensPerLine);

        // Merge lines into paragraphs
        List<string> paragraphs = TextChunker.SplitPlainTextParagraphs(lines, maxTokensPerParagraph);

        return paragraphs;
    }
    

}