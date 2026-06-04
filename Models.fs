namespace BookManagement.Domain

open System
open System.Collections.Generic
open Newtonsoft.Json
open Azure.Search.Documents.Indexes
open Azure.Search.Documents.Models

/// JWT
[<CLIMutable>]
type LoginRequest =
    {
        Username : string
        Password : string
    }

[<CLIMutable>]
type TokenResult =
    {
        Token     : string
        ExpiresAt : DateTime
    }

/// Core domain model — maps to both Cosmos DB document and Azure AI Search index.
/// Azure Search field schema is driven by [SimpleField] / [SearchableField] attributes.
/// FieldBuilder.Build<Book>() reads these attributes to auto-generate the index schema.
[<CLIMutable>]
type Book =
    {
        /// Cosmos DB + Search key field
        [<JsonProperty("id")>]
        [<SimpleField(IsKey = true)>]
        Id            : string

        [<JsonProperty("title")>]
        [<SearchableField>]
        Title         : string

        /// List of authors — supports multi-author books.
        /// Stored as Collection(Edm.String) in Azure Search.
        [<JsonProperty("authors")>]
        [<SearchableField>]
        Authors       : string list

        [<JsonProperty("isbn")>]
        [<SearchableField>]
        Isbn          : string

        [<JsonProperty("publisher")>]
        [<SearchableField>]
        Publisher     : string

        [<JsonProperty("publishedYear")>]
        [<SimpleField(IsFilterable = true, IsSortable = true)>]
        PublishedYear : int

        [<JsonProperty("genre")>]
        [<SimpleField(IsFilterable = true, IsFacetable = true)>]
        Genre         : string

        [<JsonProperty("description")>]
        [<SearchableField>]
        Description   : string

        [<JsonProperty("price")>]
        [<SimpleField(IsFilterable = true, IsSortable = true)>]
        Price         : decimal

        [<JsonProperty("stock")>]
        [<SimpleField(IsFilterable = true)>]
        Stock         : int

        /// Not indexed in Azure Search (internal timestamps)
        [<JsonProperty("createdAt")>]
        [<FieldBuilderIgnore>]
        CreatedAt     : DateTime

        [<JsonProperty("updatedAt")>]
        [<FieldBuilderIgnore>]
        UpdatedAt     : DateTime
    }

/// DTO for POST /api/books
[<CLIMutable>]
type CreateBookRequest =
    {
        Title         : string
        Authors       : string list
        Isbn          : string
        Publisher     : string
        PublishedYear : int
        Genre         : string
        Description   : string
        Price         : decimal
        Stock         : int
    }

/// DTO for PUT /api/books/{id}/{genre}
[<CLIMutable>]
type UpdateBookRequest =
    {
        Title         : string
        Authors       : string list
        Isbn          : string
        Publisher     : string
        PublishedYear : int
        Description   : string
        Price         : decimal
        Stock         : int
    }

/// API response — same shape as Book for simplicity
type BookResponse = Book

/// Query parameters for Azure AI Search
type SearchRequest =
    {
        Query  : string
        Genre  : string option
        Author : string option    // filter by author name (matches within Authors array)
        Page   : int
        Size   : int
    }

/// Generic paged response
type PagedResult<'T> =
    {
        Items      : 'T list
        TotalCount : int64
        Page       : int
        Size       : int
    }

/// API error response
type ErrorResponse =
    {
        Message : string
        Detail  : string option
    }

module Book =
    /// Create a new Book from a CreateBookRequest
    let fromCreateRequest (req: CreateBookRequest) : Book =
        let now = DateTime.UtcNow
        {
            Id            = Guid.NewGuid().ToString()
            Title         = req.Title
            Authors       = req.Authors
            Isbn          = req.Isbn
            Publisher     = req.Publisher
            PublishedYear = req.PublishedYear
            Genre         = req.Genre
            Description   = req.Description
            Price         = req.Price
            Stock         = req.Stock
            CreatedAt     = now
            UpdatedAt     = now
        }

    /// Apply an UpdateBookRequest to an existing Book
    let applyUpdate (req: UpdateBookRequest) (existing: Book) : Book =
        { existing with
            Title         = req.Title
            Authors       = req.Authors
            Isbn          = req.Isbn
            Publisher     = req.Publisher
            PublishedYear = req.PublishedYear
            Description   = req.Description
            Price         = req.Price
            Stock         = req.Stock
            UpdatedAt     = DateTime.UtcNow }

    // ── Azure Search document mapping ──────────────────────────────────────────
    // Centralised here so SearchService stays thin.

    let private tryConvertInt (v: obj) : int =
        match v with
        | :? int32 as i  -> i
        | :? int64 as l  -> int l
        | :? double as d -> int d
        | :? float32 as f -> int f
        | null -> 0
        | _ -> try Convert.ToInt32(v) with _ -> 0

    let private tryConvertDecimal (v: obj) : decimal =
        match v with
        | :? decimal as dec -> dec
        | :? double as d    -> decimal d
        | :? float32 as f   -> decimal f
        | :? int32 as i     -> decimal i
        | :? int64 as l     -> decimal l
        | null -> 0m
        | _ -> try Convert.ToDecimal(v) with _ -> 0m

    let private tryConvertString (v: obj) : string =
        match v with
        | :? string as s -> s
        | null -> ""
        | _ -> string v

    let private tryConvertStringList (v: obj) : string list =
        match v with
        | :? IEnumerable<obj> as col ->
            col |> Seq.choose (function :? string as s -> Some s | _ -> None) |> Seq.toList
        | :? string as s -> [s]
        | null -> []
        | _ -> []

module BookSearchIndexConversion = 
    /// Build a SearchDocument dictionary from a Book (for indexing)
    let toSearchModel (book: Book) =
        {
            Id            = book.Id
            Title         = book.Title
            Authors       = book.Authors
            Isbn          = book.Isbn
            Publisher     = book.Publisher
            PublishedYear = book.PublishedYear
            Genre         = book.Genre
            Description   = book.Description
            Price         = book.Price
            Stock         = book.Stock
            CreatedAt     = book.CreatedAt
            UpdatedAt     = book.UpdatedAt
        }
