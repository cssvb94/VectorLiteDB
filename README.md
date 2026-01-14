# VectorLiteDB

> HNSW ANN Vector Database with 99% Recall for AI Agent Knowledge Storage

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com/)

VectorLiteDB is a production-ready vector database that extends LiteDB with efficient approximate nearest neighbor (ANN) search capabilities. It provides semantic search with 99% recall, hierarchical tagging, and graph-based knowledge relationships - perfect for AI agent knowledge storage and retrieval.

## ✨ Features

- **🚀 HNSW Approximate Nearest Neighbor Search**: 99% recall with <50ms latency using oversample-rerank strategy
- **🏷️ Hierarchical Tag Prefix Matching**: Tag filtering with prefix support (`AI/ML` matches `AI/ML/NeuralNetworks`)
- **🔗 Bidirectional Relation Traversal**: Graph-based knowledge discovery with configurable depth and decay factors
- **⚖️ Weighted Relationships**: Relationship strengths from 0.1 (weak) to 2.0 (strong)
- **🗑️ Soft Delete with Auto-Rebuild**: Maintains performance while allowing data recovery
- **🏢 Enterprise Code Quality**: Proper resource disposal, null-safety, comprehensive testing
- **📊 Comprehensive Logging**: Serilog integration for production monitoring

## 🚀 Quick Start

### Installation

```bash
dotnet add package VectorLiteDB
```

### Basic Usage

```csharp
using VectorLiteDB;

// Create database
var store = new VectorDbStore("knowledge.db");

// Store knowledge with embeddings
var entry = new KnowledgeEntry
{
    Content = "Machine learning enables systems to learn from data without explicit programming",
    Embedding = GenerateEmbedding("Machine learning enables systems to learn from data"),
    Tags = new List<string> { "AI", "ML", "fundamentals" },
    Metadata = new Dictionary<string, object> { ["difficulty"] = "beginner" }
};

store.Add(entry);

// Semantic search
var results = store.Search(queryEmbedding, k: 10);

// Advanced search with filtering
var advancedResults = store.Search(new SearchRequest
{
    Query = queryEmbedding,
    K = 20,
    TraversalDepth = 2,
    TagPrefixes = new List<string> { "AI" },
    Filters = new Dictionary<string, object> { ["difficulty"] = "beginner" }
});
```

## 📚 Documentation

- **Complete API Documentation**: [VectorLiteDB_API_Documentation.md](VectorLiteDB_API_Documentation.md)
- **MCP Server Integration Guide**: See repository knowledge base for comprehensive MCP server implementation examples, production deployment patterns, and testing strategies.

## 🤖 MCP Server Integration

VectorLiteDB provides comprehensive MCP (Model Context Protocol) server integration for AI agents with 5 specialized tools for knowledge management.

### Available MCP Tools
- **`store_knowledge`**: Store knowledge entries with automatic embedding generation
- **`search_knowledge`**: Semantic search with advanced filtering and relation traversal
- **`add_relationship`**: Create bidirectional relationships between knowledge entries
- **`get_statistics`**: Database performance and usage metrics
- **`rebuild_index`**: Optimize search performance through index rebuild

### Quick MCP Setup
```csharp
// MCP server with VectorLiteDB
var knowledgeTool = new KnowledgeTool("mcp_knowledge.db");
var server = new MCPKnowledgeServer(knowledgeTool);
await server.StartAsync();
```

**📖 Complete MCP Integration Guide**: See [MCP_INTEGRATION_GUIDE.md](MCP_INTEGRATION_GUIDE.md) for comprehensive implementation details, JSON-RPC examples, server setup, testing strategies, and production deployment patterns.

## 🏗️ Core API

| Class | Description |
|-------|-------------|
| `VectorDbStore` | Main database interface for CRUD operations |
| `KnowledgeEntry` | Data model with content, embedding, tags, and relations |
| `SearchRequest` | Advanced search parameters with filtering and traversal |
| `SearchResult` | Search result with similarity score and traversal info |
| `Relation` | Weighted relationship between knowledge entries |

## 🔍 Search Features

```csharp
// Basic semantic search
var results = store.Search(embedding, k: 10);

// With metadata filters
var filtered = store.Search(embedding, k: 10,
    filters: new Dictionary<string, object> { ["category"] = "tutorial" });

// With hierarchical tag filtering
var tagged = store.Search(new SearchRequest
{
    Query = embedding,
    TagPrefixes = new List<string> { "AI/ML" }, // Matches AI/ML/*
    K = 20
});

// With relation traversal (knowledge graph)
var traversed = store.Search(new SearchRequest
{
    Query = embedding,
    TraversalDepth = 2, // Follow relationships 2 levels deep
    MaxTraversalResults = 100
});
```

## ⚡ Performance

| Operation | Latency | Notes |
|-----------|---------|-------|
| Store Knowledge | <10ms | Per entry with auto-embedding |
| Semantic Search | <50ms | 99% recall, HNSW with rerank |
| Relation Traversal | <100ms | Depth 2, 1000 max results |
| Index Rebuild | <5s | For 10k entries |

## 🏛️ Architecture

```
VectorLiteDB
├── LiteDB (Document Storage)
│   ├── KnowledgeEntry collection
│   ├── Metadata indexing
│   └── Transaction support
├── HNSW.NET (ANN Search)
│   ├── Cosine distance metric
│   ├── Oversample + rerank strategy
│   └── Automatic fallback to brute force
├── Accord.Math (Optional)
│   └── PCA quantization
└── Serilog (Logging)
    ├── Performance metrics
    └── Operation tracing
```

## ⚙️ Configuration

```csharp
// Basic configuration
var store = new VectorDbStore("knowledge.db");

// With encryption
var secureStore = new VectorDbStore("secure.db", password: "your-password");

// Sharded for large deployments
var shardedStore = new ShardedVectorDbStore(
    shardCount: 4,
    basePath: "/data/shards/");
```

## 📋 Requirements

- .NET 10.0+
- LiteDB 5.0.17+
- HNSWIndex 1.5.0+
- Accord.Math 3.8.0+

## 🧪 Testing

```bash
# Run all tests
dotnet test VectorLiteDB/VectorLiteDB.Tests/

# Run specific test suite
dotnet test --filter "FullyQualifiedName~ComprehensiveFeatureTests"
```

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Add tests for your changes
4. Ensure all tests pass (`dotnet test`)
5. Commit your changes (`git commit -m 'Add amazing feature'`)
6. Push to the branch (`git push origin feature/amazing-feature`)
7. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🆘 Support

- 📚 **API Documentation**: [VectorLiteDB_API_Documentation.md](VectorLiteDB_API_Documentation.md)
- 🤖 **MCP Integration**: Repository knowledge base contains comprehensive MCP server implementation guides, production deployment patterns, and testing strategies
- 🐛 **Issues**: [GitHub Issues](https://github.com/cssvb94/VectorLiteDB/issues)
- 💬 **Discussions**: [GitHub Discussions](https://github.com/cssvb94/VectorLiteDB/discussions)

---

**VectorLiteDB** - Enterprise-grade vector database for AI agent knowledge storage and retrieval.