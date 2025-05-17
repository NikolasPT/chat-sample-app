using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Text;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using AngleSharp.Html.Parser;


namespace SemanticKernelSamples;

internal static class Sample07
{
    public static async Task RunAsync(IConfiguration config)
    {
        var deploymentName = config["AzureAIFoundry:DeploymentName"]!;
        var endpoint = config["AzureAIFoundry:GPT41:Endpoint"]!;
        var apiKey = config["AzureAIFoundry:GPT41:APIKey"]!;

        var embeddingDeploymentName = config["AzureAIFoundry:EmbeddingDeploymentName"]!;
        var embeddingEndpoint = config["AzureAIFoundry:TextEmbedding3Large:Endpoint"]!;
        var embeddingApiKey = config["AzureAIFoundry:TextEmbedding3Large:APIKey"]!;

        const string memoryName = "RAG-memory";

        // Initialize Semantic Kernel and chat service
        var builder = Kernel.CreateBuilder();
        builder.AddAzureOpenAIChatCompletion(deploymentName, endpoint, apiKey);
        var kernel = builder.Build();

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        ChatHistory chatHistory = new (systemMessage: "You are an AI assistant that helps people find information.");

        // Build in-memory semantic memory with embeddings for RAG
        // Instantiate the embedding generation service
        var embeddingService = new AzureOpenAITextEmbeddingGenerationService(embeddingDeploymentName, embeddingEndpoint, embeddingApiKey);

        // Explicitly type memory variable and use WithTextEmbeddingGeneration
        ISemanticTextMemory memory = new MemoryBuilder()
            .WithMemoryStore(new VolatileMemoryStore())
            .WithTextEmbeddingGeneration(embeddingService) // Use the instantiated service
            .Build();

        // Download and index documents into memory
        List<string> articleList = 
        [
            "https://www.dr.dk/event/melodigrandprix/saadan-er-danmarks-chancer-i-eurovision-finalen",
            "https://www.dr.dk/nyheder/indland/ansatte-i-hjemmeplejen-i-nordjysk-kommune-skal-nu-til-arbejde-baade-aften-og-nat"
        ];
        List<string> allParagraphs = [];
        using HttpClient httpClient = new();
        foreach (var url in articleList)
        {
            // Fetch raw HTML and parse with AngleSharp for readable text
            var html = await httpClient.GetStringAsync(url);
            var parser = new HtmlParser();
            var doc = await parser.ParseDocumentAsync(html);
            // Target article or main container, fallback to body
            var container = doc.QuerySelector("article")
                           ?? doc.QuerySelector("main")
                           ?? doc.Body;
            var text = container?.TextContent ?? string.Empty;
            var lines = TextChunker.SplitPlainTextLines(text, 64);
            var paragraphs = TextChunker.SplitPlainTextParagraphs(lines, 512);
            allParagraphs.AddRange(paragraphs);
        }
        for (var i = 0; i < allParagraphs.Count; i++)
        {
            await memory.SaveInformationAsync(memoryName, allParagraphs[i], $"paragraph[{i}]");
        }

        // Chat loop with RAG
        Console.WriteLine("Chat with in-memory RAG (type 'exit' to quit):");
        while (true)
        {
            Console.Write("Me: ");
            string question = Console.ReadLine() ?? "";
            if (string.Equals(question, "exit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            // Retrieve relevant context
            StringBuilder contextBuilder = new();
            IAsyncEnumerable<MemoryQueryResult> results = memory.SearchAsync(
                memoryName,
                question,
                limit: 3, // How many results to return
                minRelevanceScore: 0.4f, // Minimum relevance score, lower returns more results
                withEmbeddings: true);

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

                // Add RAG context to chat using the Tool role
                chatHistory.AddMessage(AuthorRole.Developer, contextBuilder.ToString());

                contextIndex = chatHistory.Count;
            }

            // Use null-forgiving operator for question
            chatHistory.AddUserMessage(question!);

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

            // Remove content added by RAG
            if (contextIndex >= 0)
            {
                chatHistory.RemoveAt(contextIndex);
            }

            Console.WriteLine();
        }
    }
    

}