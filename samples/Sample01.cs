using System;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Configuration;

namespace SemanticKernelSamples
{
    internal static class Sample01
    {
        public static async Task RunAsync(IConfiguration config)
        {
            var deploymentName = "gpt-4.1";
            var endpoint = config["AI:OpenAI:Endpoint"]!;
            var apiKey = config["AI:OpenAI:APIKey"]!;

            // Initialize Semantic Kernel
            var builder = Kernel.CreateBuilder();
            builder.AddAzureOpenAIChatCompletion(deploymentName, endpoint, apiKey);
            var kernel = builder.Build();

            Console.WriteLine("Basic chat (type 'exit' to quit):");
            while (true)
            {
                Console.Write("Me: ");
                var question = Console.ReadLine();
                if (string.Equals(question, "exit", StringComparison.OrdinalIgnoreCase))
                    break;

                var response = await kernel.InvokePromptAsync(question!);
                Console.WriteLine($"AI: {response}");
                Console.WriteLine();
            }
        }
    }
}