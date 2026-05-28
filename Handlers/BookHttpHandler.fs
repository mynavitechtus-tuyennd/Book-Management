namespace BookManagement.Handlers

open System
open Microsoft.AspNetCore.Http
open Giraffe
open BookManagement.Domain
open BookManagement.Infrastructure.CosmosDb
open BookManagement.Helpers

module BookHttpHandler =

    // GET /api/books?page=1&size=20
    let getAll (repo: IBookRepository) : HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let page = ctx.TryGetQueryStringValue "page" |> Option.bind CommonHelper.tryParseInt |> Option.defaultValue 1
                let size = ctx.TryGetQueryStringValue "size" |> Option.bind CommonHelper.tryParseInt |> Option.defaultValue 20
                let! result = repo.GetAll page size
                return! json result next ctx
            }

    // GET /api/books/{id}/{genre}
    let getById (id: string) (genre: string) (repo: IBookRepository) : HttpHandler =
        fun next ctx ->
            task {
                let! book = repo.GetById id genre
                match book with
                | Some b -> return! json b next ctx
                | None   -> return! CommonHelper.notFound $"Book '{id}' not found" next ctx
            }

    // POST /api/books
    let create (repo: IBookRepository) : HttpHandler =
        fun next ctx ->
            task {
                try
                    let! req = ctx.BindJsonAsync<CreateBookRequest>()
                    if isNull (box req) then
                        return! CommonHelper.badRequest "Request body is required and must be valid JSON" next ctx
                    elif String.IsNullOrWhiteSpace(req.Title) then
                        return! CommonHelper.badRequest "Title is required" next ctx
                    elif String.IsNullOrWhiteSpace(req.Genre) then
                        return! CommonHelper.badRequest "Genre is required" next ctx
                    else
                        let! created = repo.Create req
                        ctx.SetStatusCode 201
                        return! json created next ctx
                with ex ->
                    return! CommonHelper.badRequest $"Invalid JSON body: {ex.Message}" next ctx
            }

    // PUT /api/books/{id}/{genre}
    let update (id: string) (genre: string) (repo: IBookRepository) : HttpHandler =
        fun next ctx ->
            task {
                try
                    let! req = ctx.BindJsonAsync<UpdateBookRequest>()
                    if isNull (box req) then
                        return! CommonHelper.badRequest "Request body is required and must be valid JSON" next ctx
                    elif String.IsNullOrWhiteSpace(req.Title) then
                        return! CommonHelper.badRequest "Title is required" next ctx
                    else
                        let! updated = repo.Update id genre req
                        match updated with
                        | Some b -> return! json b next ctx
                        | None   -> return! CommonHelper.notFound $"Book '{id}' not found" next ctx
                with ex ->
                    return! CommonHelper.badRequest $"Invalid JSON body: {ex.Message}" next ctx
            }

    // DELETE /api/books/{id}/{genre}
    let delete (id: string) (genre: string) (repo: IBookRepository) : HttpHandler =
        fun next ctx ->
            task {
                let! deleted = repo.Delete id genre
                if deleted then
                    ctx.SetStatusCode 204
                    return! next ctx
                else
                    return! CommonHelper.notFound $"Book '{id}' not found" next ctx
            }
