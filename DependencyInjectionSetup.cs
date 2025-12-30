using System;
using ChatBot.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pinecone;

namespace ChatBot;

static class DependencyInjectionSetup
{
    public static void ConfigureServices(WebApplicationBuilder builder)
    {
        var openAiKey = builder.RequireEnv("OPENAI_API_KEY");
        
        // Read vector store provider from configuration (default to SqliteVec)
        var vectorProvider = builder.Configuration["VectorStore:Provider"] ?? "SqliteVec";
        Console.WriteLine($"Using vector store provider: {vectorProvider}");

        builder.Services.AddSingleton<StringEmbeddingGenerator>(s => new OpenAI.Embeddings.EmbeddingClient(
                model: "text-embedding-3-small",
                apiKey: openAiKey
            ).AsIEmbeddingGenerator());

        // Configure vector store based on provider
        if (vectorProvider.Equals("Pinecone", StringComparison.OrdinalIgnoreCase))
        {
            var pineconeKey = builder.RequireEnv("PINECONE_API_KEY");
            builder.Services.AddSingleton<IndexClient>(s => new PineconeClient(pineconeKey).Index("landmark-chunks"));
            builder.Services.AddSingleton<DocumentChunkStore>();
            
            builder.Services.AddSingleton<PineconeVectorStore>();
            builder.Services.AddSingleton<IVectorSearchService>(sp => sp.GetRequiredService<PineconeVectorStore>());
            builder.Services.AddSingleton<IVectorStore>(sp => sp.GetRequiredService<PineconeVectorStore>());
            
            // Legacy support
            builder.Services.AddSingleton<VectorSearchService>();
            
            Console.WriteLine("✓ Pinecone vector store configured");
        }
        else if (vectorProvider.Equals("SqliteVec", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddSingleton<SqliteVectorStore>();
            builder.Services.AddSingleton<IVectorSearchService>(sp => sp.GetRequiredService<SqliteVectorStore>());
            builder.Services.AddSingleton<IVectorStore>(sp => sp.GetRequiredService<SqliteVectorStore>());
            
            Console.WriteLine("✓ SQLite-vec vector store configured");
        }
        else
        {
            throw new InvalidOperationException($"Unknown vector store provider: {vectorProvider}. Valid options: Pinecone, SqliteVec");
        }
        
        // Register IndexBuilder (works with any IVectorStore)
        builder.Services.AddSingleton<IndexBuilder>();

        builder.Services.AddLogging(logging => logging.AddConsole().SetMinimumLevel(LogLevel.Information));

        builder.Services.AddSingleton<ILoggerFactory>(sp =>
            LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information)));

        builder.Services.AddSingleton<IChatClient>(sp =>
         {
             var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
             var client = new OpenAI.Chat.ChatClient(
                  "gpt-5-mini",
                  openAiKey).AsIChatClient();

             return new ChatClientBuilder(client)
                 .UseLogging(loggerFactory)
                 .UseFunctionInvocation(loggerFactory, c =>
                 {
                     c.IncludeDetailedErrors = true;
                 })
                 .Build(sp);
         });

        builder.Services.AddTransient<ChatOptions>(sp =>
        {
            var jsonOptions = new System.Text.Json.JsonSerializerOptions
            {
                TypeInfoResolverChain = { AppJsonSerializerContext.Default }
            };

            return new ChatOptions
            {
                Tools = FunctionRegistry.GetTools(sp, jsonOptions).ToList(),
            };
        });

        builder.Services.AddSingleton<WikipediaClient>();
        builder.Services.AddSingleton<RagQuestionService>();
        builder.Services.AddSingleton<ArticleSplitter>();
        builder.Services.AddSingleton<PromptStore>();
        builder.Services.AddSingleton<PromptService>();
    }
}
