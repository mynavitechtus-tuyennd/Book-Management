namespace BookManagement.Infrastructure.CosmosDb

open System.Net
open System.Threading.Tasks
open Microsoft.Azure.Cosmos
open Microsoft.Azure.Cosmos.Linq
open Microsoft.Extensions.Logging
open BookManagement.Domain
open BookManagement.Infrastructure.Search
open BookManagement.Helpers

type BookRepository(cosmosClient: CosmosClient,
                    databaseName: string,
                    containerName: string,
                    searchService: ISearchService,
                    logger: ILogger<BookRepository>) =

    let container : Container = cosmosClient.GetContainer(databaseName, containerName)

    let getById' (id: string) (genre: string) : Task<BookResponse option> =
        task {
            try
                let! response = container.ReadItemAsync<Book>(id, PartitionKey(genre))
                return Some response.Resource
            with
            | :? CosmosException as ex when ex.StatusCode = HttpStatusCode.NotFound ->
                return None
            | ex ->
                logger.LogError(ex, "Error reading book {Id}", id)
                return None
        }

    interface IBookRepository with

        member _.GetAll (page: int) (size: int) : Task<PagedResult<BookResponse>> =
            task {
                let skip = (page - 1) * size

                // Collect all items with pagination
                let queryDef =
                    QueryDefinition("SELECT * FROM c OFFSET @skip LIMIT @take")
                        .WithParameter("@skip", skip)
                        .WithParameter("@take", size)

                use feedIterator  = container.GetItemQueryIterator<Book>(queryDef)
                let! items        = CommonHelper.collectPages feedIterator []

                // Count total
                let countDef = QueryDefinition("SELECT VALUE COUNT(1) FROM c")
                use countIterator  = container.GetItemQueryIterator<int>(countDef)
                let! totalCount   = CommonHelper.sumPages countIterator 0L

                return {
                    Items      = items
                    TotalCount = totalCount
                    Page       = page
                    Size       = size
                }
            }

        member _.GetById (id: string) (genre: string) : Task<BookResponse option> =
            getById' id genre

        member _.Create (req: CreateBookRequest) : Task<BookResponse> =
            task {
                let book = Book.fromCreateRequest req
                let! response = container.CreateItemAsync(book, PartitionKey(book.Genre))
                let created = response.Resource

                try
                    do! searchService.IndexDocument(created)
                with ex ->
                    logger.LogWarning(ex, "Failed to index book {Id} in Azure Search", created.Id)

                return created
            }

        member _.Update (id: string) (genre: string) (req: UpdateBookRequest) : Task<BookResponse option> =
            task {
                let! existing = getById' id genre
                match existing with
                | None -> return None
                | Some book ->
                    let updated = Book.applyUpdate req book
                    let! response = container.ReplaceItemAsync<Book>(updated, id, PartitionKey(genre))
                    let saved = response.Resource

                    try
                        do! searchService.IndexDocument(saved)
                    with ex ->
                        logger.LogWarning(ex, "Failed to update search index for book {Id}", id)

                    return Some saved
            }

        member _.Delete (id: string) (genre: string) : Task<bool> =
            task {
                try
                    let! _ = container.DeleteItemAsync<Book>(id, PartitionKey(genre))

                    try
                        do! searchService.DeleteDocument(id)
                    with ex ->
                        logger.LogWarning(ex, "Failed to remove book {Id} from search index", id)

                    return true
                with
                | :? CosmosException as ex when ex.StatusCode = HttpStatusCode.NotFound ->
                    return false
                | ex ->
                    logger.LogError(ex, "Error deleting book {Id}", id)
                    return false
            }
