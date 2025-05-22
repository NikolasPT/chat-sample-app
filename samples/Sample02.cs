using System.ComponentModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace SemanticKernelSamples;

/// <summary>
/// Demonstrates a DateTime plugin that the model can invoke automatically
/// thanks to <see cref="OpenAIPromptExecutionSettings"/> with
/// <see cref="FunctionChoiceBehavior.Auto()"/>.
/// </summary>

internal static class Sample02
{
    public static async Task<bool> RunAsync(IConfiguration config)
    {
        string deploymentName =     config["AzureAIFoundry:DeploymentName"]!;
        string endpoint =           config["AzureAIFoundry:GPT41:Endpoint"]!;
        string apiKey =             config["AzureAIFoundry:GPT41:APIKey"]!;

        // ---------- Kernel & model ----------
        var builder = Kernel.CreateBuilder();
        builder.AddAzureOpenAIChatCompletion(deploymentName, endpoint, apiKey);
        builder.Services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));

        // ---------- Register plugin ----------
        builder.Plugins.Add(KernelPluginFactory.CreateFromType<DateTimePlugin>());

        Kernel kernel = builder.Build();
        var chat = kernel.GetRequiredService<IChatCompletionService>();

        // ---------- Auto function-calling settings ----------
        OpenAIPromptExecutionSettings settings = new()
        {
            // Let the model decide when it should call a plugin function
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        Console.WriteLine();
        Console.WriteLine("Chat with DateTime plugin (type 'exit' to quit):");
        Console.WriteLine();

        // ---------- Chat loop ----------
        while (true)
        {
            Console.Write("Me: ");
            string userInput = Console.ReadLine()!;
            if (string.Equals(userInput, "exit", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var response = await chat.GetChatMessageContentAsync(
                userInput,
                executionSettings: settings,
                kernel: kernel);

            Console.WriteLine($"AI: {response.Content}");
            Console.WriteLine();
        }
    }
}

/// <summary>
/// Plugin that exposes the current UTC date/time.
/// </summary>
internal class DateTimePlugin
{
    [KernelFunction("now")]
    [Description("Returns the current UTC date and time in RFC 1123 format.")]
    public static string Now()
    {
        return DateTime.UtcNow.ToString("r");
    }
}
