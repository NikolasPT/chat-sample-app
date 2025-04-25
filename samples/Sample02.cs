using Microsoft.SemanticKernel;
using Microsoft.Extensions.Configuration;


namespace SemanticKernelSamples;

internal static class Sample02
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

        Console.WriteLine("Chat with DateTime plugin (type 'exit' to quit):");
        while (true)
        {
            Console.Write("Me: ");
            var input = Console.ReadLine();
            if (string.Equals(input, "exit", StringComparison.OrdinalIgnoreCase)) break;

            // Use null-forgiving operator for input
            var result = await prompt.InvokeAsync(kernel, new() { ["input"] = input! });
            Console.WriteLine($"AI: {result}");
            Console.WriteLine();
        }
    }
}