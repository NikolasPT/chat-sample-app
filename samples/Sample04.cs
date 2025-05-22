using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel.Plugins.Web.Google;
using Microsoft.SemanticKernel.Data;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace SemanticKernelSamples;

/// <summary>
/// This sample demonstrates how to use the Semantic Kernel with Azure OpenAI 
/// and augment it with a Google search plugin. It also demonstrates how to stream back the response.
/// It allows you to ask questions and get answers, 
/// with the AI model invoking the search plugin when needed and streaming the AI response.
/// </summary>

internal static class Sample04
{
    public static async Task<bool> RunAsync(IConfiguration config)
    {
        string deploymentName       = config["AzureAIFoundry:DeploymentName"]!;
        string endpoint             = config["AzureAIFoundry:GPT41:Endpoint"]!;
        string apiKey               = config["AzureAIFoundry:GPT41:APIKey"]!;

        string searchEngineId       = config["Google:SearchEngineId"]!;
        string searchConsoleAPIKey  = config["Google:SearchConsoleAPIKey"]!;

        // ---------- Kernel & model ----------
        IKernelBuilder builder = Kernel.CreateBuilder();
        builder.AddAzureOpenAIChatCompletion(deploymentName, endpoint, apiKey);

        // Register the function-invocation filter so plugin calls are logged
        builder.Services.AddSingleton<IFunctionInvocationFilter, InvocationLogger>();

        Kernel kernel = builder.Build();

        // ---------- Google text-search plugin ----------
        GoogleTextSearch textSearch = new (
            searchEngineId,
            searchConsoleAPIKey);

        KernelPlugin searchPlugin = textSearch.CreateWithGetTextSearchResults("SearchPlugin");
        kernel.Plugins.Add(searchPlugin);

        // ---------- Streaming chat settings ----------
        PromptExecutionSettings settings = new()
        {
            // Let the model decide when to invoke a search
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        IChatCompletionService chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
        ChatHistory history = new(systemMessage:
            "You are an AI assistant that helps people find information. " +
            "Include citations to the relevant information where it is referenced in the response. " +
            "When you call SearchPlugin.GetTextSearchResults, always pass count=\"10\" so that ten results are returned.");

        Console.WriteLine();
        Console.WriteLine("Chat with streaming and Google search enabled (type 'exit' to quit):");
        Console.WriteLine();

        // ---------- Chat loop ----------
        while (true)
        {
            Console.Write("Me: ");
            string userInput = Console.ReadLine() ?? "";
            if (string.IsNullOrWhiteSpace(userInput))
            {
                continue;
            }

            if (string.Equals(userInput, "exit", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            history.AddUserMessage(userInput); // Add question to chat history

            bool aiPrefixPrinted = false; // Tracks if "AI: " has been printed for the current response
            StringBuilder buffer = new();
            // ---------- Stream the reply ----------
            await foreach (var chunk in chatCompletionService
                .GetStreamingChatMessageContentsAsync(history, settings, kernel))
            {
                // Only print "AI: " if there's actual content from the assistant to display
                // and the prefix hasn't been printed yet for this response.
                if (!string.IsNullOrEmpty(chunk.Content))
                {
                    if (!aiPrefixPrinted)
                    {
                        Console.Write("AI: ");
                        aiPrefixPrinted = true;
                    }

                    // Print and append the recieved token to the buffer
                    Console.Write(chunk.Content);
                    buffer.Append(chunk.Content);
                }
            }
            Console.WriteLine();

            // Add the assistant's message to history only if the AI actually provided content.
            if (aiPrefixPrinted) // This implies buffer.Length > 0 for textual content
            {
                history.AddAssistantMessage(buffer.ToString());
            }
            Console.WriteLine();
        }

    }
}


public sealed class InvocationLogger : IFunctionInvocationFilter
{
    public async Task OnFunctionInvocationAsync(
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, Task> next)
    {
        var opts = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

        // 1️ before the plugin runs
        Console.WriteLine();
        Console.WriteLine("Search results section start");
        Console.WriteLine("===================================================================");
        Console.WriteLine($"{context.Function.PluginName}.{context.Function.Name}");
        Console.WriteLine($"args: {JsonSerializer.Serialize(context.Arguments, opts)}");

        await next(context); // executes the plugin

        // 2️ after: GetValue<T>() to read the payload
        if (context.Result is { } r)
        {
            var payload = r.GetValue<object>();
            Console.WriteLine($"← {context.Function.Name} result:");
            Console.WriteLine(JsonSerializer.Serialize(payload, opts));
        }
        Console.WriteLine("===================================================================");
        Console.WriteLine("Search results section finished");
        Console.WriteLine();
    }
}

