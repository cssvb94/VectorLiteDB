# VectorLiteDB MCP Server Integration Guide

This document provides comprehensive guidance for integrating VectorLiteDB into MCP (Model Context Protocol) servers for AI agent knowledge storage and retrieval.

## Overview

VectorLiteDB provides 5 MCP tools that enable AI agents to store, search, and manage knowledge with advanced vector similarity search, hierarchical tagging, and graph-based relationships.

## MCP Tools Reference

### 1. store_knowledge

Store a knowledge entry with automatic embedding generation and optional metadata.

#### Parameters
- `content` (string, required): The knowledge content to store
- `tags` (string[], optional): Hierarchical tags for categorization
- `embedding` (float[], optional): Pre-computed embedding vector
- `metadata` (object, optional): Additional key-value metadata

#### Example Request
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "store_knowledge",
    "arguments": {
      "content": "Machine learning enables systems to learn and improve from experience without being explicitly programmed.",
      "tags": ["AI", "ML", "fundamentals"],
      "metadata": {
        "category": "educational",
        "difficulty": "beginner",
        "author": "system"
      }
    }
  }
}
```

#### Example Response
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "success": true,
    "data": {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "content": "Machine learning enables systems...",
      "tags": ["AI", "ML", "fundamentals"]
    }
  }
}
```

### 2. search_knowledge

Perform semantic search with advanced filtering and relation traversal.

#### Parameters
- `query` (string, required): Search query text
- `embedding` (float[], optional): Pre-computed query embedding
- `k` (int, optional): Number of results to return (default: 10)
- `useExact` (bool, optional): Use exact search instead of ANN (default: false)
- `traversalDepth` (int, optional): Depth for relation traversal (default: 0)
- `tagPrefixes` (string[], optional): Tag prefixes to filter by
- `maxTraversalResults` (int, optional): Maximum traversal results (default: 1000)

#### Example Request - Basic Search
```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "tools/call",
  "params": {
    "name": "search_knowledge",
    "arguments": {
      "query": "artificial intelligence machine learning",
      "k": 5
    }
  }
}
```

#### Example Request - Advanced Search with Relations
```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "tools/call",
  "params": {
    "name": "search_knowledge",
    "arguments": {
      "query": "neural network fundamentals",
      "k": 10,
      "traversalDepth": 2,
      "tagPrefixes": ["AI/ML"],
      "maxTraversalResults": 50
    }
  }
}
```

#### Example Response
```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": {
    "success": true,
    "data": [
      {
        "id": "550e8400-e29b-41d4-a716-446655440000",
        "content": "Machine learning enables systems...",
        "similarity": 0.89,
        "tags": ["AI", "ML", "fundamentals"],
        "traversalDepth": 0
      },
      {
        "id": "550e8400-e29b-41d4-a716-446655440001",
        "content": "Neural networks are computing systems...",
        "similarity": 0.76,
        "tags": ["AI", "neural-networks"],
        "traversalDepth": 1,
        "relationPath": ["prerequisite"]
      }
    ]
  }
}
```

### 3. add_relationship

Create a bidirectional weighted relationship between two knowledge entries.

#### Parameters
- `sourceId` (string, required): GUID of source knowledge entry
- `targetId` (string, required): GUID of target knowledge entry
- `weight` (float, optional): Relationship strength 0.1-2.0 (default: 1.0)
- `type` (string, optional): Relationship type (default: "related")

#### Example Request
```json
{
  "jsonrpc": "2.0",
  "id": 4,
  "method": "tools/call",
  "params": {
    "name": "add_relationship",
    "arguments": {
      "sourceId": "550e8400-e29b-41d4-a716-446655440000",
      "targetId": "550e8400-e29b-41d4-a716-446655440001",
      "weight": 1.5,
      "type": "prerequisite"
    }
  }
}
```

#### Example Response
```json
{
  "jsonrpc": "2.0",
  "id": 4,
  "result": {
    "success": true,
    "data": {
      "sourceId": "550e8400-e29b-41d4-a716-446655440000",
      "targetId": "550e8400-e29b-41d4-a716-446655440001",
      "weight": 1.5,
      "type": "prerequisite"
    }
  }
}
```

### 4. get_statistics

Retrieve database performance and usage statistics.

#### Parameters
None required

#### Example Request
```json
{
  "jsonrpc": "2.0",
  "id": 5,
  "method": "tools/call",
  "params": {
    "name": "get_statistics",
    "arguments": {}
  }
}
```

#### Example Response
```json
{
  "jsonrpc": "2.0",
  "id": 5,
  "result": {
    "success": true,
    "data": {
      "totalEntries": 1250,
      "deletedCount": 23,
      "hnswIndexSize": 256000,
      "memoryUsage": 4194304,
      "averageRecall": 0.99
    }
  }
}
```

### 5. rebuild_index

Rebuild the HNSW index for optimal search performance.

#### Parameters
None required

#### Example Request
```json
{
  "jsonrpc": "2.0",
  "id": 6,
  "method": "tools/call",
  "params": {
    "name": "rebuild_index",
    "arguments": {}
  }
}
```

#### Example Response
```json
{
  "jsonrpc": "2.0",
  "id": 6,
  "result": {
    "success": true,
    "data": {
      "durationMs": 1250.5,
      "message": "Index rebuilt successfully"
    }
  }
}
```

## Server Implementation

### Basic MCP Server Setup

```csharp
using VectorLiteDB.MCPServer;
using Serilog;

class Program
{
    static async Task Main(string[] args)
    {
        // Configure logging
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("logs/mcp_server_.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        // Create VectorLiteDB-backed MCP server
        using var server = new VectorLiteDBMCPServer("knowledge.db");
        await server.RunAsync();
    }
}
```

### Custom MCP Server Implementation

```csharp
public class CustomMCPServer
{
    private readonly KnowledgeTool _tool;
    private readonly ILogger<CustomMCPServer> _logger;

    public CustomMCPServer(string dbPath)
    {
        _tool = new KnowledgeTool(dbPath);
        _logger = LoggerFactory.Create(builder =>
            builder.AddSerilog()).CreateLogger<CustomMCPServer>();
    }

    public async Task<MCPResponse> HandleToolCallAsync(MCPRequest request)
    {
        return request.Tool switch
        {
            "store_knowledge" => await _tool.StoreKnowledgeAsync(request),
            "search_knowledge" => await _tool.SearchKnowledgeAsync(request),
            "add_relationship" => await _tool.AddRelationshipAsync(request),
            "get_statistics" => await _tool.GetStatisticsAsync(request),
            "rebuild_index" => await _tool.RebuildIndexAsync(request),
            _ => new MCPResponse { Success = false, Error = "Unknown tool" }
        };
    }

    public void Dispose() => _tool?.Dispose();
}
```

### MCP Protocol Handler

```csharp
public class MCPProtocolHandler
{
    private readonly CustomMCPServer _server;

    public MCPProtocolHandler(CustomMCPServer server)
    {
        _server = server;
    }

    public async Task<string> ProcessMessageAsync(string jsonMessage)
    {
        var message = JsonSerializer.Deserialize<MCPMessage>(jsonMessage);

        MCPResponse response = message.Method switch
        {
            "tools/call" => await _server.HandleToolCallAsync(message.Params),
            "tools/list" => ListAvailableTools(),
            _ => new MCPResponse { Success = false, Error = "Method not supported" }
        };

        return JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = message.Id,
            result = response.Success ? response.Data : null,
            error = response.Success ? null : new { message = response.Error }
        });
    }

    private MCPResponse ListAvailableTools()
    {
        return new MCPResponse
        {
            Success = true,
            Data = new[]
            {
                new { name = "store_knowledge", description = "Store knowledge entries" },
                new { name = "search_knowledge", description = "Semantic search with filtering" },
                new { name = "add_relationship", description = "Create knowledge relationships" },
                new { name = "get_statistics", description = "Database performance metrics" },
                new { name = "rebuild_index", description = "Optimize search performance" }
            }
        };
    }
}
```

## Integration Patterns

### AI Agent Knowledge Base

```csharp
public class AIAgentKnowledgeBase
{
    private readonly CustomMCPServer _server;

    public AIAgentKnowledgeBase()
    {
        _server = new CustomMCPServer("ai_agent_knowledge.db");
    }

    public async Task StoreLearning(string concept, string explanation, string[] tags)
    {
        var request = new MCPRequest
        {
            Tool = "store_knowledge",
            Parameters = new Dictionary<string, object>
            {
                ["content"] = explanation,
                ["tags"] = tags,
                ["metadata"] = new Dictionary<string, object>
                {
                    ["type"] = "learning",
                    ["concept"] = concept,
                    ["learned_at"] = DateTime.UtcNow
                }
            }
        };

        await _server.HandleToolCallAsync(request);
    }

    public async Task<string[]> FindRelatedConcepts(string query, int maxResults = 5)
    {
        var request = new MCPRequest
        {
            Tool = "search_knowledge",
            Parameters = new Dictionary<string, object>
            {
                ["query"] = query,
                ["k"] = maxResults,
                ["traversalDepth"] = 1
            }
        };

        var response = await _server.HandleToolCallAsync(request);
        if (!response.Success) return Array.Empty<string>();

        var results = (IEnumerable<dynamic>)response.Data;
        return results.Select(r => (string)r.content).ToArray();
    }

    public async Task LinkConcepts(string concept1, string concept2, string relationshipType)
    {
        // Find the entries first
        var search1 = await FindConceptsByContent(concept1);
        var search2 = await FindConceptsByContent(concept2);

        if (search1.Length > 0 && search2.Length > 0)
        {
            var request = new MCPRequest
            {
                Tool = "add_relationship",
                Parameters = new Dictionary<string, object>
                {
                    ["sourceId"] = search1[0], // Would need actual ID
                    ["targetId"] = search2[0], // Would need actual ID
                    ["type"] = relationshipType,
                    ["weight"] = 1.0
                }
            };

            await _server.HandleToolCallAsync(request);
        }
    }

    private async Task<string[]> FindConceptsByContent(string content)
    {
        // Implementation to find concept IDs by content
        return Array.Empty<string>();
    }

    public void Dispose() => _server?.Dispose();
}
```

### Multi-Agent Knowledge Sharing

```csharp
public class MultiAgentKnowledgeHub
{
    private readonly Dictionary<string, CustomMCPServer> _agentServers = new();

    public void RegisterAgent(string agentId, string dbPath)
    {
        _agentServers[agentId] = new CustomMCPServer(dbPath);
    }

    public async Task ShareKnowledge(string fromAgentId, string toAgentId, string knowledgeId)
    {
        // Export knowledge from source agent
        var exportRequest = new MCPRequest
        {
            Tool = "get_knowledge",
            Parameters = new Dictionary<string, object> { ["id"] = knowledgeId }
        };

        var sourceServer = _agentServers[fromAgentId];
        var knowledge = await sourceServer.HandleToolCallAsync(exportRequest);

        if (knowledge.Success)
        {
            // Import to destination agent
            var importRequest = new MCPRequest
            {
                Tool = "store_knowledge",
                Parameters = knowledge.Data
            };

            var destServer = _agentServers[toAgentId];
            await destServer.HandleToolCallAsync(importRequest);
        }
    }

    public async Task SearchAcrossAgents(string query, string[] agentIds)
    {
        var allResults = new List<dynamic>();

        foreach (var agentId in agentIds)
        {
            if (_agentServers.TryGetValue(agentId, out var server))
            {
                var searchRequest = new MCPRequest
                {
                    Tool = "search_knowledge",
                    Parameters = new Dictionary<string, object>
                    {
                        ["query"] = query,
                        ["k"] = 5
                    }
                };

                var results = await server.HandleToolCallAsync(searchRequest);
                if (results.Success)
                {
                    var agentResults = (IEnumerable<dynamic>)results.Data;
                    foreach (var result in agentResults)
                    {
                        result.agentId = agentId;
                        allResults.Add(result);
                    }
                }
            }
        }

        // Rank and return combined results
        return allResults.OrderByDescending(r => r.similarity).Take(10);
    }
}
```

## Performance Considerations

### Optimization Strategies

#### Batch Operations
```csharp
public async Task StoreBatchAsync(IEnumerable<KnowledgeEntry> entries)
{
    foreach (var entry in entries)
    {
        var request = new MCPRequest
        {
            Tool = "store_knowledge",
            Parameters = new Dictionary<string, object>
            {
                ["content"] = entry.Content,
                ["tags"] = entry.Tags,
                ["embedding"] = entry.Embedding
            }
        };

        await _server.HandleToolCallAsync(request);
    }

    // Rebuild index after batch
    var rebuildRequest = new MCPRequest { Tool = "rebuild_index" };
    await _server.HandleToolCallAsync(rebuildRequest);
}
```

#### Connection Pooling
```csharp
public class MCPConnectionPool
{
    private readonly SemaphoreSlim _semaphore;
    private readonly CustomMCPServer _server;

    public MCPConnectionPool(CustomMCPServer server, int maxConcurrent = 10)
    {
        _server = server;
        _semaphore = new SemaphoreSlim(maxConcurrent);
    }

    public async Task<MCPResponse> ExecuteToolAsync(MCPRequest request)
    {
        await _semaphore.WaitAsync();
        try
        {
            return await _server.HandleToolCallAsync(request);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
```

### Caching Strategies

#### Embedding Cache
```csharp
public class EmbeddingCache
{
    private readonly Dictionary<string, float[]> _cache = new();
    private readonly IEmbeddingService _embeddingService;

    public async Task<float[]> GetOrCreateEmbeddingAsync(string text)
    {
        if (_cache.TryGetValue(text, out var embedding))
            return embedding;

        embedding = await _embeddingService.GenerateEmbeddingAsync(text);
        _cache[text] = embedding;
        return embedding;
    }

    public void Clear() => _cache.Clear();
}
```

#### Query Result Cache
```csharp
public class QueryCache
{
    private readonly Dictionary<string, (List<dynamic> Results, DateTime Timestamp)> _cache = new();
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    public bool TryGetResults(string query, out List<dynamic> results)
    {
        if (_cache.TryGetValue(query, out var cached))
        {
            if (DateTime.UtcNow - cached.Timestamp < _cacheDuration)
            {
                results = cached.Results;
                return true;
            }
            else
            {
                _cache.Remove(query);
            }
        }

        results = null;
        return false;
    }

    public void CacheResults(string query, List<dynamic> results)
    {
        _cache[query] = (results, DateTime.UtcNow);
    }
}
```

## Error Handling

### MCP Error Responses

```csharp
public class MCPErrorHandler
{
    public MCPResponse HandleError(Exception ex)
    {
        var errorCode = ex switch
        {
            ArgumentException => -32602, // Invalid params
            InvalidOperationException => -32603, // Internal error
            KeyNotFoundException => -32602, // Not found
            _ => -32000 // Server error
        };

        return new MCPResponse
        {
            Success = false,
            Error = new MCPError
            {
                Code = errorCode,
                Message = ex.Message,
                Data = ex.StackTrace
            }
        };
    }
}

public class MCPError
{
    public int Code { get; set; }
    public string Message { get; set; }
    public string Data { get; set; }
}
```

### Retry Logic

```csharp
public class MCPRetryHandler
{
    public async Task<MCPResponse> ExecuteWithRetryAsync(
        Func<Task<MCPResponse>> operation,
        int maxRetries = 3,
        TimeSpan delay = default)
    {
        delay = delay == default ? TimeSpan.FromSeconds(1) : delay;

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                var result = await operation();
                if (result.Success) return result;

                // Retry on specific errors
                if (attempt < maxRetries - 1 &&
                    IsRetryableError(result.Error))
                {
                    await Task.Delay(delay * (attempt + 1));
                    continue;
                }

                return result;
            }
            catch (Exception ex)
            {
                if (attempt == maxRetries - 1)
                    return new MCPResponse { Success = false, Error = ex.Message };
            }
        }

        return new MCPResponse { Success = false, Error = "Max retries exceeded" };
    }

    private bool IsRetryableError(string error)
    {
        return error.Contains("timeout") ||
               error.Contains("connection") ||
               error.Contains("temporary");
    }
}
```

## Testing MCP Integration

### Unit Tests for MCP Tools

```csharp
[TestFixture]
public class MCPToolsTests
{
    private CustomMCPServer _server;

    [SetUp]
    public void Setup()
    {
        _server = new CustomMCPServer(":memory:");
    }

    [Test]
    public async Task StoreKnowledge_ValidInput_ReturnsSuccess()
    {
        var request = new MCPRequest
        {
            Tool = "store_knowledge",
            Parameters = new Dictionary<string, object>
            {
                ["content"] = "Test knowledge",
                ["tags"] = new[] { "test" }
            }
        };

        var response = await _server.HandleToolCallAsync(request);

        Assert.IsTrue(response.Success);
        Assert.IsNotNull(response.Data);
        Assert.IsNotNull(((dynamic)response.Data).id);
    }

    [Test]
    public async Task SearchKnowledge_WithResults_ReturnsMatches()
    {
        // First store some knowledge
        await StoreTestKnowledge("Machine learning fundamentals");

        var request = new MCPRequest
        {
            Tool = "search_knowledge",
            Parameters = new Dictionary<string, object>
            {
                ["query"] = "machine learning",
                ["k"] = 5
            }
        };

        var response = await _server.HandleToolCallAsync(request);

        Assert.IsTrue(response.Success);
        var results = (IEnumerable<dynamic>)response.Data;
        Assert.IsNotEmpty(results);
    }

    [Test]
    public async Task AddRelationship_ValidIds_CreatesConnection()
    {
        // Store two knowledge entries
        var id1 = await StoreTestKnowledge("Concept 1");
        var id2 = await StoreTestKnowledge("Concept 2");

        var request = new MCPRequest
        {
            Tool = "add_relationship",
            Parameters = new Dictionary<string, object>
            {
                ["sourceId"] = id1,
                ["targetId"] = id2,
                ["type"] = "related",
                ["weight"] = 1.0
            }
        };

        var response = await _server.HandleToolCallAsync(request);
        Assert.IsTrue(response.Success);
    }

    private async Task<string> StoreTestKnowledge(string content)
    {
        var request = new MCPRequest
        {
            Tool = "store_knowledge",
            Parameters = new Dictionary<string, object>
            {
                ["content"] = content,
                ["tags"] = new[] { "test" }
            }
        };

        var response = await _server.HandleToolCallAsync(request);
        return ((dynamic)response.Data).id;
    }
}
```

### Load Testing

```csharp
[Test]
public async Task Concurrent_Load_Test()
{
    using var server = new CustomMCPServer("load_test.db");

    const int concurrentUsers = 50;
    const int operationsPerUser = 10;

    var tasks = new List<Task>();

    for (int user = 0; user < concurrentUsers; user++)
    {
        tasks.Add(Task.Run(async () =>
        {
            for (int op = 0; op < operationsPerUser; op++)
            {
                // Mix of operations
                var operationType = op % 4;

                MCPRequest request = operationType switch
                {
                    0 => new MCPRequest
                    {
                        Tool = "store_knowledge",
                        Parameters = new Dictionary<string, object>
                        {
                            ["content"] = $"User {user} operation {op}",
                            ["tags"] = new[] { "load_test" }
                        }
                    },
                    1 => new MCPRequest
                    {
                        Tool = "search_knowledge",
                        Parameters = new Dictionary<string, object>
                        {
                            ["query"] = "load test",
                            ["k"] = 5
                        }
                    },
                    2 => new MCPRequest
                    {
                        Tool = "get_statistics",
                        Parameters = new Dictionary<string, object>()
                    },
                    _ => new MCPRequest
                    {
                        Tool = "rebuild_index",
                        Parameters = new Dictionary<string, object>()
                    }
                };

                var response = await server.HandleToolCallAsync(request);
                Assert.IsNotNull(response);
            }
        }));
    }

    await Task.WhenAll(tasks);

    // Verify final state
    var statsRequest = new MCPRequest
    {
        Tool = "get_statistics",
        Parameters = new Dictionary<string, object>()
    };

    var statsResponse = await server.HandleToolCallAsync(statsRequest);
    Assert.IsTrue(statsResponse.Success);

    var stats = statsResponse.Data;
    Assert.GreaterOrEqual(((dynamic)stats).totalEntries, concurrentUsers * operationsPerUser / 4);
}
```

## Deployment Examples

### Docker Container

```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app
EXPOSE 3000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["MCPKnowledgeServer.csproj", "."]
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MCPKnowledgeServer.dll"]
```

### Kubernetes Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: vectorlitedb-mcp
spec:
  replicas: 3
  selector:
    matchLabels:
      app: vectorlitedb-mcp
  template:
    metadata:
      labels:
        app: vectorlitedb-mcp
    spec:
      containers:
      - name: mcp-server
        image: vectorlitedb/mcp-server:latest
        ports:
        - containerPort: 3000
        env:
        - name: DATABASE_PATH
          value: "/data/knowledge.db"
        volumeMounts:
        - name: data-volume
          mountPath: /data
      volumes:
      - name: data-volume
        persistentVolumeClaim:
          claimName: vectorlitedb-data
```

### Cloud Deployment (AWS)

```csharp
// AWS Lambda MCP Server
public class LambdaMCPHandler
{
    private readonly CustomMCPServer _server;

    public LambdaMCPHandler()
    {
        // Use S3 for persistence
        var s3Client = new AmazonS3Client();
        _server = new CustomMCPServer("s3://my-bucket/knowledge.db");
    }

    public async Task<MCPResponse> HandleAsync(MCPRequest request, ILambdaContext context)
    {
        using var cts = new CancellationTokenSource(context.RemainingTime);
        cts.Token.ThrowIfCancellationRequested();

        return await _server.HandleToolCallAsync(request);
    }
}
```

This comprehensive guide provides everything needed to integrate VectorLiteDB into MCP servers for AI agent knowledge management.</content>
<parameter name="filePath">MCP_INTEGRATION_GUIDE.md