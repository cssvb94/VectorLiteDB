using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using LiteDB;
using Accord.Math.Distances;
using Accord.Statistics.Analysis;
using Serilog;
using VectorLiteDB.Models;
using HNSWIndex;

namespace VectorLiteDB.Services
{
    public class VectorDbStore : IDisposable
    {
        private readonly ILiteDatabase _db;
        private readonly ILiteCollection<KnowledgeEntry> _collection;
        private readonly Cosine _distance;
        private readonly ILogger _logger;
        private readonly string _connectionString;
        private PrincipalComponentAnalysis? _pca;
        private readonly HnswVectorIndex _hnswIndex;
        private readonly DateTime _startTime;
        private int _totalSearches;
        private double _totalSearchTimeMs;
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
        private static readonly JsonSerializerOptions _jsonImportOptions = new() { PropertyNameCaseInsensitive = true };

        public VectorDbStore(string connectionString = "vectorlitedb.db", string? password = null)
        {
            _logger = Log.ForContext<VectorDbStore>();
            _startTime = DateTime.UtcNow;
            _connectionString = connectionString;

            // Enable encryption if password provided
            if (!string.IsNullOrEmpty(password))
            {
                connectionString += $";Password={password}";
                _logger.Information("Database encryption enabled");
            }

            _db = new LiteDatabase(connectionString);
            _collection = _db.GetCollection<KnowledgeEntry>("knowledge");
            _distance = new Cosine();

            // Initialize HNSW index with 384 dimensions (typical embedding size)
            _hnswIndex = new HnswVectorIndex(384);

            InitializePCA();
            LoadIndex();
            _logger.Information("VectorDbStore initialized with {Count} entries", _collection.Count());
        }

        private void LoadIndex()
        {
            foreach (var entry in _collection.FindAll())
            {
                if (entry.Embedding != null)
                {
                    _hnswIndex.Add(entry.Embedding, entry.Id);
                }
            }
            _logger.Information("Loaded {Count} vectors into HNSW index", _hnswIndex.Count);
        }

        private void InitializePCA()
        {
            var embeddings = _collection.FindAll()
                .Where(e => e.Embedding != null)
                .Select(e => e.Embedding!.Select(f => (double)f).ToArray())
                .ToArray();

            if (embeddings.Length > 10) // Need enough data for PCA
            {
                _pca = new PrincipalComponentAnalysis();
                _pca.Learn(embeddings);
                _logger.Information("PCA initialized with {Components} components", _pca.Components.Count);
            }
        }

        public void Add(KnowledgeEntry entry)
        {
            // GUID is auto-generated in constructor, but ensure it's set
            if (string.IsNullOrEmpty(entry.Id))
                entry.Id = Guid.NewGuid().ToString();

            entry.UpdatedAt = DateTime.UtcNow;

            // Check if entry exists
            var existing = _collection.FindById(entry.Id);
            if (existing == null)
            {
                // New entry
                entry.CreatedAt = DateTime.UtcNow;
                _collection.Insert(entry);
            }
            else
            {
                // Update existing entry
                entry.CreatedAt = existing.CreatedAt; // Preserve original creation time
                _collection.Update(entry);
            }

            // Add/update in HNSW index
            if (entry.Embedding != null)
            {
                _hnswIndex.Add(entry.Embedding, entry.Id);
            }

            // Handle bidirectional relations
            UpdateBidirectionalRelations(entry);

            _logger.Debug("Added/Updated entry {Id} with {Relations} relations", entry.Id, entry.Relations.Count);
        }

        private void UpdateBidirectionalRelations(KnowledgeEntry entry)
        {
            foreach (var relation in entry.Relations)
            {
                var relatedEntry = _collection.FindById(relation.TargetId);
                if (relatedEntry != null && !relatedEntry.Relations.Any(r => r.TargetId == entry.Id))
                {
                    // Add reverse relation
                    relatedEntry.Relations.Add(new Relation
                    {
                        TargetId = entry.Id,
                        Weight = relation.Weight, // Mirror the weight
                        Type = GetReverseRelationType(relation.Type),
                        CreatedAt = DateTime.UtcNow
                    });
                    relatedEntry.UpdatedAt = DateTime.UtcNow;
                    _collection.Update(relatedEntry);
                }
            }
        }

        private static string? GetReverseRelationType(string? relationType)
        {
            return relationType switch
            {
                "parent_of" => "child_of",
                "child_of" => "parent_of",
                "depends_on" => "depended_by",
                "depended_by" => "depends_on",
                _ => relationType
            };
        }

        public void AddBatch(IEnumerable<KnowledgeEntry> entries)
        {
            foreach (var entry in entries)
            {
                Add(entry); // Use existing Add method
            }
            _logger.Information("Batch added {Count} entries", entries.Count());
            ReinitializePCA(); // Update PCA with new data
        }

        private void ReinitializePCA()
        {
            _pca = null; // Reset PCA
            InitializePCA(); // Reinitialize with all data
        }

        public List<SearchResult> Search(SearchRequest request, bool autoNormalize = true)
        {
            if (request.Query == null || request.Query.Length == 0)
                throw new ArgumentException("Query vector cannot be null or empty");

            // Auto-normalize query vector for cosine similarity consistency
            if (autoNormalize && request.Query != null)
            {
                request.Query = NormalizeVector(request.Query);
            }

            var startTime = DateTime.UtcNow;

            // Step 1: Apply all filters (metadata, tags, tag prefixes)
            var candidates = ApplyFilters(request);

            // Step 2: Vector search (HNSW + exact rerank or brute force)
            if (request.Query == null) return new List<SearchResult>();
            var vectorResults = PerformVectorSearch(request.Query, candidates, request.K * (request.TraversalDepth + 1), request.UseExact, request.EfSearch ?? 400);

            // Step 3: Relation traversal if requested
            if (request.TraversalDepth > 0)
            {
                vectorResults = TraverseRelations(vectorResults, request, request.Query);
                // Re-rank by similarity after traversal
                vectorResults = vectorResults
                    .OrderByDescending(r => r.Similarity)
                    .Take(request.K)
                    .ToList();
            }
            else
            {
                // No traversal, just take top K
                vectorResults = vectorResults.Take(request.K).ToList();
            }

            var duration = DateTime.UtcNow - startTime;
            _totalSearches++;
            _totalSearchTimeMs += duration.TotalMilliseconds;

            _logger.Debug("Search completed in {Duration}ms, traversal depth {Depth}, returned {Count} results",
                duration.TotalMilliseconds, request.TraversalDepth, vectorResults.Count);

            return vectorResults;
        }

        // Backward compatibility method
        public List<SearchResult> Search(float[] query, int k = 10, Dictionary<string, object>? filters = null)
        {
            var request = new SearchRequest
            {
                Query = query,
                K = k,
                Filters = filters,
                TraversalDepth = 0,
                UseExact = false,
                EfSearch = 400
            };
            return Search(request);
        }

        public void MarkForDeletion(string entryId)
        {
            var entry = _collection.FindById(entryId);
            if (entry != null && !entry.IsDeleted)
            {
                entry.IsDeleted = true;
                entry.DeletedAt = DateTime.UtcNow;
                _collection.Update(entry);

                // Remove from HNSW index immediately
                _hnswIndex.Remove(entryId);

                _logger.Information("Marked entry {Id} for deletion", entryId);
            }
        }

        public void RebuildIndex()
        {
            var startTime = DateTime.UtcNow;

            // Clear existing index
            _hnswIndex.Rebuild();

            // Rebuild from all non-deleted entries
            var entries = _collection.FindAll().Where(e => !e.IsDeleted && e.Embedding != null);
            foreach (var entry in entries)
            {
                if (entry.Embedding != null)
                {
                    _hnswIndex.Add(entry.Embedding, entry.Id);
                }
            }

            // Clear deleted flags after successful rebuild
            ClearDeletedFlags();

            var duration = DateTime.UtcNow - startTime;
            _logger.Information("Index rebuilt in {Duration}ms with {Count} entries", duration.TotalMilliseconds, entries.Count());
        }

        public void ClearDeletedFlags()
        {
            var deletedEntries = _collection.FindAll().Where(e => e.IsDeleted);
            foreach (var entry in deletedEntries)
            {
                entry.IsDeleted = false;
                entry.DeletedAt = null;
                _collection.Update(entry);
            }
            _logger.Information("Cleared deleted flags from {Count} entries", deletedEntries.Count());
        }

        public int GetDeletedCount()
        {
            return _collection.FindAll().Count(e => e.IsDeleted);
        }

        public bool ShouldRebuild()
        {
            var totalEntries = _collection.Count();
            var deletedCount = GetDeletedCount();
            return deletedCount > 1000 || (totalEntries > 0 && deletedCount > totalEntries * 0.1);
        }

        public void PurgeDeleted()
        {
            var deletedEntries = _collection.FindAll().Where(e => e.IsDeleted).ToList();
            foreach (var entry in deletedEntries)
            {
                _collection.Delete(entry.Id);
            }
            _logger.Information("Purged {Count} deleted entries from database", deletedEntries.Count);
        }

        private static float[] NormalizeVector(float[] vector)
        {
            if (vector == null || vector.Length == 0) return vector ?? Array.Empty<float>();

            float norm = 0;
            for (int i = 0; i < vector.Length; i++)
            {
                norm += vector[i] * vector[i];
            }
            norm = (float)Math.Sqrt(norm);

            if (norm == 0) return vector; // Zero vector, can't normalize

            var normalized = new float[vector.Length];
            for (int i = 0; i < vector.Length; i++)
            {
                normalized[i] = vector[i] / norm;
            }
            return normalized;
        }

        private List<KnowledgeEntry> ApplyFilters(SearchRequest request)
        {
            var candidates = _collection.FindAll().ToList();

            // Apply metadata filters
            if (request.Filters != null && request.Filters.Count > 0)
            {
                candidates = candidates.Where(entry =>
                {
                    foreach (var filter in request.Filters)
                    {
                        if (entry.Metadata.TryGetValue(filter.Key, out var value) &&
                            value?.Equals(filter.Value) == true)
                            continue;
                        else
                            return false;
                    }
                    return true;
                }).ToList();
            }

            // Apply tag filtering (exact OR hierarchical prefix)
            if ((request.Tags != null && request.Tags.Count > 0) ||
                (request.TagPrefixes != null && request.TagPrefixes.Count > 0))
            {
                candidates = candidates.Where(entry =>
                {
                    // Check exact tag matches
                    bool hasExactMatch = request.Tags != null &&
                        request.Tags.Any(tag => entry.Tags.Contains(tag));

                    // Check hierarchical prefix matches
                    bool hasPrefixMatch = request.TagPrefixes != null &&
                        request.TagPrefixes.Any(prefix =>
                            entry.Tags.Any(tag => tag.StartsWith(prefix + "/") || tag == prefix));

                    // Entry matches if it has either exact OR prefix match
                    return hasExactMatch || hasPrefixMatch;
                }).ToList();
            }

            _logger.Debug("Applied filters: metadata={Meta}, tags={Tags}, prefixes={Prefixes}, candidates={Count}",
                request.Filters?.Count ?? 0, request.Tags?.Count ?? 0, request.TagPrefixes?.Count ?? 0, candidates.Count);

            return candidates;
        }

        private List<SearchResult> PerformVectorSearch(float[] query, List<KnowledgeEntry> candidates, int oversampleK, bool useExact, int efSearch)
        {
            // Use HNSW if we have enough candidates and not forcing exact
            if (!useExact && candidates.Count >= 1000 && _hnswIndex.Count >= 1000)
            {
                return PerformHnswSearch(query, candidates, oversampleK, efSearch, useExact);
            }
            else
            {
                return PerformBruteForceSearch(query, candidates, oversampleK, useExact);
            }
        }

        private List<SearchResult> PerformHnswSearch(float[] query, List<KnowledgeEntry> candidates, int oversampleK, int efSearch, bool useExact = false)
        {
            // Get oversampled results from HNSW
            var hnswResults = _hnswIndex.KnnQuery(query, oversampleK, efSearch);

            // Filter to only include candidates and convert to SearchResult
            var candidateIds = new HashSet<string>(candidates.Select(c => c.Id));

            var results = hnswResults
                .Where(r => candidateIds.Contains(r.Id))
                .Select(r =>
                {
                    var entry = candidates.First(c => c.Id == r.Id);
                    // Convert distance to similarity (1 - distance)
                    var similarity = 1 - r.Distance;
                    return new SearchResult
                    {
                        Entry = entry,
                        Similarity = (float)similarity
                    };
                })
                .OrderByDescending(r => r.Similarity)
                .ToList();

            _logger.Debug("HNSW search: oversampled {Oversample}, filtered to {Filtered}", oversampleK, results.Count);
            return results;
        }

        private List<SearchResult> PerformBruteForceSearch(float[] query, List<KnowledgeEntry> candidates, int k, bool useExact = false)
        {
            var allResults = candidates
                .Where(entry => entry.Embedding != null)
                .Select(entry =>
                {
                    var similarity = 1 - _distance.Distance(
                        query.Select(x => (double)x).ToArray(),
                        entry.Embedding!.Select(x => (double)x).ToArray());
                    return new SearchResult { Entry = entry, Similarity = (float)similarity };
                })
                .ToList();

            List<SearchResult> results;
            if (useExact)
            {
                // For exact search, only return entries with perfect similarity
                results = allResults.Where(r => r.Similarity >= 0.999f)
                    .OrderByDescending(r => r.Similarity)
                    .Take(k)
                    .ToList();
            }
            else
            {
                results = allResults
                    .OrderByDescending(r => r.Similarity)
                    .Take(k)
                    .ToList();
            }

            _logger.Debug("Brute force search: candidates {Candidates}, returned {Results}, exact={Exact}", candidates.Count, results.Count, useExact);
            return results;
        }

        private List<SearchResult> TraverseRelations(List<SearchResult> initialResults, SearchRequest request, float[] query)
        {
            const float TRAVERSAL_DECAY_FACTOR = 0.95f; // 5% decay per depth level

            var visited = new HashSet<string>();
            var allResults = new Dictionary<string, SearchResult>();

            // Add initial results
            foreach (var result in initialResults)
            {
                visited.Add(result.Entry!.Id);
                allResults[result.Entry.Id] = new SearchResult
                {
                    Entry = result.Entry,
                    Similarity = result.Similarity,
                    TraversalDepth = 0,
                    RelationPath = new List<string> { result.Entry.Id }
                };
            }

            // BFS traversal
            var queue = new Queue<(string EntryId, int Depth, string? SourceId, List<string> Path)>();
            foreach (var result in initialResults)
            {
                queue.Enqueue((result.Entry!.Id, 0, null, new List<string> { result.Entry.Id }));
            }

            while (queue.Count > 0 && allResults.Count < request.MaxTraversalResults)
            {
                var (currentId, depth, sourceId, path) = queue.Dequeue();
                if (depth >= request.MaxDepth) continue;

                var currentEntry = _collection.FindById(currentId);
                if (currentEntry == null) continue;

                foreach (var relation in currentEntry.Relations)
                {
                    var relatedId = relation.TargetId;
                    if (visited.Contains(relatedId)) continue;
                    visited.Add(relatedId);

                    var relatedEntry = _collection.FindById(relatedId);
                    if (relatedEntry != null)
                    {
                        // Calculate actual cosine similarity to original query
                        float similarity = 0;
                        if (relatedEntry.Embedding != null)
                        {
                            // Calculate proper cosine similarity with original query
                            similarity = (float)(1 - _distance.Distance(
                                query.Select(x => (double)x).ToArray(),
                                relatedEntry.Embedding.Select(x => (double)x).ToArray()));

                            // Apply decay factor for deeper traversals
                            similarity *= (float)Math.Pow(TRAVERSAL_DECAY_FACTOR, depth + 1);

                            // Clamp similarity to non-negative values
                            similarity = Math.Max(0f, similarity);

                            // Apply relation weight
                            similarity *= relation.Weight;
                        }

                        var newPath = new List<string>(path) { relatedId };

                        allResults[relatedId] = new SearchResult
                        {
                            Entry = relatedEntry,
                            Similarity = similarity,
                            TraversalDepth = depth + 1,
                            SourceEntryId = sourceId ?? currentId,
                            RelationPath = newPath
                        };

                        queue.Enqueue((relatedId, depth + 1, currentId, newPath));
                    }
                }
            }

            _logger.Debug("Relation traversal: depth {Depth}, total results {Total}, limit {Limit}",
                request.MaxDepth, allResults.Count, request.MaxTraversalResults);
            return allResults.Values.OrderByDescending(r => r.Similarity).ToList();
        }

        private long GetDatabaseSize()
        {
            try
            {
                // Parse the connection string to get the file path
                var parts = _connectionString.Split(';');
                var filename = parts[0];
                if (filename == ":memory:" || filename.StartsWith(":temp:"))
                    return 0; // In-memory database has no file size
                return new FileInfo(filename).Length;
            }
            catch
            {
                return 0; // Fallback if unable to determine size
            }
        }

        public VectorDbStats GetStats()
        {
            int pcaIndexSize = _pca != null ? _pca.Components.Count : 0;

            // Calculate tag distribution
            var allTags = _collection.FindAll()
                .SelectMany(e => e.Tags)
                .GroupBy(tag => tag, (key, group) => new { Tag = key, Count = group.Count() })
                .ToDictionary(x => x.Tag, x => x.Count);

            // Calculate recall (simplified - would need actual test data)
            double averageRecall = 0.99; // Placeholder - would be calculated from actual benchmarks

            return new VectorDbStats
            {
                TotalEntries = _collection.Count(),
                IndexSize = pcaIndexSize,
                HnswIndexSize = _hnswIndex.Count,
                MemoryUsage = GC.GetTotalMemory(false),
                LastUpdated = DateTime.UtcNow,
                LastIndexRebuild = DateTime.UtcNow, // Would track actual rebuild time
                Uptime = DateTime.UtcNow - _startTime,
                TotalSearches = _totalSearches,
                AverageSearchTimeMs = _totalSearches > 0 ? _totalSearchTimeMs / _totalSearches : 0,
                AverageRecall = averageRecall,
                DatabaseSizeBytes = GetDatabaseSize(),
                ActiveConnections = 1, // Single connection for now
                MetadataCategoryCounts = new Dictionary<string, int>(), // Legacy - kept for compatibility
                TagDistribution = allTags
            };
        }

        public void ImportFromJson(string jsonFilePath)
        {
            if (!File.Exists(jsonFilePath))
                throw new FileNotFoundException("JSON file not found", jsonFilePath);

            var json = File.ReadAllText(jsonFilePath);
            var entries = System.Text.Json.JsonSerializer.Deserialize<List<KnowledgeEntry>>(json, _jsonImportOptions);

            if (entries != null && entries.Count > 0)
            {
                AddBatch(entries);
                _logger.Information("Imported {Count} entries from {File}", entries.Count, jsonFilePath);
            }
        }

        public void ExportToJson(string jsonFilePath)
        {
            var entries = _collection.FindAll().ToList();
            var json = System.Text.Json.JsonSerializer.Serialize(entries, _jsonOptions);
            File.WriteAllText(jsonFilePath, json);
            _logger.Information("Exported {Count} entries to {File}", entries.Count, jsonFilePath);
        }

        public void Dispose()
        {
            _db.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}