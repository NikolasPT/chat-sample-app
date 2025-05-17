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
        var deploymentName       = config["AzureAIFoundry:DeploymentName"]!;
        var endpoint             = config["AzureAIFoundry:GPT41:Endpoint"]!;
        var apiKey               = config["AzureAIFoundry:GPT41:APIKey"]!;

        var searchEngineId       = config["Google:SearchEngineId"]!;
        var searchConsoleAPIKey  = config["Google:SearchConsoleAPIKey"]!;

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
            string question = Console.ReadLine() ?? "";
            if (string.Equals(question, "exit", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            history.AddUserMessage(question); // Add question to chat history

            // ---------- Stream the reply ----------
            StringBuilder buffer = new();
            Console.Write("AI: ");
            await foreach (var chunk in chatCompletionService
                .GetStreamingChatMessageContentsAsync(history, settings, kernel))
            {
                Console.Write(chunk.Content);
                buffer.Append(chunk.Content); 
            }
            Console.WriteLine();

            history.AddAssistantMessage(buffer.ToString()); // Add reply to chat history
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
        Console.WriteLine("===================================================================");
        Console.WriteLine($"{context.Function.PluginName}.{context.Function.Name}");
        Console.WriteLine($"args: {JsonSerializer.Serialize(context.Arguments, opts)}");

        await next(context);           // executes the plugin

        // 2️ after: GetValue<T>() to read the payload
        if (context.Result is { } r)
        {
            var payload = r.GetValue<object>();   // or IEnumerable<TextSearchResult>
            Console.WriteLine($"← {context.Function.Name} result:");
            Console.WriteLine(JsonSerializer.Serialize(payload, opts));
        }
        Console.WriteLine("===================================================================");
        Console.WriteLine();
    }
}

