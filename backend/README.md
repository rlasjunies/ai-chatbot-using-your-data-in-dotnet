# AI Chatbot Using Your Data in .NET

A high-performance, AOT-compiled AI chatbot application built with .NET 10 that uses Retrieval-Augmented Generation (RAG) to answer questions about landmarks using Wikipedia data. The application features vector search powered by Pinecone and OpenAI embeddings, with an embedded web interface for easy deployment.

## 📋 Table of Contents

- [Overview](#overview)
- [Features](#features)
- [Architecture](#architecture)
- [Data Flow Diagrams](#data-flow-diagrams)
- [Technical Stack](#technical-stack)
- [Getting Started](#getting-started)
- [Development](#development)
- [Deployment](#deployment)
- [API Documentation](#api-documentation)
- [Configuration](#configuration)

## 🎯 Overview

This project demonstrates how to build an intelligent chatbot that can:
- Answer questions about famous landmarks using RAG (Retrieval-Augmented Generation)
- Perform semantic vector search across chunked Wikipedia articles
- Engage in natural conversations with function calling capabilities
- Self-host with a single native executable (AOT compilation)

### Key Concepts

**RAG (Retrieval-Augmented Generation)**: Instead of relying solely on the LLM's training data, the system retrieves relevant context from a knowledge base and includes it in the prompt, leading to more accurate and up-to-date responses.

**Vector Search**: Documents are converted to embeddings (numerical representations) and stored in a vector database. When a user asks a question, it's converted to an embedding and similar documents are retrieved.

**AOT Compilation**: Ahead-of-Time compilation produces a native executable with faster startup times and lower memory usage compared to JIT compilation.

## ✨ Features

- **🔍 Vector Search**: Semantic search using OpenAI embeddings and Pinecone vector database
- **💬 Interactive Chat**: Full conversation support with context retention
- **❓ RAG Q&A**: Question answering with retrieved context from Wikipedia
- **🔧 Index Manager**: Web-based interface to build and manage the vector index
- **🚀 AOT Compiled**: Single native executable for fast startup and low memory footprint
- **🌐 Embedded Frontend**: No separate web server needed - everything in one file
- **⚙️ Configurable**: Environment-based configuration with .env file support
- **🔐 Secure**: API keys stored in environment variables, never committed to source

## 🏗️ Architecture

### System Components

```
┌─────────────────────────────────────────────────────────────┐
│                      User's Browser                          │
│                   (React SPA - Embedded)                     │
└────────────────┬────────────────────────────────────────────┘
                 │ HTTP/JSON
                 │
┌────────────────▼────────────────────────────────────────────┐
│              ASP.NET Core Web API                            │
│  ┌──────────┐  ┌──────────┐  ┌─────────────┐              │
│  │ Frontend │  │   API    │  │   Index     │              │
│  │ Serving  │  │ Endpoints│  │  Management │              │
│  └──────────┘  └──────────┘  └─────────────┘              │
└───┬────────┬────────┬──────────┬─────────────┬─────────────┘
    │        │        │          │             │
    │        │        │          │             │
┌───▼────┐ ┌▼────────▼──┐  ┌────▼─────┐  ┌───▼──────────┐
│ SQLite │ │   OpenAI   │  │ Pinecone │  │  Wikipedia   │
│ (Local)│ │(Embeddings │  │ (Vector  │  │     API      │
│Content │ │    & Chat) │  │  Search) │  │ (Data Source)│
└────────┘ └────────────┘  └──────────┘  └──────────────┘
```

### Application Layers

1. **Presentation Layer**: Embedded React frontend served at `/ui`
2. **API Layer**: RESTful endpoints for search, chat, and index management
3. **Service Layer**: Business logic for RAG, vector search, and Wikipedia integration
4. **Data Layer**: Pinecone (vectors) + SQLite (document content)

## 📊 Data Flow Diagrams

### 1. Index Building Flow

```
┌─────────┐
│  User   │ Clicks "Create Index"
└────┬────┘
     │
     │ POST /api/index/build
     ▼
┌──────────────────┐
│ IndexBuilder     │
│ Service          │
└────┬─────────────┘
     │
     │ For each landmark:
     ├─────────────────────────────────┐
     │                                 │
     ▼                                 ▼
┌────────────────┐            ┌─────────────────┐
│ Wikipedia API  │            │ ArticleSplitter │
│ Fetch article  │───────────>│ Chunk into      │
└────────────────┘            │ sections        │
                              └────┬────────────┘
                                   │
                                   │ Chunks
                                   ▼
                              ┌─────────────────┐
                              │ OpenAI          │
                              │ Generate        │
                              │ Embeddings      │
                              └────┬────────────┘
                                   │
                    ┌──────────────┴───────────────┐
                    │                              │
                    ▼                              ▼
            ┌──────────────┐              ┌──────────────┐
            │  Pinecone    │              │   SQLite     │
            │ Store vectors│              │ Store chunks │
            └──────────────┘              └──────────────┘
```

### 2. Vector Search Flow

```
┌─────────┐
│  User   │ Enters search query
└────┬────┘
     │
     │ GET /api/search?query=...
     ▼
┌────────────────────┐
│ VectorSearchService│
└────┬───────────────┘
     │
     │ 1. Convert query to embedding
     ▼
┌──────────────┐
│   OpenAI     │
│  Embedding   │
│  Generator   │
└────┬─────────┘
     │
     │ 2. Search similar vectors
     ▼
┌──────────────┐
│  Pinecone    │
│  Query API   │
└────┬─────────┘
     │
     │ 3. Retrieve IDs
     ▼
┌────────────────┐
│ DocumentChunk  │
│     Store      │
│   (SQLite)     │
└────┬───────────┘
     │
     │ 4. Return matched chunks
     ▼
┌─────────┐
│  User   │ Sees search results
└─────────┘
```

### 3. RAG Question Answering Flow

```
┌─────────┐
│  User   │ Asks question
└────┬────┘
     │
     │ GET /api/ask?question=...
     ▼
┌─────────────────────┐
│ RagQuestionService  │
└────┬────────────────┘
     │
     │ 1. Search relevant context
     ▼
┌─────────────────────┐
│ VectorSearchService │
│ (Top 5 chunks)      │
└────┬────────────────┘
     │
     │ 2. Build RAG prompt
     │    "Based on this context: [chunks]
     │     Answer: [question]"
     ▼
┌──────────────┐
│   OpenAI     │
│ Chat API     │
│ (GPT Model)  │
└────┬─────────┘
     │
     │ 3. Return answer
     ▼
┌─────────┐
│  User   │ Receives answer
└─────────┘
```

### 4. Chat with Function Calling Flow

```
┌─────────┐
│  User   │ Sends message
└────┬────┘
     │
     │ POST /api/chat
     ▼
┌────────────────────┐
│ Chat Endpoint      │
│ + System Prompt    │
└────┬───────────────┘
     │
     │ With function: database_search_service
     ▼
┌──────────────┐
│   OpenAI     │
│ Chat API     │
└────┬─────────┘
     │
     ├─ If model decides to call function ─┐
     │                                      │
     │                                      ▼
     │                              ┌──────────────────┐
     │                              │ VectorSearchSvc  │
     │                              │ FindInDatabase() │
     │                              └────┬─────────────┘
     │                                   │
     │                              Returns results
     │◄──────────────────────────────────┘
     │
     │ Model generates final response
     ▼
┌─────────┐
│  User   │ Receives answer
└─────────┘
```

## 🛠️ Technical Stack

### Backend
- **.NET 10** - Latest .NET framework with AOT support
- **ASP.NET Core** - Web API framework
- **Pinecone** - Vector database for semantic search
- **OpenAI API** - Embeddings (text-embedding-3-small) and Chat (GPT-4)
- **SQLite** - Local storage for document chunks
- **Microsoft.Extensions.AI** - Unified AI client abstraction

### Frontend
- **React 18** (via CDN) - UI framework
- **Tailwind CSS** (via CDN) - Styling
- **Babel Standalone** - JSX transformation in browser

### Infrastructure
- **DotNetEnv** - .env file configuration
- **Source Generators** - AOT-compatible JSON serialization

## 🚀 Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- OpenAI API Key ([Get one here](https://platform.openai.com/api-keys))
- Pinecone API Key ([Get one here](https://www.pinecone.io/))
- Pinecone Index named `landmark-chunks` (512 dimensions)

### Quick Start

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd backend
   ```

2. **Configure environment variables**
   
   Edit the `.env` file:
   ```bash
   OPENAI_API_KEY=your-openai-key-here
   PINECONE_API_KEY=your-pinecone-key-here
   PORT=5108
   ```

3. **Run the application**
   ```bash
   dotnet run
   ```

4. **Open your browser**
   
   The application will automatically open at `http://localhost:5108/ui`

5. **Build the index**
   
   Navigate to "Index Manager" and click "Create Index" to populate the vector database with Wikipedia articles about landmarks.

## 💻 Development

### Project Structure

```
backend/
├── Program.cs                 # Application entry point
├── Startup.cs                 # Service configuration
├── EmbeddedFrontend.cs        # Embedded HTML pages
├── AppJsonSerializerContext.cs # JSON source generation
├── FunctionRegistry.cs        # AI function definitions
├── Models/
│   ├── Document.cs
│   ├── DocumentChunk.cs
│   └── IndexResponses.cs
├── Services/
│   ├── ArticleSplitter.cs     # Text chunking
│   ├── DocumentStore.cs       # SQLite storage
│   ├── DocumentChunkStore.cs
│   ├── IndexBuilder.cs        # Vector index creation
│   ├── VectorSearchService.cs # Search functionality
│   ├── RagQuestionService.cs  # RAG implementation
│   ├── WikipediaClient.cs     # Wikipedia API client
│   ├── WikipediaJsonContext.cs # JSON context for Wikipedia
│   └── PromptService.cs       # Prompt templates
├── Prompts/
│   ├── ChatSystemPrompt.txt
│   ├── HydePrompt.txt
│   └── RagSystemPrompt.txt
└── .env                       # Environment configuration
```

### Running in Development Mode

#### Visual Studio Code
1. Press **F5** to start debugging
2. Set breakpoints in any .cs file
3. Use Debug Console for REPL

#### Command Line
```bash
# Development mode (with hot reload)
dotnet watch run

# Standard run
dotnet run

# With specific port
PORT=8080 dotnet run
```

### Adding New Features

#### Adding a New API Endpoint

1. **Add endpoint in Program.cs**:
   ```csharp
   app.MapGet("/api/myendpoint", async () =>
   {
       return Results.Ok(new MyResponse("data"));
   });
   ```

2. **Create response type in Models/**:
   ```csharp
   public record MyResponse(string Data);
   ```

3. **Register in AppJsonSerializerContext.cs**:
   ```csharp
   [JsonSerializable(typeof(MyResponse))]
   ```

#### Adding a New Frontend Page

1. **Add HTML constant in EmbeddedFrontend.cs**:
   ```csharp
   public const string MyPageHtml = """
   <!DOCTYPE html>
   ...
   """;
   ```

2. **Register route in Program.cs**:
   ```csharp
   app.MapGet("/ui/mypage", () => 
       Results.Content(EmbeddedFrontend.MyPageHtml, "text/html"));
   ```

### Testing

```bash
# Run all tests
dotnet test

# With code coverage
dotnet test /p:CollectCoverage=true
```

### Code Quality

```bash
# Format code
dotnet format

# Analyze code
dotnet build /p:EnforceCodeStyleInBuild=true
```

## 📦 Deployment

### Building for Production

#### Standard Build
```bash
dotnet build -c Release
```

#### AOT Native Build
```bash
dotnet publish -c Release
```

Output location: `bin/Release/net10.0/win-x64/publish/ChatBot.exe`

### AOT Compilation Benefits

- **Faster Startup**: ~10x faster than JIT
- **Lower Memory**: Reduced baseline memory usage
- **Single Executable**: Everything bundled (except .env)
- **Smaller Size**: Trimmed unused code

### Deployment Options

#### 1. Self-Contained Executable

The published AOT executable is fully self-contained:

```bash
# Copy these files to your server:
ChatBot.exe              # Main executable
.env                     # Configuration (create on server)
contentstore.db          # SQLite database (auto-created)
```

#### 2. Windows Service

```bash
# Install as Windows Service
sc create ChatBotService binPath="C:\path\to\ChatBot.exe"
sc start ChatBotService
```

#### 3. Docker Container

Create `Dockerfile`:
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
WORKDIR /app
COPY bin/Release/net10.0/linux-x64/publish/ .
EXPOSE 5108
ENTRYPOINT ["./ChatBot"]
```

Build and run:
```bash
docker build -t chatbot .
docker run -p 5108:5108 -e OPENAI_API_KEY=xxx -e PINECONE_API_KEY=xxx chatbot
```

#### 4. Cloud Deployment

**Azure App Service**:
```bash
az webapp up --name my-chatbot --runtime "DOTNET:10.0"
```

**AWS Elastic Beanstalk**:
```bash
eb init -p "64bit Amazon Linux 2 v2.0.0 running .NET 10"
eb create chatbot-env
```

### Environment Variables in Production

**Never commit** `.env` to source control. Set environment variables directly:

**Windows**:
```powershell
$env:OPENAI_API_KEY = "your-key"
$env:PINECONE_API_KEY = "your-key"
```

**Linux/Mac**:
```bash
export OPENAI_API_KEY="your-key"
export PINECONE_API_KEY="your-key"
```

**Docker**:
```bash
docker run -e OPENAI_API_KEY=xxx -e PINECONE_API_KEY=xxx chatbot
```

## 📡 API Documentation

### Frontend Routes

| Route                     | Description                   |
| ------------------------- | ----------------------------- |
| `GET /ui`                 | Main index page with links    |
| `GET /ui/indexer`         | Index management interface    |
| `GET /ui/chat`            | Chat interface                |
| `GET /ui/question`        | Q&A interface                 |
| `GET /ui/searchchunks`    | Search chunks of articles     |
| `GET /ui/searchlandmarks` | Search landmark introductions |

### API Endpoints

#### Index Management

**Get Index Statistics**
```http
GET /api/index/list
```
Response:
```json
{
  "count": 325,
  "hasRecords": true
}
```

**Build Index**
```http
POST /api/index/build
```
Response:
```json
{
  "message": "Index built successfully",
  "recordCount": 52
}
```

#### Search

**Vector Search**
```http
GET /api/search?query=ancient+stone+structure
```
Response:
```json
[
  {
    "id": "stonehenge_intro_0",
    "title": "Stonehenge",
    "section": "Introduction",
    "chunkIndex": 0,
    "content": "Stonehenge is a prehistoric...",
    "sourcePageUrl": "https://en.wikipedia.org/wiki/Stonehenge"
  }
]
```

#### Question Answering

**Ask Question (RAG)**
```http
GET /api/ask?question=When+was+Stonehenge+built
```
Response:
```json
"Stonehenge was built in several stages between approximately 3000 BC and 2000 BC, with the most famous stone circle being erected around 2500 BC."
```

#### Chat

**Chat with AI**
```http
POST /api/chat
Content-Type: application/json

[
  {
    "role": "user",
    "contents": [
      {
        "$type": "text",
        "text": "Tell me about the Eiffel Tower"
      }
    ]
  }
]
```

## ⚙️ Configuration

### Environment Variables

| Variable           | Required | Default | Description                            |
| ------------------ | -------- | ------- | -------------------------------------- |
| `OPENAI_API_KEY`   | Yes      | -       | OpenAI API key for embeddings and chat |
| `PINECONE_API_KEY` | Yes      | -       | Pinecone API key for vector database   |
| `PORT`             | No       | 5108    | HTTP port to listen on                 |

### Pinecone Index Setup

1. Create a new index in Pinecone console
2. Name: `landmark-chunks`
3. Dimensions: `512`
4. Metric: `cosine`
5. Pod Type: `s1.x1` or higher

### Customizing Data Sources

Edit `SourceData.cs` to change the landmarks:

```csharp
public static readonly string[] LandmarkNames =
[
    "Your Custom Landmark 1",
    "Your Custom Landmark 2",
    // ...
];
```

## 🔧 Troubleshooting

### Common Issues

**Port already in use**
```bash
# Change port in .env
PORT=8080
```

**API key errors**
```bash
# Verify environment variables are set
echo $env:OPENAI_API_KEY
```

**AOT compilation warnings**
- WikipediaClient warnings are expected (only used during indexing)
- All runtime code is fully AOT-compatible

**Index not building**
- Check Pinecone index exists and is named `landmark-chunks`
- Verify API keys have correct permissions
- Check network connectivity

## 📝 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## 🙏 Acknowledgments

- Built as part of Dometrain's "AI Chatbot Using Your Data in .NET" course
- Uses OpenAI's GPT models and embedding API
- Vector search powered by Pinecone
- Wikipedia as the knowledge source

## 📚 Further Reading

- [RAG (Retrieval-Augmented Generation) Overview](https://arxiv.org/abs/2005.11401)
- [.NET Native AOT Deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
- [Microsoft.Extensions.AI Documentation](https://devblogs.microsoft.com/dotnet/introducing-microsoft-extensions-ai-preview/)
- [Pinecone Vector Database](https://docs.pinecone.io/)
