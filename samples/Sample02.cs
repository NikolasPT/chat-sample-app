using Microsoft.SemanticKernel;
using Microsoft.Extensions.Configuration;


namespace SemanticKernelSamples;

/// <summary>
/// This sample demonstrates Semantic Kernel plugins and how to use them in a chat context.
/// It uses a DateTime plugin to get the current date and time, which is then used in the chat prompt.
/// </summary>

internal static class Sample02
{
    public static async Task RunAsync(IConfiguration config)
    {
        var deploymentName       = config["AzureAIFoundry:DeploymentName"]!;
        var endpoint             = config["AzureAIFoundry:GPT41:Endpoint"]!;
        var apiKey               = config["AzureAIFoundry:GPT41:APIKey"]!;

        // Initialize Semantic Kernel
        var builder = Kernel.CreateBuilder();
        builder.AddAzureOpenAIChatCompletion(deploymentName, endpoint, apiKey);
        
        // Register DateTime helper plugin
        builder.Plugins.AddFromFunctions(
            pluginName: "DateTimeHelpers",
            functions:
            [
                KernelFunctionFactory.CreateFromMethod(
                    () => DateTime.UtcNow.ToString("r"),
                    "Now",
                    "Gets the current date and time"
                )
            ]);
        var kernel = builder.Build();

        // Create prompt function using the DateTime plugin
        var prompt = KernelFunctionFactory.CreateFromPrompt(
            promptTemplate: @"The current date and time is {{ datetimehelpers.now }}.{{ $input }}"
        );

        Console.WriteLine();
        Console.WriteLine("Chat with DateTime plugin (type 'exit' to quit):");
        Console.WriteLine();

        // ---------- Chat loop ----------
        while (true)
        {
            Console.Write("Me: ");
            string? input = Console.ReadLine();
            if (string.Equals(input, "exit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            var result = await prompt.InvokeAsync(kernel, new() { ["input"] = input });
            Console.WriteLine($"AI: {result}");
            Console.WriteLine();
        }

    }
}