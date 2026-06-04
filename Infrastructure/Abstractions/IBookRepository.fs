namespace BookManagement.Infrastructure.Abstractions

open System.Threading.Tasks
open BookManagement.Domain

/// Abstraction over Cosmos DB operations for Books.
type IBookRepository =
    /// Get all books (paged)
    abstract member GetAll   : page:int -> size:int -> Task<PagedResult<BookResponse>>

    /// Get a book by id and genre (partition key)
    abstract member GetById  : id:string -> genre:string -> Task<BookResponse option>

    /// Search books in the database
    abstract member SearchDb : req:SearchDbRequest -> Task<PagedResult<BookResponse>>

    /// Create a new book — also pushes to Azure Search index
    abstract member Create   : req:CreateBookRequest -> Task<BookResponse>

    /// Update a book by id/genre — also updates Azure Search index
    abstract member Update   : id:string -> genre:string -> req:UpdateBookRequest -> Task<BookResponse option>

    /// Delete a book by id/genre — also removes from Azure Search index
    abstract member Delete   : id:string -> genre:string -> Task<bool>
