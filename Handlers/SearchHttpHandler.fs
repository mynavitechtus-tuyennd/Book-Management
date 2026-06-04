namespace BookManagement.Handlers

open System
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Giraffe
open BookManagement.Domain
open BookManagement.Infrastructure.Abstractions

module SearchHttpHandler =

    // GET /api/books/search?q=clean+code&genre=Technology&author=Martin&page=1&size=10
    let search (req: SearchRequest) : HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let searchService = ctx.RequestServices.GetRequiredService<ISearchQueryService>()
                // Apply defaults for page/size if missing
                let req = { req with
                                Query = if String.IsNullOrWhiteSpace(req.Query) then "*" else req.Query
                                Page = if req.Page <= 0 then 1 else req.Page
                                Size = if req.Size <= 0 then 10 else min 100 req.Size }

                let! result = searchService.Search req
                return! json result next ctx
            }

    // GET /api/books/search-db?title=clean+code&genre=Technology&page=1&size=10
    // Request model is bound by Giraffe's bindModel at the route level (HttpHandler.fs)
    let searchDb (req: SearchDbRequest) : HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let searchService = ctx.RequestServices.GetRequiredService<ISearchQueryService>()

                // Apply defaults for page/size since bindModel won't set them if missing
                let req = { req with
                                Page = if req.Page <= 0 then 1 else req.Page
                                Size = if req.Size <= 0 then 10 else min 100 req.Size }

                let! result = searchService.SearchDb req
                return! json result next ctx
            }
