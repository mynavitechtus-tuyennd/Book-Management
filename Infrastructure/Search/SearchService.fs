namespace BookManagement.Infrastructure.Search

open System
open System.Threading.Tasks
open Azure.Search.Documents
open Azure.Search.Documents.Models
open Azure.Search.Documents.Indexes
open Azure.Search.Documents.Indexes.Models
open Azure
open Microsoft.Extensions.Logging
open BookManagement.Domain

/// Azure AI Search index field names
module internal IndexFields =
    let [<Literal>] FieldId            = "id"
    let [<Literal>] FieldTitle         = "title"
    let [<Literal>] FieldAuthor        = "author"
    let [<Literal>] FieldIsbn          = "isbn"
    let [<Literal>] FieldPublisher     = "publisher"
    let [<Literal>] FieldPublishedYear = "publishedYear"
    let [<Literal>] FieldGenre         = "genre"
    let [<Literal>] FieldDescription   = "description"
    let [<Literal>] FieldPrice         = "price"
    let [<Literal>] FieldStock         = "stock"

open IndexFields

/// Maps a Book to an Azure Search SearchDocument
module internal Mapping =
    let toSearchDocument (book: Book) : SearchDocument =
        let doc = SearchDocument()
        doc.[FieldId]            <- book.Id
        doc.[FieldTitle]         <- book.Title
        doc.[FieldAuthor]        <- book.Author
        doc.[FieldIsbn]          <- book.Isbn
        doc.[FieldPublisher]     <- book.Publisher
        doc.[FieldPublishedYear] <- box book.PublishedYear
        doc.[FieldGenre]         <- book.Genre
        doc.[FieldDescription]   <- book.Description
        doc.[FieldPrice]         <- box (float book.Price)
        doc.[FieldStock]         <- box book.Stock
        doc

    let tryConvertInt (v: obj) : int =
        match v with
        | :? int32 as i -> i
        | :? int64 as l -> int l
        | :? double as d -> int d
        | :? float32 as f -> int f
        | null -> 0
        | _ -> try Convert.ToInt32(v) with _ -> 0

    let tryConvertDecimal (v: obj) : decimal =
        match v with
        | :? decimal as dec -> dec
        | :? double as d -> decimal d
        | :? float32 as f -> decimal f
        | :? int32 as i -> decimal i
        | :? int64 as l -> decimal l
        | null -> 0m
        | _ -> try Convert.ToDecimal(v) with _ -> 0m

    let tryConvertString (v: obj) : string =
        match v with
        | :? string as s -> s
        | null -> ""
        | _ -> string v

    let fromSearchDocument (doc: SearchDocument) : BookResponse option =
        try
            let get key = 
                let mutable value = null
                if doc.TryGetValue(key, &value) then value else null
            
            Some {
                Id            = get FieldId            |> tryConvertString
                Title         = get FieldTitle         |> tryConvertString
                Author        = get FieldAuthor        |> tryConvertString
                Isbn          = get FieldIsbn          |> tryConvertString
                Publisher     = get FieldPublisher     |> tryConvertString
                PublishedYear = get FieldPublishedYear |> tryConvertInt
                Genre         = get FieldGenre         |> tryConvertString
                Description   = get FieldDescription   |> tryConvertString
                Price         = get FieldPrice         |> tryConvertDecimal
                Stock         = get FieldStock         |> tryConvertInt
                CreatedAt     = DateTime.UtcNow
                UpdatedAt     = DateTime.UtcNow
            }
        with ex ->
            Console.WriteLine("Error mapping SearchDocument to BookResponse: {0}", ex.ToString())
            None

type SearchService(searchClient: SearchClient,
                   indexClient: SearchIndexClient,
                   indexName: string,
                   logger: ILogger<SearchService>) =

    /// Ensure the search index exists with correct schema
    member _.EnsureIndexExists() : Task<unit> =
        task {
            let index = SearchIndex(indexName)

            // Key field
            let idField = SearchField(FieldId, SearchFieldDataType.String)
            idField.IsKey <- true
            index.Fields.Add(idField)

            // Searchable text fields
            let addSearchable name =
                let f = SearchField(name, SearchFieldDataType.String)
                f.IsSearchable <- true
                index.Fields.Add(f)

            addSearchable FieldTitle
            addSearchable FieldAuthor
            addSearchable FieldIsbn
            addSearchable FieldPublisher
            addSearchable FieldDescription

            // Filterable fields
            let genreField = SearchField(FieldGenre, SearchFieldDataType.String)
            genreField.IsFilterable <- true
            genreField.IsFacetable  <- true
            index.Fields.Add(genreField)

            let yearField = SearchField(FieldPublishedYear, SearchFieldDataType.Int32)
            yearField.IsFilterable <- true
            yearField.IsSortable   <- true
            index.Fields.Add(yearField)

            let priceField = SearchField(FieldPrice, SearchFieldDataType.Double)
            priceField.IsFilterable <- true
            priceField.IsSortable   <- true
            index.Fields.Add(priceField)

            let stockField = SearchField(FieldStock, SearchFieldDataType.Int32)
            stockField.IsFilterable <- true
            index.Fields.Add(stockField)

            let! _ = indexClient.CreateOrUpdateIndexAsync(index)
            logger.LogInformation("Azure Search index '{IndexName}' ready", indexName)
        }

    interface ISearchService with

        member _.IndexDocument(book: Book) : Task<unit> =
            task {
                let doc   = Mapping.toSearchDocument book
                let batch = IndexDocumentsBatch.MergeOrUpload([| doc |])
                let! _    = searchClient.IndexDocumentsAsync(batch)
                logger.LogDebug("Indexed book {Id} in Azure Search", book.Id)
            }

        member _.DeleteDocument(id: string) : Task<unit> =
            task {
                let batch = IndexDocumentsBatch.Delete(FieldId, [| id |])
                let! _    = searchClient.IndexDocumentsAsync(batch)
                logger.LogDebug("Deleted book {Id} from Azure Search", id)
            }

        member _.Search(req: SearchRequest) : Task<PagedResult<BookResponse>> =
            task {
                let options = SearchOptions()
                options.Skip  <- Nullable((req.Page - 1) * req.Size)
                options.Size  <- Nullable(req.Size)
                options.IncludeTotalCount <- true

                match req.Genre with
                | Some genre when not (String.IsNullOrWhiteSpace(genre)) ->
                    options.Filter <- sprintf "genre eq '%s'" genre
                | _ -> ()

                let! results = searchClient.SearchAsync<SearchDocument>(req.Query, options)
                let items =
                    results.Value.GetResults()
                    |> Seq.choose (fun result -> Mapping.fromSearchDocument result.Document)
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
