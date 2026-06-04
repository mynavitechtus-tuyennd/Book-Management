namespace BookManagement.Handlers

open System
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Giraffe
open BookManagement.Domain
open BookManagement.Infrastructure.Abstractions

module SearchHttpHandler =

    // GET /api/books/search?q=clean+code&genre=Technology&author=Martin&page=1&size=10
    let search : HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let searchService = ctx.RequestServices.GetRequiredService<ISearchQueryService>()

                let q      = ctx.TryGetQueryStringValue "q"      |> Option.defaultValue "*"
                let genre  = ctx.TryGetQueryStringValue "genre"
                let author = ctx.TryGetQueryStringValue "author"
                let page   = ctx.TryGetQueryStringValue "page"   |> Option.bind (fun s -> match Int32.TryParse(s) with true, v -> Some v | _ -> None) |> Option.defaultValue 1
                let size   = ctx.TryGetQueryStringValue "size"   |> Option.bind (fun s -> match Int32.TryParse(s) with true, v -> Some v | _ -> None) |> Option.defaultValue 10

                let req = {
                    Query  = q
                    Genre  = genre
                    Author = author
                    Page   = max 1 page
                    Size   = min 100 (max 1 size)
                }

                let! result = searchService.Search req
                return! json result next ctx
            }
