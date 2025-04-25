using Microsoft.Extensions.Configuration;

namespace SemanticKernelSamples;

class Program
{
    static async Task Main(string[] args)
    {
        // Load AI configuration from user secrets and appsettings.json
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true) // Add this line
            .AddUserSecrets<Program>()
            .Build();

        Console.WriteLine("Choose a Semantic Kernel chat sample to run:");
        Console.WriteLine(" 1) Console chat basic");
        Console.WriteLine(" 2) Chat with DateTime plugin");
        Console.WriteLine(" 3) Chat with memory history");
        Console.WriteLine(" 4) (reserved)");
        Console.WriteLine(" 5) Chat with dynamic web content");
        Console.WriteLine(" 6) Embedding similarity demo");
        Console.WriteLine(" 7) Chat with in‑memory RAG");
        Console.WriteLine(" 8) Chat with SQLite RAG");
        Console.Write("Enter choice (1-8): ");
        if (!int.TryParse(Console.ReadLine(), out int choice))
        {
            Console.WriteLine("Invalid input. Exiting.");
            return;
        }

        switch (choice)
        {
            case 1:
                await Sample01.RunAsync(config);
                break;
            case 2:
                await Sample02.RunAsync(config);
                break;
            case 3:
                await Sample03.RunAsync(config);
                break;
            case 5:
                await Sample05.RunAsync(config);
                break;
            case 6:
                await Sample06.RunAsync(config);
                break;
            case 7:
                await Sample07.RunAsync(config);
                break;
            case 8:
                await Sample08.RunAsync(config);
                break;
            default:
                Console.WriteLine("Option not yet implemented.");
                break;
        }
    }
}