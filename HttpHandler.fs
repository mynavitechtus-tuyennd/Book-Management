namespace BookManagement.HttpHandler

open Giraffe
open BookManagement.Domain
open BookManagement.Handlers

module HttpHandler =

    /// Require a valid JWT Bearer token; returns 401 if missing/invalid
    let private requiresAuth: HttpHandler =
        requiresAuthentication (
            setStatusCode 401
            >=> json {| message = "Unauthorized. A valid JWT Bearer token is required." |}
        )

    let webApp: HttpHandler =
        choose
            [
              // Auth routes (public)
              subRoute "/api/auth" (choose [ POST >=> route "/login" >=> AuthHttpHandler.login ])

              // Book CRUD routes (CosmosDB) + Search routes (Azure AI Search)
              subRoute
                  "/api/books"
                  (choose
                      [
                        // Public read-only routes
                        GET >=> route "/search" >=> bindModel<SearchRequest> None SearchHttpHandler.search
                        GET >=> route "/search-db" >=> bindModel<SearchDbRequest> None SearchHttpHandler.searchDb     

                        GET >=> routef "/%s/%s" (fun (genre, id) -> BookHttpHandler.getById genre id)

                        // Protected mutation routes (JWT required)
                        // bindModel<'T> declares the expected request body model and auto-returns 400 on invalid JSON/Form
                        POST
                        >=> requiresAuth
                        >=> bindModel<CreateBookRequest> None BookHttpHandler.create

                        PUT
                        >=> routef "/%s/%s" (fun (genre, id) ->
                            requiresAuth
                            >=> bindModel<UpdateBookRequest> None (BookHttpHandler.update genre id))

                        DELETE
                        >=> routef "/%s/%s" (fun (genre, id) -> requiresAuth >=> BookHttpHandler.delete genre id)
                        GET >=> bindModel<GetAllRequest>  None BookHttpHandler.getAll
                        ])

              setStatusCode 404 >=> json {| message = "Route not found" |} ]
