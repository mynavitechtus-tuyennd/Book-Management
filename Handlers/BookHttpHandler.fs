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
    let getAll (req: GetAllRequest) : HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let svc = getService ctx
                let page = if req.Page.IsSome then req.Page.Value else 1
                let size = if req.Size.IsSome then req.Size.Value else 20
                let! result = svc.GetAll page size
                return! json result next ctx
            }

    // GET /api/books/{id}/{genre}
    let getById (genre: string) (id: string)  : HttpHandler =
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
                match CommonHelper.validate req with
                | Error err -> return! CommonHelper.badRequest err next ctx
                | Ok () ->
                    let! created = (getService ctx).Create req
                    ctx.SetStatusCode(int HttpStatusCode.Created)
                    return! json created next ctx
            }

    // PUT /api/books/{genre}/{id} — body bound by bindJson<UpdateBookRequest> in routing
    let update (genre: string) (id: string) (req: UpdateBookRequest) : HttpHandler =
        fun next ctx ->
            task {
                match CommonHelper.validate req with
                | Error err -> return! CommonHelper.badRequest err next ctx
                | Ok () ->
                    let! updated = (getService ctx).Update id genre req
                    match updated with
                    | Some b -> return! json b next ctx
                    | None   -> return! CommonHelper.notFound $"Book '{id}' not found" next ctx
            }

    // DELETE /api/books/{genre}/{id}
    let delete (genre: string) (id: string) : HttpHandler =
        fun next ctx ->
            task {
                let! deleted = (getService ctx).Delete id genre
                if deleted then
                    ctx.SetStatusCode(int HttpStatusCode.NoContent)
                    return! next ctx
                else
                    return! CommonHelper.notFound $"Book '{id}' not found" next ctx
            }
