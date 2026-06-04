namespace BookManagement.Infrastructure.Abstractions

open System.Threading.Tasks
open BookManagement.Domain

type IBookService =
    abstract member GetAll   : page:int -> size:int -> Task<PagedResult<BookResponse>>
    abstract member GetById  : id:string -> genre:string -> Task<BookResponse option>
    abstract member Create   : req:CreateBookRequest -> Task<BookResponse>
    abstract member Update   : id:string -> genre:string -> req:UpdateBookRequest -> Task<BookResponse option>
    abstract member Delete   : id:string -> genre:string -> Task<bool>
