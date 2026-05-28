namespace BookManagement.Infrastructure.CosmosDb

open System.Threading.Tasks
open BookManagement.Domain

/// Abstraction over Cosmos DB operations for Books.
/// Depends on ISearchService to keep index in sync after mutations.
type IBookRepository =
    /// Get all books (paged)
    abstract member GetAll   : page:int -> size:int -> Task<PagedResult<BookResponse>>

    /// Get a book by id and genre (partition key)
    abstract member GetById  : id:string -> genre:string -> Task<BookResponse option>

    /// Create a new book — also pushes to Azure Search index
    abstract member Create   : req:CreateBookRequest -> Task<BookResponse>

    /// Update a book by id/genre — also updates Azure Search index
    abstract member Update   : id:string -> genre:string -> req:UpdateBookRequest -> Task<BookResponse option>

    /// Delete a book by id/genre — also removes from Azure Search index
    abstract member Delete   : id:string -> genre:string -> Task<bool>
