# Semantic Kernel Samples

This repository contains a collection of C# console samples demonstrating how to use [Microsoft Semantic Kernel](https://github.com/microsoft/semantic-kernel) with Azure OpenAI and related plugins. The samples cover a range of scenarios, from basic chat to advanced Retrieval-Augmented Generation (RAG) and web search integration.

## Features

- **Basic Chat**: Simple chat with Azure OpenAI using Semantic Kernel.
- **Plugins**: Extend the AI with custom plugins (e.g., DateTime, Google Search).
- **Chat History**: Maintain conversation context across messages.
- **Web Search Integration**: Use Google Programmable Search to augment responses.
- **Embeddings & Similarity**: Generate and compare text embeddings using Azure OpenAI.
- **Retrieval-Augmented Generation (RAG)**: In-memory RAG with web content ingestion and context retrieval.
- **Agentic RAG**: Automated multi-step RAG workflow with web search, ingestion, and answer synthesis.

## Project Structure

- `Program.cs` — Main entry point with a menu to run each sample.
- `samples/` — Contains individual sample files:
  - `Sample01.cs`: Basic chat
  - `Sample02.cs`: DateTime plugin
  - `Sample03.cs`: Chat with memory/history
  - `Sample04.cs`: Chat with dynamic web content (Google Search)
  - `Sample05.cs`: Embedding similarity demo
  - `Sample06.cs`: In-memory RAG
  - `Sample07.cs`: Agentic RAG (work in progress)
- `appsettings.json` — Configuration for Azure OpenAI and embeddings
- `semantic-kernel-samples.csproj` — Project file with dependencies

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- Azure OpenAI resource (with deployment and API key)
- (Optional) Google Programmable Search Engine and API key for web search samples

## Setup

1. **Clone the repository**
   ```powershell
   git clone https://github.com/your-username/semantic-kernel-samples.git
   cd semantic-kernel-samples
   ```
2. **Configure secrets**
   - Update `appsettings.json` with your Azure OpenAI deployment names.
   - Add your API keys and endpoints using [User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets):
     ```powershell
     dotnet user-secrets set "AzureAIFoundry:GPT41:Endpoint" "<your-endpoint>"
     dotnet user-secrets set "AzureAIFoundry:GPT41:APIKey" "<your-api-key>"
     dotnet user-secrets set "AzureAIFoundry:TextEmbedding3Large:Endpoint" "<your-endpoint>"
     dotnet user-secrets set "AzureAIFoundry:TextEmbedding3Large:APIKey" "<your-api-key>"
     dotnet user-secrets set "Google:SearchEngineId" "<your-google-cx>"
     dotnet user-secrets set "Google:SearchConsoleAPIKey" "<your-google-api-key>"
     ```

3. **Restore and build**
   ```powershell
   dotnet build
   ```

4. **Run the samples**
   ```powershell
   dotnet run
   ```
   Follow the menu to select and run each sample.

## Security & Best Practices

- **Never commit secrets**: All API keys and sensitive data should be stored in user secrets or environment variables.
- **Follow Azure SDK and Semantic Kernel best practices** for authentication, error handling, and performance.

## References

- [Microsoft Semantic Kernel Documentation](https://learn.microsoft.com/en-us/semantic-kernel/)
- [Azure OpenAI Service](https://learn.microsoft.com/en-us/azure/ai-services/openai/)
- [Google Programmable Search Engine](https://programmablesearchengine.google.com/about/)

## License

This project is licensed under the MIT License.
