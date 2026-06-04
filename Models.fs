namespace BookManagement.Domain

open System
open System.Collections
open System.Collections.Generic
open Newtonsoft.Json
open Azure.Search.Documents.Indexes
open Azure.Search.Documents.Models
open System.Text.Json.Serialization
open System.ComponentModel.DataAnnotations

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

/// Core domain model — maps to both Cosmos DB document.
/// Azure Search field schema is driven by [SimpleField] / [SearchableField] attributes.
/// FieldBuilder.Build<Book>() reads these attributes to auto-generate the index schema.
[<CLIMutable>]
type Book =
    {
        /// Cosmos DB key field
        [<JsonProperty("id")>]
        Id            : string

        [<JsonProperty("title")>]
        Title         : string

        /// List of authors — supports multi-author books.
        /// Stored as Collection(Edm.String) in Azure Search.
        [<JsonProperty("authors")>]
        Authors       : string list

        [<JsonProperty("isbn")>]
        Isbn          : string

        [<JsonProperty("publisher")>]
        Publisher     : string

        [<JsonProperty("publishedYear")>]
        PublishedYear : int

        [<JsonProperty("genre")>]
        Genre         : string

        [<JsonProperty("description")>]
        Description   : string

        [<JsonProperty("price")>]
        Price         : double

        [<JsonProperty("stock")>]
        Stock         : int

        [<JsonProperty("createdAt")>]
        CreatedAt     : DateTime

        [<JsonProperty("updatedAt")>]
        UpdatedAt     : DateTime
    }

[<CLIMutable>]
type BookSearchDocument =
    {
        [<JsonPropertyName("id")>]
        [<SimpleField(IsKey = true)>]
        Id            : string

        [<JsonPropertyName("title")>]
        [<SearchableField>]
        Title         : string

        /// List of authors — supports multi-author books.
        /// Stored as Collection(Edm.String) in Azure Search.
        [<JsonPropertyName("authors")>]
        [<SearchableField(IsFilterable = true)>]
        Authors       : string list

        [<JsonPropertyName("isbn")>]
        [<SearchableField>]
        Isbn          : string

        [<JsonPropertyName("publisher")>]
        [<SearchableField>]
        Publisher     : string

        [<JsonPropertyName("publishedYear")>]
        [<SimpleField(IsFilterable = true, IsSortable = true)>]
        PublishedYear : int

        [<JsonPropertyName("genre")>]
        [<SimpleField(IsFilterable = true, IsFacetable = true)>]
        Genre         : string

        [<JsonPropertyName("description")>]
        [<SearchableField>]
        Description   : string

        [<JsonPropertyName("price")>]
        [<SimpleField(IsFilterable = true, IsSortable = true)>]
        Price         : double

        [<JsonPropertyName("stock")>]
        [<SimpleField(IsFilterable = true)>]
        Stock         : int

        [<JsonPropertyName("createdAt")>]
        [<SimpleField(IsFilterable = true)>]
        CreatedAt     : DateTime

        [<JsonPropertyName("updatedAt")>]
        [<SimpleField(IsFilterable = true)>]
        UpdatedAt     : DateTime
    }


/// Custom validation attribute to ensure a list or collection is not empty
type RequireNonEmptyListAttribute() =
    inherit ValidationAttribute("At least one item is required.")
    override _.IsValid(value: obj) =
        match value with
        | null -> false
        | :? IEnumerable as col ->
            let enumerator = col.GetEnumerator()
            enumerator.MoveNext()
        | _ -> false

/// DTO for POST /api/books
[<CLIMutable>]
type CreateBookRequest =
    {
        [<Required(ErrorMessage = "Title is required")>]
        Title         : string

        [<RequireNonEmptyList(ErrorMessage = "At least one author is required")>]
        Authors       : string list

        [<Required(ErrorMessage = "Isbn is required")>]
        Isbn          : string

        [<Required(ErrorMessage = "Publisher is required")>]
        Publisher     : string

        [<Range(1800, 2100, ErrorMessage = "Published year must be between 1800 and 2100")>]
        PublishedYear : int

        [<Required(ErrorMessage = "Genre is required")>]
        Genre         : string

        [<Required(ErrorMessage = "Description is required")>]
        Description   : string

        [<Range(0.01, 1000000.0, ErrorMessage = "Price must be greater than 0")>]
        Price         : double

        [<Range(0, 1000000, ErrorMessage = "Stock cannot be negative")>]
        Stock         : int
    }

/// DTO for PUT /api/books/{id}/{genre}
[<CLIMutable>]
type UpdateBookRequest =
    {
        [<Required(ErrorMessage = "Title is required")>]
        Title         : string

        [<RequireNonEmptyList(ErrorMessage = "At least one author is required")>]
        Authors       : string list

        [<Required(ErrorMessage = "Isbn is required")>]
        Isbn          : string

        [<Required(ErrorMessage = "Publisher is required")>]
        Publisher     : string

        [<Range(1800, 2100, ErrorMessage = "Published year must be between 1800 and 2100")>]
        PublishedYear : int

        [<Required(ErrorMessage = "Description is required")>]
        Description   : string

        [<Range(0.01, 1000000.0, ErrorMessage = "Price must be greater than 0")>]
        Price         : double

        [<Range(0, 1000000, ErrorMessage = "Stock cannot be negative")>]
        Stock         : int
    }

/// API response — same shape as Book for simplicity
type BookResponse = Book

[<CLIMutable>]
type GetAllRequest =
    {
        Page: int option
        Size: int option
    }

/// Query parameters for Azure AI Search
[<CLIMutable>]
type SearchRequest =
    {
        Query  : string
        Genre  : string option
        Author : string option    // filter by author name (matches within Authors array)
        Page   : int
        Size   : int
    }

/// Query parameters for Cosmos DB Search
[<CLIMutable>]
type SearchDbRequest =
    {
        Title : string option
        Genre : string option
        Page  : int
        Size  : int
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
    let toSearchDoc (book: Book) : BookSearchDocument =
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

    /// Convert BookSearchDocument to Book (for search result representation)
    let toBook (doc: BookSearchDocument) : Book =
        {
            Id            = doc.Id
            Title         = doc.Title
            Authors       = doc.Authors
            Isbn          = doc.Isbn
            Publisher     = doc.Publisher
            PublishedYear = doc.PublishedYear
            Genre         = doc.Genre
            Description   = doc.Description
            Price         = doc.Price
            Stock         = doc.Stock
            CreatedAt     = doc.CreatedAt
            UpdatedAt     = doc.UpdatedAt
        }
