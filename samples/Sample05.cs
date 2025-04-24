using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Configuration;
// Add Azure OpenAI connector namespace for extension methods
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;

namespace SemanticKernelSamples
{
    internal static class Sample05
    {
        public static async Task RunAsync(IConfiguration config)
        {
            var deploymentName = config["AI:OpenAI:DeploymentName"];
            var endpoint = config["AI:OpenAI:Endpoint"];
            var apiKey = config["AI:OpenAI:APIKey"];

            // Initialize Kernel
            var builder = Kernel.CreateBuilder();
            // Use null-forgiving operator for config values
            builder.AddAzureOpenAIChatCompletion(deploymentName!, endpoint!, apiKey!);
            var kernel = builder.Build();

            // Prepare chat history
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var chat = new ChatHistory(systemMessage: "You are an AI assistant that helps people find information.");

            // Download web pages for context
            var urls = new[]
            {
                "https://raw.githubusercontent.com/dotnet/docs/main/docs/csharp/whats-new/csharp-12.md"
                // add more URLs as needed
            };
            var sb = new StringBuilder();
            using var http = new HttpClient();
            foreach (var url in urls)
            {
                sb.AppendLine(await http.GetStringAsync(url));
            }
            chat.AddUserMessage($"Here's some additional information: {sb}");

            Console.WriteLine("Chat with dynamic web content (type 'exit' to quit):");
            while (true)
            {
                Console.Write("Me: ");
                var question = Console.ReadLine();
                if (string.Equals(question, "exit", StringComparison.OrdinalIgnoreCase)) break;

                // Use null-forgiving operator for question
                chat.AddUserMessage(question!); 
                var responseBuilder = new StringBuilder();
                Console.Write("AI: ");
                await foreach (var msg in chatService.GetStreamingChatMessageContentsAsync(chat, null, kernel))
                {
                    Console.Write(msg.Content);
                    responseBuilder.Append(msg.Content);
                }
                Console.WriteLine();
                chat.AddAssistantMessage(responseBuilder.ToString());
                Console.WriteLine();
            }
        }
    }
}