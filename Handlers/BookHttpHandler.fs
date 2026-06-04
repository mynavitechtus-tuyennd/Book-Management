namespace BookManagement.Handlers

open System
open System.Net
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Giraffe
open BookManagement.Domain
open BookManagement.Infrastructure.Abstractions
open BookManagement.Helpers

module BookHttpHandler =

    let private getService (ctx: HttpContext) =
        ctx.RequestServices.GetRequiredService<IBookService>()

    // GET /api/books?page=1&size=20
    let getAll : HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let svc = getService ctx
                let page = ctx.TryGetQueryStringValue "page" |> Option.bind CommonHelper.tryParseInt |> Option.defaultValue 1
                let size = ctx.TryGetQueryStringValue "size" |> Option.bind CommonHelper.tryParseInt |> Option.defaultValue 20
                let! result = svc.GetAll page size
                return! json result next ctx
            }

    // GET /api/books/{id}/{genre}
    let getById (id: string) (genre: string) : HttpHandler =
        fun next ctx ->
            task {
                let! book = (getService ctx).GetById id genre
                match book with
                | Some b -> return! json b next ctx
                | None   -> return! CommonHelper.notFound $"Book '{id}' not found" next ctx
            }

    // POST /api/books — body bound by bindJson<CreateBookRequest> in routing
    let create (req: CreateBookRequest) : HttpHandler =
        fun next ctx ->
            task {
                if String.IsNullOrWhiteSpace(req.Title) then
                    return! CommonHelper.badRequest "Title is required" next ctx
                elif String.IsNullOrWhiteSpace(req.Genre) then
                    return! CommonHelper.badRequest "Genre is required" next ctx
                elif req.Authors |> List.isEmpty then
                    return! CommonHelper.badRequest "At least one author is required" next ctx
                else
                    let! created = (getService ctx).Create req
                    ctx.SetStatusCode(int HttpStatusCode.Created)
                    return! json created next ctx
            }

    // PUT /api/books/{id}/{genre} — body bound by bindJson<UpdateBookRequest> in routing
    let update (id: string) (genre: string) (req: UpdateBookRequest) : HttpHandler =
        fun next ctx ->
            task {
                if String.IsNullOrWhiteSpace(req.Title) then
                    return! CommonHelper.badRequest "Title is required" next ctx
                elif req.Authors |> List.isEmpty then
                    return! CommonHelper.badRequest "At least one author is required" next ctx
                else
                    let! updated = (getService ctx).Update id genre req
                    match updated with
                    | Some b -> return! json b next ctx
                    | None   -> return! CommonHelper.notFound $"Book '{id}' not found" next ctx
            }

    // DELETE /api/books/{id}/{genre}
    let delete (id: string) (genre: string) : HttpHandler =
        fun next ctx ->
            task {
                let! deleted = (getService ctx).Delete id genre
                if deleted then
                    ctx.SetStatusCode(int HttpStatusCode.NoContent)
                    return! next ctx
                else
                    return! CommonHelper.notFound $"Book '{id}' not found" next ctx
            }
