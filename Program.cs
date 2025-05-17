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

        bool exitProgram = false;
        while (!exitProgram)
        {
            Console.WriteLine("\nChoose a Semantic Kernel sample to run:");
            Console.WriteLine(" 1) Console chat basic");
            Console.WriteLine(" 2) Chat with DateTime plugin");
            Console.WriteLine(" 3) Chat with memory history");
            Console.WriteLine(" 4) Chat with dynamic web content");
            Console.WriteLine(" 5) Embedding similarity demo");
            Console.WriteLine(" 6) Chat with in-memory RAG");
            Console.WriteLine(" 7) Chat with agentic RAG");
            Console.WriteLine(" 8) Exit");
            Console.Write("Enter choice (1-8): ");
            if (!int.TryParse(Console.ReadLine(), out int choice))
            {
                Console.WriteLine("Invalid input. Please enter a number between 1 and 8.");
                continue;
            }

            bool stayInSample = true;
            switch (choice)
            {
                case 1:
                    while (stayInSample) stayInSample = !await Sample01.RunAsync(config);
                    break;
                case 2:
                    while (stayInSample) stayInSample = !await Sample02.RunAsync(config);
                    break;
                case 3:
                    while (stayInSample) stayInSample = !await Sample03.RunAsync(config);
                    break;
                case 4:
                    while (stayInSample) stayInSample = !await Sample04.RunAsync(config);
                    break;
                case 5:
                    while (stayInSample) stayInSample = !await Sample05.RunAsync(config);
                    break;
                case 6:
                    while (stayInSample) stayInSample = !await Sample06.RunAsync(config);
                    break;
                case 7:
                    while (stayInSample) stayInSample = !await Sample07.RunAsync(config);
                    break;
                case 8:
                    exitProgram = true;
                    Console.WriteLine("Exiting application.");
                    break;
                default:
                    Console.WriteLine("Invalid choice. Please enter a number between 1 and 7.");
                    break;
            }
        }
    }
}