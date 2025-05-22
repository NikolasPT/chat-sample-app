using Microsoft.SemanticKernel;
using Microsoft.Extensions.Configuration;


namespace SemanticKernelSamples;

/// <summary>
/// This sample demonstrates how to use the Semantic Kernel with Azure OpenAI to setup a simple chat.
/// It allows you to send a prompt and get a response from the AI model.
/// </summary>

internal static class Sample01
{
    public static async Task<bool> RunAsync(IConfiguration config)
    {
        string deploymentName       = config["AzureAIFoundry:DeploymentName"]!;
        string endpoint             = config["AzureAIFoundry:GPT41:Endpoint"]!;
        string apiKey               = config["AzureAIFoundry:GPT41:APIKey"]!;

        // Initialize Semantic Kernel
        var builder = Kernel.CreateBuilder();
        builder.AddAzureOpenAIChatCompletion(deploymentName, endpoint, apiKey);
        Kernel kernel = builder.Build();

        Console.WriteLine();
        Console.WriteLine("Basic chat (type 'exit' to quit):");
        Console.WriteLine();

        // ---------- Chat loop ----------
        while (true)
        {
            Console.Write("Me: ");
            string question = Console.ReadLine()!;
            if (string.Equals(question, "exit", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var response = await kernel.InvokePromptAsync(question);
            Console.WriteLine($"AI: {response}");
            Console.WriteLine();
        }
        
    }
}