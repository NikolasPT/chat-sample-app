using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Configuration;


namespace SemanticKernelSamples;

internal static class Sample03
{
    public static async Task RunAsync(IConfiguration config)
    {
        var deploymentName = config["AI:OpenAI:DeploymentName"]!;
        var endpoint = config["AI:OpenAI:Endpoint"]!;
        var apiKey = config["AI:OpenAI:APIKey"]!;

        // Initialize Semantic Kernel
        var builder = Kernel.CreateBuilder();
        // Use null-forgiving operator for config values
        builder.AddAzureOpenAIChatCompletion(deploymentName, endpoint, apiKey);
        var kernel = builder.Build();

        // Create chat with history
        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var chat = new ChatHistory(systemMessage: "You are an AI assistant that helps people find information.");

        Console.WriteLine("Chat with memory history (type 'exit' to quit):");
        while (true)
        {
            Console.Write("Me: ");
            var question = Console.ReadLine();
            if (string.Equals(question, "exit", StringComparison.OrdinalIgnoreCase))
                break;

            // Use null-forgiving operator for question
            chat.AddUserMessage(question!); 
            var answer = await chatService.GetChatMessageContentAsync(chat);
            chat.AddAssistantMessage(answer.Content!); 

            Console.WriteLine($"AI: {answer.Content}");
            Console.WriteLine();
        }
    }
}