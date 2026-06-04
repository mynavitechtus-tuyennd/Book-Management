namespace BookManagement.Infrastructure.Abstractions

open System.Threading.Tasks
open BookManagement.Domain

type ISearchQueryService =
    abstract member Search : req:SearchRequest -> Task<PagedResult<BookResponse>>
