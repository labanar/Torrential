using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Torrential.Extensions.Indexing.Api;
using Torrential.Extensions.Indexing.Models;
using Torrential.Extensions.Indexing.Persistence;
using Torrential.Extensions.Indexing.Services;

namespace Torrential.Extensions.Indexing;

internal static class IndexingEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/indexers");

        group.MapGet("/", async (IIndexerRepository repo, CancellationToken ct) =>
        {
            var indexers = await repo.GetAllAsync(ct);
            var vms = indexers.Select(ToVm).ToArray();
            return Results.Ok(new { Data = vms });
        });

        group.MapGet("/{id:guid}", async (Guid id, IIndexerRepository repo, CancellationToken ct) =>
        {
            var indexer = await repo.GetByIdAsync(id, ct);
            if (indexer is null)
                return Results.NotFound(new { Error = new { Code = "NotFound", Message = "Indexer not found" } });

            return Results.Ok(new { Data = ToVm(indexer) });
        });

        group.MapPost("/", async (CreateIndexerRequest request, IIndexerRepository repo, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.BaseUrl))
                return Results.BadRequest(new { Error = new { Code = "ValidationError", Message = "Name and BaseUrl are required" } });

            var definition = new IndexerDefinition
            {
                Name = request.Name,
                Type = request.Type,
                BaseUrl = request.BaseUrl,
                AuthMode = request.AuthMode,
                ApiKey = request.ApiKey,
                Username = request.Username,
                Password = request.Password,
                Enabled = request.Enabled
            };

            var created = await repo.AddAsync(definition, ct);
            return Results.Created($"/indexers/{created.Id}", new { Data = ToVm(created) });
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateIndexerRequest request, IIndexerRepository repo, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.BaseUrl))
                return Results.BadRequest(new { Error = new { Code = "ValidationError", Message = "Name and BaseUrl are required" } });

            var definition = new IndexerDefinition
            {
                Id = id,
                Name = request.Name,
                Type = request.Type,
                BaseUrl = request.BaseUrl,
                AuthMode = request.AuthMode,
                ApiKey = request.ApiKey,
                Username = request.Username,
                Password = request.Password,
                Enabled = request.Enabled
            };

            var updated = await repo.UpdateAsync(definition, ct);
            if (updated is null)
                return Results.NotFound(new { Error = new { Code = "NotFound", Message = "Indexer not found" } });

            return Results.Ok(new { Data = ToVm(updated) });
        });

        group.MapDelete("/{id:guid}", async (Guid id, IIndexerRepository repo, CancellationToken ct) =>
        {
            var deleted = await repo.DeleteAsync(id, ct);
            if (!deleted)
                return Results.NotFound(new { Error = new { Code = "NotFound", Message = "Indexer not found" } });

            return Results.Ok(new { Data = new { Success = true } });
        });

        group.MapPost("/{id:guid}/test", async (Guid id, IIndexerSearchService searchService, CancellationToken ct) =>
        {
            var success = await searchService.TestIndexerAsync(id, ct);
            return Results.Ok(new { Data = new { Success = success } });
        });

        group.MapPost("/search", async (IndexerSearchRequest request, IIndexerSearchService searchService, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Query))
                return Results.BadRequest(new { Error = new { Code = "ValidationError", Message = "Query is required" } });

            var searchRequest = new SearchRequest
            {
                Query = request.Query,
                Category = request.Category,
                Limit = request.Limit
            };

            var results = await searchService.SearchAsync(searchRequest, ct);
            var vms = results.Select(ToSearchResultVm).ToArray();
            return Results.Ok(new { Data = vms });
        });
    }

    private static IndexerVm ToVm(IndexerDefinition def) => new()
    {
        Id = def.Id,
        Name = def.Name,
        Type = def.Type.ToString(),
        BaseUrl = def.BaseUrl,
        AuthMode = def.AuthMode.ToString(),
        Enabled = def.Enabled,
        DateAdded = def.DateAdded
    };

    private static SearchResultVm ToSearchResultVm(EnrichedSearchResult enriched)
    {
        var r = enriched.Result;
        var vm = new SearchResultVm
        {
            Title = r.Title,
            SizeBytes = r.SizeBytes,
            Seeders = r.Seeders,
            Leechers = r.Leechers,
            InfoHash = r.InfoHash,
            DownloadUrl = r.DownloadUrl,
            DetailsUrl = r.DetailsUrl,
            Category = r.Category,
            PublishDate = r.PublishDate,
            IndexerName = r.IndexerName
        };

        if (enriched.Metadata is not null)
        {
            vm.Metadata = new MetadataVm
            {
                Description = enriched.Metadata.Description,
                ExternalId = enriched.Metadata.ExternalId,
                ArtworkUrl = enriched.Metadata.ArtworkUrl,
                Genre = enriched.Metadata.Genre,
                Year = enriched.Metadata.Year
            };
        }

        return vm;
    }
}
