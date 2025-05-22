using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace SemanticKernelSamples;

/// <summary>
/// This sample demostrates how to add a simple plugin to the Semantic Kernel.
/// The plugin augments the AI model with a function that returns the current UTC date/time.
/// The model itself chooses when to call the plugin function by setting the 
/// FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
/// </summary>

internal static class Sample02
{
    public static async Task<bool> RunAsync(IConfiguration config)
    {
        string deploymentName       = config["AzureAIFoundry:DeploymentName"]!;
        string endpoint             = config["AzureAIFoundry:GPT41:Endpoint"]!;
        string apiKey               = config["AzureAIFoundry:GPT41:APIKey"]!;

        // ---------- Kernel & model ----------
        var builder = Kernel.CreateBuilder();

        builder.Services.AddLogging(log =>
        {
            log.AddConsole()
               .AddDebug()
               .SetMinimumLevel(LogLevel.Information);
        });

        // Configure HttpClient defaults for Semantic Kernel and other services
        builder.Services.ConfigureHttpClientDefaults(httpBuilder =>
        {
            // Add our custom RawWireLogger to log full request/response bodies
            httpBuilder.AddHttpMessageHandler<RawWireLogger>();
        });

        builder.Services.AddTransient<RawWireLogger>();

        // Add Azure OpenAI Chat Completion - it will use the default configured HttpClient
        builder.AddAzureOpenAIChatCompletion(deploymentName, endpoint, apiKey);

        // ---------- Register plugin ----------
        builder.Plugins.Add(KernelPluginFactory.CreateFromType<DateTimePlugin>());

        Kernel kernel = builder.Build();
        var chat = kernel.GetRequiredService<IChatCompletionService>();

        // ---------- Auto function-calling settings ----------
        OpenAIPromptExecutionSettings settings = new()
        {
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

            Console.WriteLine();
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







// Full-body wire logger ----------------------------------------------------
public sealed class RawWireLogger(ILogger<RawWireLogger> log) : DelegatingHandler
{

    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new()
    { 
        WriteIndented = true
    };

    private static string FormatJsonPayload(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload) || payload == "<no body>")
        {
            return payload ?? "<no body>";
        }
        try
        {
            using JsonDocument jsonDoc = JsonDocument.Parse(payload);
            return JsonSerializer.Serialize(jsonDoc.RootElement, s_jsonSerializerOptions);
        }
        catch (JsonException)
        {
            // Not a valid JSON string, return as is.
            return payload;
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage req, CancellationToken ct)
    {
        string requestBodyString = "<no body>";
        if (req.Content != null)
        {
            await req.Content.LoadIntoBufferAsync(); // Buffer content for multiple reads
            requestBodyString = await req.Content.ReadAsStringAsync(ct);
        }

        // Log nicely formatted request
        log.LogInformation(
            "\n>> HTTP {Method} {Uri}\n   Request Body:\n{Body}",
            req.Method,
            req.RequestUri,
            FormatJsonPayload(requestBodyString)
        );

        var rsp = await base.SendAsync(req, ct);

        string responseBodyString = "<no body>";
        if (rsp.Content != null)
        {
            await rsp.Content.LoadIntoBufferAsync(); // Buffer content for multiple reads
            responseBodyString = await rsp.Content.ReadAsStringAsync(ct);
        }

        // Log nicely formatted response
        log.LogInformation(
            "\n<< HTTP {StatusCode} ({ReasonPhrase})\n   Response Body:\n{Body}",
            (int)rsp.StatusCode, // Log status code as int for better structure
            rsp.ReasonPhrase,
            FormatJsonPayload(responseBodyString)
        );

        return rsp;
    }

}