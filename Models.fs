namespace BookManagement.Domain

open System
open Newtonsoft.Json

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

/// Core domain model — maps directly to Cosmos DB document
[<CLIMutable>]
type Book =
    {
        [<JsonProperty("id")>]
        Id            : string
        [<JsonProperty("title")>]
        Title         : string
        [<JsonProperty("author")>]
        Author        : string
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
        Price         : decimal
        [<JsonProperty("stock")>]
        Stock         : int
        [<JsonProperty("createdAt")>]
        CreatedAt     : DateTime
        [<JsonProperty("updatedAt")>]
        UpdatedAt     : DateTime
    }

/// DTO for POST /api/books
[<CLIMutable>]
type CreateBookRequest =
    {
        Title         : string
        Author        : string
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
        Author        : string
        Isbn          : string
        Publisher     : string
        PublishedYear : int
        Description   : string
        Price         : decimal
        Stock         : int
    }

/// API response — same shape as Book for simplicity
type BookResponse = Book

/// Query parameters for Azure Search
type SearchRequest =
    {
        Query  : string
        Genre  : string option
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
            Author        = req.Author
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
            Author        = req.Author
            Isbn          = req.Isbn
            Publisher     = req.Publisher
            PublishedYear = req.PublishedYear
            Description   = req.Description
            Price         = req.Price
            Stock         = req.Stock
            UpdatedAt     = DateTime.UtcNow }