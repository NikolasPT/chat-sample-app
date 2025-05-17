using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using System.Numerics.Tensors;
using Microsoft.Extensions.Configuration;


namespace SemanticKernelSamples;

/// <summary>
/// This sample demonstrates how to use the Semantic Kernel to generate embeddings for a given input
/// and compute similarities with a set of examples.
/// It uses the Azure OpenAI embedding service to generate embeddings and compute cosine similarity.
/// </summary>

internal static class Sample05
{
    public static async Task<bool> RunAsync(IConfiguration config)
    {

        var embeddingDeploymentName =   config["AzureAIFoundry:EmbeddingDeploymentName"]!;
        var embeddingEndpoint =         config["AzureAIFoundry:TextEmbedding3Large:Endpoint"]!;
        var embeddingApiKey =           config["AzureAIFoundry:TextEmbedding3Large:APIKey"]!;

        // Initialize Semantic Kernel with embedding service
        var builder = Kernel.CreateBuilder();
        builder.AddAzureOpenAITextEmbeddingGeneration(embeddingDeploymentName, embeddingEndpoint, embeddingApiKey);
        Kernel kernel = builder.Build();

        string input = "Sopra Steria er Danmarks bedste IT-virksomhed.";
        List<string> examples =
        [
            "Sopra Steria er Danmarks bedste IT-virksomhed.",
            "Sopra Steria is the best IT company in Denmark.",
            "What is the best IT company in Denmark?",
            "Sopra Steria is a leading IT company in Denmark.",
            "Sopra Steria is a top IT company in Denmark.",
            "Sopra Steria builds IT systems",
            "Sopra Steria is a company.",
            "Denmark is a country.",
            "København ligger i Danmark",
            "Min virksomhed hedder Sopra Steria.",
            "Jeg hedder Nikolas"
        ];

        Console.WriteLine();
        Console.WriteLine("Generating embeddings and computing similarities...");
        Console.WriteLine();
        Console.WriteLine("Input:");
        Console.WriteLine(input);
        Console.WriteLine();

        // Generate embeddings for the input and examples
        ITextEmbeddingGenerationService embeddingService = kernel.GetRequiredService<ITextEmbeddingGenerationService>();
        ReadOnlyMemory<float> inputEmbedding = (await embeddingService.GenerateEmbeddingsAsync([input])).First();
        List<ReadOnlyMemory<float>> exampleEmbeddings = [.. await embeddingService.GenerateEmbeddingsAsync(examples)];

        // Compute cosine similarity for each example and store results in a list
        List<(float score, string text)> similarityResults = [];
        for (int i = 0; i < examples.Count; i++)
        {
            float score = TensorPrimitives.CosineSimilarity(exampleEmbeddings[i].Span, inputEmbedding.Span);
            similarityResults.Add((score, examples[i]));
        }

        // Sort the results in descending order by score
        var similarities = similarityResults.OrderByDescending(x => x.score);

        // Display the results
        Console.WriteLine("Similarity\tExample");
        foreach (var (score, text) in similarities)
        {
            Console.WriteLine($"{score:F6}\t{text}");
        }
        Console.WriteLine();
        Console.WriteLine("Type 'exit' to return to menu or any other key to continue...");
        var key = Console.ReadKey();
        if (key.KeyChar == 'e' || key.KeyChar == 'E') // A bit simplistic, but works for this console app
        {
            string remaining = Console.ReadLine() ?? "";
            if (remaining.Equals("xit", StringComparison.CurrentCultureIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

}