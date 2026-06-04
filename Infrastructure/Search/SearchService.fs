namespace BookManagement.Infrastructure.Search

open System
open System.Threading.Tasks
open Azure.Search.Documents
open Azure.Search.Documents.Models
open Microsoft.Extensions.Logging
open BookManagement.Domain
open BookManagement.Infrastructure.Abstractions

/// Azure AI Search implementation of ISearchService.
/// Index schema is defined by [SimpleField] / [SearchableField] attributes on the Book model.
/// Use scripts/CreateSearchIndex.fsx to create or recreate the index.
type SearchService(searchClient: SearchClient,
                   logger: ILogger<SearchService>) =

    interface ISearchService with

        member _.IndexDocument(book: Book) : Task<unit> =
            task {
                let doc   = book
                let batch = IndexDocumentsBatch.MergeOrUpload([| doc |])
                let! _    = searchClient.IndexDocumentsAsync(batch)
                logger.LogDebug("Indexed book {Id} in Azure Search", book.Id)
            }

        member _.DeleteDocument(id: string) : Task<unit> =
            task {
                let batch = IndexDocumentsBatch.Delete("id", [| id |])
                let! _    = searchClient.IndexDocumentsAsync(batch)
                logger.LogDebug("Deleted book {Id} from Azure Search", id)
            }

        member _.Search(req: SearchRequest) : Task<PagedResult<BookResponse>> =
            task {
                let options = SearchOptions()
                options.Skip  <- Nullable((req.Page - 1) * req.Size)
                options.Size  <- Nullable(req.Size)
                options.IncludeTotalCount <- true

                // Build OData filter expression
                let filters = System.Collections.Generic.List<string>()

                match req.Genre with
                | Some genre when not (String.IsNullOrWhiteSpace(genre)) ->
                    filters.Add(sprintf "genre eq '%s'" genre)
                | _ -> ()

                // Authors is a Collection(Edm.String) — use OData 'any' lambda syntax
                match req.Author with
                | Some author when not (String.IsNullOrWhiteSpace(author)) ->
                    filters.Add(sprintf "authors/any(a: a eq '%s')" author)
                | _ -> ()

                if filters.Count > 0 then
                    options.Filter <- String.Join(" and ", filters)

                let! results = searchClient.SearchAsync<Book>(req.Query, options)
                let items =
                    results.Value.GetResults()
                    |> Seq.map (fun result -> result.Document)
                    |> Seq.toList

                let total =
                    if results.Value.TotalCount.HasValue then results.Value.TotalCount.Value
                    else 0L

                return {
                    Items      = items
                    TotalCount = total
                    Page       = req.Page
                    Size       = req.Size
                }
            }
