namespace BookManagement.Infrastructure.Abstractions

open System.Threading.Tasks
open BookManagement.Domain

/// Abstraction over Azure AI Search operations.
type ISearchService =
    /// Index (create or update) a book document in the search index
    abstract member IndexDocument  : book:Book -> Task<unit>

    /// Remove a book document from the search index by id
    abstract member DeleteDocument : id:string -> Task<unit>

    /// Full-text search with optional filters and pagination
    abstract member Search         : req:SearchRequest -> Task<PagedResult<BookResponse>>
