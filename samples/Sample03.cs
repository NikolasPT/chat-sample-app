using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Configuration;


namespace SemanticKernelSamples;

/// <summary>
/// This sample demonstrates the chat history feature of the Semantic Kernel.
/// It allows you to have a conversation with the AI, maintaining context across messages.
/// </summary>

internal static class Sample03
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

        // Create chat with history
        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        ChatHistory chatHistory = new (
            systemMessage: "You are an AI assistant that helps people find information.");

        Console.WriteLine();
        Console.WriteLine("Chat with memory history (type 'exit' to quit):");
        Console.WriteLine();

        // ---------- Chat loop ----------
        while (true)
        {
            Console.Write("Me: ");
            string? question = Console.ReadLine()!;
            if (string.Equals(question, "exit", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            chatHistory.AddUserMessage(question); 
            var answer = await chatService.GetChatMessageContentAsync(chatHistory);
            chatHistory.AddAssistantMessage(answer.Content!); 

            Console.WriteLine($"AI: {answer.Content}");
            Console.WriteLine();
        }
        
    }
}