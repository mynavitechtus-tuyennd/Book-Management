namespace BookManagement.HttpHandler

open Giraffe
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open BookManagement.Infrastructure.CosmosDb
open BookManagement.Infrastructure.Search
open BookManagement.Handlers

module HttpHandler =

    /// Resolve IBookRepository from DI container
    let private getRepo (ctx: HttpContext) =
        ctx.RequestServices.GetRequiredService<IBookRepository>()

    /// Resolve ISearchService from DI container
    let private getSearch (ctx: HttpContext) =
        ctx.RequestServices.GetRequiredService<ISearchService>()

    /// Require a valid JWT Bearer token; returns 401 if missing/invalid
    let private requiresAuth : HttpHandler =
        requiresAuthentication (setStatusCode 401 >=> json {| message = "Unauthorized. A valid JWT Bearer token is required." |})

    let webApp : HttpHandler =
        choose [
            // Auth routes (public)
            subRoute "/api/auth" (
                choose [
                    POST >=> route "/login" >=> AuthHttpHandler.login
                ]
            )

            // Book CRUD routes
            subRoute "/api/books" (
                choose [
                    // Public read-only routes
                    GET  >=> route  "/search" >=> (fun next ctx -> SearchHttpHandler.search (getSearch ctx) next ctx)
                    GET  >=> route  ""        >=> (fun next ctx -> BookHttpHandler.getAll   (getRepo  ctx) next ctx)

                    GET  >=> routef "/%s/%s"  (fun (id, genre) -> fun next ctx ->
                        BookHttpHandler.getById id genre (getRepo ctx) next ctx)

                    // Protected mutation routes (JWT required)
                    POST   >=> route  ""      >=> requiresAuth >=> (fun next ctx -> BookHttpHandler.create (getRepo ctx) next ctx)

                    PUT    >=> routef "/%s/%s" (fun (id, genre) -> requiresAuth >=> fun next ctx ->
                        BookHttpHandler.update id genre (getRepo ctx) next ctx)

                    DELETE >=> routef "/%s/%s" (fun (id, genre) -> requiresAuth >=> fun next ctx ->
                        BookHttpHandler.delete id genre (getRepo ctx) next ctx)
                ]
            )

            setStatusCode 404 >=> json {| message = "Route not found" |}
        ]