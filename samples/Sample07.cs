using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Text;
// Add plugin memory namespace for extension methods
using Microsoft.SemanticKernel.Plugins.Memory;
// Add Azure OpenAI connector namespace for extension methods
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Embeddings; // Add this for ITextEmbeddingGenerationService

namespace SemanticKernelSamples
{
    internal static class Sample07
    {
        public static async Task RunAsync(IConfiguration config)
        {
            var deploymentName = config["AI:OpenAI:DeploymentName"];
            var embeddingDeploymentName = config["AI:OpenAI:EmbeddingDeploymentName"];
            var endpoint = config["AI:OpenAI:Endpoint"];
            var apiKey = config["AI:OpenAI:APIKey"];

            // Initialize Semantic Kernel and chat service
            var builder = Kernel.CreateBuilder();
            builder.AddAzureOpenAIChatCompletion(deploymentName!, endpoint!, apiKey!);
            var kernel = builder.Build();
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var chat = new ChatHistory(systemMessage: "You are an AI assistant that helps people find information.");

            // Build in-memory semantic memory with embeddings for RAG
            // Instantiate the embedding generation service
            var embeddingService = new AzureOpenAITextEmbeddingGenerationService(embeddingDeploymentName!, endpoint!, apiKey!);
            // Explicitly type memory variable and use WithTextEmbeddingGeneration
            ISemanticTextMemory memory = new MemoryBuilder()
                .WithMemoryStore(new VolatileMemoryStore())
                .WithTextEmbeddingGeneration(embeddingService) // Use the instantiated service
                .Build();

            // Download and index documents into memory
            var articleList = new List<string>
            {
                "https://raw.githubusercontent.com/dotnet/docs/main/docs/csharp/whats-new/relationships-between-language-and-library.md",
                "https://raw.githubusercontent.com/dotnet/docs/main/docs/csharp/whats-new/version-update-considerations.md",
                "https://raw.githubusercontent.com/dotnet/docs/main/docs/csharp/whats-new/csharp-version-history.md",
                "https://raw.githubusercontent.com/dotnet/docs/main/docs/csharp/whats-new/csharp-11.md",
                "https://raw.githubusercontent.com/dotnet/roslyn/main/docs/compilers/CSharp/Compiler%20Breaking%20Changes%20-%20DotNet%207.md",
                "https://raw.githubusercontent.com/dotnet/docs/main/docs/csharp/whats-new/csharp-12.md",
                "https://raw.githubusercontent.com/dotnet/roslyn/main/docs/compilers/CSharp/Compiler%20Breaking%20Changes%20-%20DotNet%208.md",
                "https://raw.githubusercontent.com/dotnet/docs/main/docs/csharp/whats-new/csharp-13.md",
                "https://raw.githubusercontent.com/dotnet/roslyn/main/docs/compilers/CSharp/Compiler%20Breaking%20Changes%20-%20DotNet%209.md",
                "https://raw.githubusercontent.com/dotnet/docs/main/docs/core/whats-new/dotnet-8/overview.md",
                "https://raw.githubusercontent.com/dotnet/docs/main/docs/core/whats-new/dotnet-8/runtime.md",
                "https://raw.githubusercontent.com/dotnet/docs/main/docs/core/whats-new/dotnet-8/sdk.md",
                "https://raw.githubusercontent.com/dotnet/docs/main/docs/core/whats-new/dotnet-8/containers.md"
            };
            var allParagraphs = new List<string>();
            using var httpClient = new HttpClient();
            foreach (var url in articleList)
            {
                var content = await httpClient.GetStringAsync(url);
                var lines = TextChunker.SplitPlainTextLines(content, 64);
                var paragraphs = TextChunker.SplitPlainTextParagraphs(lines, 512);
                allParagraphs.AddRange(paragraphs);
            }
            for (var i = 0; i < allParagraphs.Count; i++)
            {
                await memory.SaveInformationAsync("microsoft-news", allParagraphs[i], $"paragraph[{i}]");
            }

            // Chat loop with RAG
            Console.WriteLine("Chat with in-memory RAG (type 'exit' to quit):");
            while (true)
            {
                Console.Write("Me: ");
                var question = Console.ReadLine();
                if (string.Equals(question, "exit", StringComparison.OrdinalIgnoreCase)) break;

                // Retrieve relevant context
                var contextBuilder = new StringBuilder();
                // Use null-forgiving operator for question
                await foreach (var result in memory.SearchAsync("microsoft-news", question!, limit: 3))
                {
                    contextBuilder.AppendLine(result.Metadata.Text);
                }

                int contextIndex = -1;
                if (contextBuilder.Length > 0)
                {
                    chat.AddUserMessage("Here's some additional information: " + contextBuilder);
                    contextIndex = chat.Count;
                }
                // Use null-forgiving operator for question
                chat.AddUserMessage(question!);

                // Stream response
                var responseBuilder = new StringBuilder();
                Console.Write("AI: ");
                await foreach (var msg in chatService.GetStreamingChatMessageContentsAsync(chat, null, kernel))
                {
                    Console.Write(msg.Content);
                    responseBuilder.Append(msg.Content);
                }
                Console.WriteLine();
                chat.AddAssistantMessage(responseBuilder.ToString());

                // Remove added context
                if (contextIndex >= 0) chat.RemoveAt(contextIndex);
                Console.WriteLine();
            }
        }
    }
}