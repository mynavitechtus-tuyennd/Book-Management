module BookManagement.Tests.BookHandlerTests

open System
open System.Net
open System.Net.Http
open System.Net.Http.Headers
open System.Text
open System.Text.Json
open System.IdentityModel.Tokens.Jwt
open System.Security.Claims
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.IdentityModel.Tokens
open Giraffe
open Xunit
open FsUnit.Xunit
open BookManagement.Infrastructure.CosmosDb
open BookManagement.Infrastructure.Search
open BookManagement.Infrastructure.Abstractions
open BookManagement.Application
open BookManagement.Tests.Helpers.TestHelpers

/// Shared JWT config for tests
let private testJwtIssuer   = "BookManagementApi"
let private testJwtAudience = "BookManagementClient"
let private testJwtSecret   = "SuperSecretKeyForBookManagementApiProjectXYZ123!"

/// Generate a valid Bearer token for use in tests
let private makeTestToken () =
    let key   = SymmetricSecurityKey(Encoding.UTF8.GetBytes(testJwtSecret))
    let creds = SigningCredentials(key, SecurityAlgorithms.HmacSha256)
    let claims = [| Claim(ClaimTypes.Name, "testuser") |]
    let token = JwtSecurityToken(
                    issuer    = testJwtIssuer,
                    audience  = testJwtAudience,
                    claims    = claims,
                    expires   = DateTime.UtcNow.AddHours(1.0),
                    signingCredentials = creds)
    JwtSecurityTokenHandler().WriteToken(token)

/// Build a TestServer with stub dependencies + JWT middleware
let buildTestServer (repo: IBookRepository) (search: ISearchService) : TestServer =
    let host =
        Host.CreateDefaultBuilder()
            .ConfigureWebHost(fun webHost ->
                webHost
                    .UseTestServer()
                    .ConfigureServices(fun services ->
                        services.AddSingleton<IBookRepository>(repo)  |> ignore
                        services.AddSingleton<ISearchService>(search) |> ignore
                        services.AddSingleton<IBookService>(BookService(repo)) |> ignore
                        services.AddSingleton<ISearchQueryService>(SearchQueryService(search, repo)) |> ignore
                        services.AddRouting()                         |> ignore
                        services
                            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                            .AddJwtBearer(fun options ->
                                options.TokenValidationParameters <-
                                    TokenValidationParameters(
                                        ValidateIssuer           = true,
                                        ValidateAudience         = true,
                                        ValidateLifetime         = true,
                                        ValidateIssuerSigningKey = true,
                                        ValidIssuer              = testJwtIssuer,
                                        ValidAudience            = testJwtAudience,
                                        IssuerSigningKey         = SymmetricSecurityKey(Encoding.UTF8.GetBytes(testJwtSecret))))
                        |> ignore
                        services.AddAuthorization() |> ignore
                        services.AddGiraffe()       |> ignore)
                    .Configure(System.Action<_>(fun (app: Microsoft.AspNetCore.Builder.IApplicationBuilder) ->
                        app.UseRouting()        |> ignore
                        app.UseAuthentication() |> ignore
                        app.UseAuthorization()  |> ignore
                        app.UseGiraffe(BookManagement.HttpHandler.HttpHandler.webApp)))
                |> ignore)
            .Build()
    host.StartAsync() |> Async.AwaitTask |> Async.RunSynchronously
    host.GetTestServer()

let jsonContent (obj: 'a) =
    new StringContent(JsonSerializer.Serialize(obj), Encoding.UTF8, "application/json")

/// Create an HttpClient with a valid Bearer token pre-attached
let private authorizedClient (server: TestServer) =
    let client = server.CreateClient()
    client.DefaultRequestHeaders.Authorization <- AuthenticationHeaderValue("Bearer", makeTestToken())
    client


// ──────────────────────────────────────────────────────────────────
// GET /api/books
// ──────────────────────────────────────────────────────────────────

[<Fact>]
let ``GET /api/books returns 200 with book list`` () =
    let book = sampleBook "book-1" "Technology"
    let stub = StubBookRepository()
    stub.SetGetAll(singlePagedResult book)

    use server = buildTestServer (stub :> IBookRepository) (StubSearchService())
    use client = server.CreateClient()

    let response = client.GetAsync("/api/books").Result

    response.StatusCode |> should equal HttpStatusCode.OK
    let body = response.Content.ReadAsStringAsync().Result
    Assert.Contains("Clean Code", body)

// ──────────────────────────────────────────────────────────────────
// POST /api/books
// ──────────────────────────────────────────────────────────────────

[<Fact>]
let ``POST /api/books with valid body returns 201 Created`` () =
    let created = sampleBook "new-id" "Technology"
    let stub    = StubBookRepository()
    stub.SetCreate(created)

    use server = buildTestServer (stub :> IBookRepository) (StubSearchService())
    use client = authorizedClient server

    let req      = sampleCreateRequest()
    let response = client.PostAsync("/api/books", jsonContent req).Result

    response.StatusCode |> should equal HttpStatusCode.Created
    let body = response.Content.ReadAsStringAsync().Result
    Assert.Contains("new-id", body)

[<Fact>]
let ``POST /api/books with missing title returns 400`` () =
    let req  = { sampleCreateRequest() with Title = "" }
    let stub = StubBookRepository()

    use server = buildTestServer (stub :> IBookRepository) (StubSearchService())
    use client = authorizedClient server

    let response = client.PostAsync("/api/books", jsonContent req).Result

    response.StatusCode |> should equal HttpStatusCode.BadRequest

// ──────────────────────────────────────────────────────────────────
// GET /api/books/{genre}/{id}
// ──────────────────────────────────────────────────────────────────

[<Fact>]
let ``GET /api/books/Technology/book-1 returns 200 with book`` () =
    let book = sampleBook "book-1" "Technology"
    let stub = StubBookRepository()
    stub.SetGetById(Some book)

    use server = buildTestServer (stub :> IBookRepository) (StubSearchService())
    use client = server.CreateClient()

    let response = client.GetAsync("/api/books/Technology/book-1").Result

    response.StatusCode |> should equal HttpStatusCode.OK
    let body = response.Content.ReadAsStringAsync().Result
    Assert.Contains("book-1", body)

[<Fact>]
let ``GET /api/books/Technology/unknown returns 404`` () =
    let stub = StubBookRepository()
    stub.SetGetById(None)

    use server = buildTestServer (stub :> IBookRepository) (StubSearchService())
    use client = server.CreateClient()

    let response = client.GetAsync("/api/books/Technology/unknown").Result

    response.StatusCode |> should equal HttpStatusCode.NotFound

// ──────────────────────────────────────────────────────────────────
// DELETE /api/books/{genre}/{id}
// ──────────────────────────────────────────────────────────────────

[<Fact>]
let ``DELETE /api/books/Technology/book-1 returns 204 No Content`` () =
    let stub = StubBookRepository()
    stub.SetDelete(true)

    use server = buildTestServer (stub :> IBookRepository) (StubSearchService())
    use client = authorizedClient server

    let response = client.DeleteAsync("/api/books/Technology/book-1").Result

    response.StatusCode |> should equal HttpStatusCode.NoContent

[<Fact>]
let ``DELETE /api/books/Technology/unknown returns 404`` () =
    let stub = StubBookRepository()
    stub.SetDelete(false)

    use server = buildTestServer (stub :> IBookRepository) (StubSearchService())
    use client = authorizedClient server

    let response = client.DeleteAsync("/api/books/Technology/unknown").Result

    response.StatusCode |> should equal HttpStatusCode.NotFound
