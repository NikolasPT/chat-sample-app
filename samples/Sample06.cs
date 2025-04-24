using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using System.Numerics.Tensors;
using Microsoft.Extensions.Configuration;
// Add Azure OpenAI connector namespace for extension methods
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;

namespace SemanticKernelSamples
{
    internal static class Sample06
    {
        public static async Task RunAsync(IConfiguration config)
        {
            var embeddingDeploymentName = config["AI:OpenAI:EmbeddingDeploymentName"];
            var endpoint = config["AI:OpenAI:Endpoint"];
            var apiKey = config["AI:OpenAI:APIKey"];

            // Initialize Semantic Kernel with embedding service
            var builder = Kernel.CreateBuilder();
            // Use null-forgiving operator for config values
            builder.AddAzureOpenAITextEmbeddingGeneration(embeddingDeploymentName!, endpoint!, apiKey!);
            var kernel = builder.Build();

            var input = "What is a reptile?";
            var examples = new[]
            {
                "What is a reptile?",
                "¿Qué es un reptil?",
                "Was ist ein Reptil?",
                "A turtle is a reptile.",
                "Eidechse ist ein Beispiel für Reptilien.",
                "Crocodiles, lizards, snakes, and turtles are all examples.",
                "A frog is green.",
                "A grass is green.",
                "A cat is a mammal.",
                "A dog is a man's best friend.",
                "My best friend is Mike.",
                "I'm working at Inetum Polska since 2013."
            };

            Console.WriteLine("Generating embeddings and computing similarities...");
            var embeddingService = kernel.GetRequiredService<ITextEmbeddingGenerationService>();
            var inputEmbedding = (await embeddingService.GenerateEmbeddingsAsync(new[] { input })).First();
            var exampleEmbeddings = (await embeddingService.GenerateEmbeddingsAsync(examples)).ToArray();

            var similarities = examples
                .Select((text, i) => (
                    score: TensorPrimitives.CosineSimilarity(exampleEmbeddings[i].Span, inputEmbedding.Span),
                    text
                ))
                .OrderByDescending(x => x.score);

            Console.WriteLine("Similarity\tExample");
            foreach (var (score, text) in similarities)
            {
                Console.WriteLine($"{score:F6}\t{text}");
            }
        }
    }
}