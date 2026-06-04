namespace BookManagement.Application

open System.Threading.Tasks
open BookManagement.Domain
open BookManagement.Infrastructure.Abstractions

type BookService(repo: IBookRepository) =
    interface IBookService with
        member _.GetAll page size = repo.GetAll page size
        member _.GetById id genre = repo.GetById id genre
        member _.Create req = repo.Create req
        member _.Update id genre req = repo.Update id genre req
        member _.Delete id genre = repo.Delete id genre

type SearchQueryService(search: ISearchService) =
    interface ISearchQueryService with
        member _.Search req = search.Search req
