module BookManagement.Tests.Helpers.TestHelpers

open System
open System.Threading.Tasks
open BookManagement.Domain
open BookManagement.Infrastructure.Abstractions
open System.IO
open System.Collections.Generic
open System.Net.Http
open System.Net.Http.Headers
open System.Text
open System.Text.Json
open System.IdentityModel.Tokens.Jwt
open System.Security.Claims
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.Primitives
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.IdentityModel.Tokens
open Giraffe
open BookManagement.Application

/// Create a sample Book for testing
let sampleBook (id: string) (genre: string) : Book =
    {
        Id            = id
        Title         = "Clean Code"
        Authors       = ["Robert C. Martin"]
        Isbn          = "9780132350884"
        Publisher     = "Prentice Hall"
        PublishedYear = 2008
        Genre         = genre
        Description   = "A handbook of agile software craftsmanship"
        Price         = 35.99
        Stock         = 10
        CreatedAt     = DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        UpdatedAt     = DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    }

/// Create a sample CreateBookRequest
let sampleCreateRequest () : CreateBookRequest =
    {
        Title         = "Clean Code"
        Authors       = ["Robert C. Martin"]
        Isbn          = "9780132350884"
        Publisher     = "Prentice Hall"
        PublishedYear = 2008
        Genre         = "Technology"
        Description   = "A handbook of agile software craftsmanship"
        Price         = 35.99
        Stock         = 10
    }

/// Create a sample UpdateBookRequest
let sampleUpdateRequest () : UpdateBookRequest =
    {
        Title         = "Clean Code (Updated)"
        Authors       = ["Robert C. Martin"]
        Isbn          = "9780132350884"
        Publisher     = "Prentice Hall"
        PublishedYear = 2008
        Description   = "Updated description"
        Price         = 39.99
        Stock         = 5
    }

/// Create a sample LoginRequest
let sampleLoginRequest () : LoginRequest =
    { Username = "admin"; Password = "[PASSWORD]" }

/// A paged result containing a single book
let singlePagedResult (book: Book) : PagedResult<BookResponse> =
    { Items = [book]; TotalCount = 1L; Page = 1; Size = 20 }

/// Empty paged result
let emptyPagedResult () : PagedResult<BookResponse> =
    { Items = []; TotalCount = 0L; Page = 1; Size = 20 }

/// Stub IBookRepository with settable return values
type StubBookRepository(?getAllResult, ?getByIdResult, ?createResult, ?updateResult, ?deleteResult, ?searchDbResult) =
    let mutable _getAll    = defaultArg getAllResult   (emptyPagedResult())
    let mutable _getById   = defaultArg getByIdResult  None
    let mutable _create    = defaultArg createResult   (sampleBook "stub" "Technology")
    let mutable _update    = defaultArg updateResult   None
    let mutable _delete    = defaultArg deleteResult   false
    let mutable _searchDb  = defaultArg searchDbResult (emptyPagedResult())
    let mutable _createN   = 0

    member _.SetGetAll v    = _getAll   <- v
    member _.SetGetById v   = _getById  <- v
    member _.SetCreate v    = _create   <- v
    member _.SetUpdate v    = _update   <- v
    member _.SetDelete v    = _delete   <- v
    member _.SetSearchDb v  = _searchDb <- v
    member _.CreateCallCount = _createN

    interface IBookRepository with
        member _.GetAll page size    = Task.FromResult(_getAll)
        member _.GetById id genre    = Task.FromResult(_getById)
        member _.SearchDb req        = Task.FromResult(_searchDb)
        member _.Create req          =
            _createN <- _createN + 1
            Task.FromResult(_create)
        member _.Update id genre req = Task.FromResult(_update)
        member _.Delete id genre     = Task.FromResult(_delete)

/// Stub ISearchService with configurable responses and call tracking
type StubSearchService(?searchResult) =
    let mutable _searchResult = defaultArg searchResult (emptyPagedResult())
    let mutable _lastSearchRequest : SearchRequest option = None
    let mutable _searchCallCount = 0
    let mutable _indexCallCount = 0
    let mutable _deleteCallCount = 0
    let mutable _lastIndexedBook : Book option = None
    let mutable _lastDeletedId : string option = None

    member _.SetSearch v = _searchResult <- v
    member _.LastSearchRequest = _lastSearchRequest
    member _.SearchCallCount = _searchCallCount
    member _.IndexCallCount = _indexCallCount
    member _.DeleteCallCount = _deleteCallCount
    member _.LastIndexedBook = _lastIndexedBook
    member _.LastDeletedId = _lastDeletedId

    interface ISearchService with
        member _.IndexDocument book =
            _indexCallCount <- _indexCallCount + 1
            _lastIndexedBook <- Some book
            Task.FromResult<unit>(())
        member _.DeleteDocument id =
            _deleteCallCount <- _deleteCallCount + 1
            _lastDeletedId <- Some id
            Task.FromResult<unit>(())
        member _.Search req =
            _searchCallCount <- _searchCallCount + 1
            _lastSearchRequest <- Some req
            Task.FromResult(_searchResult)


// ── Fake HttpContext helpers for direct Giraffe handler unit tests ─────────────
/// Create a minimal fake HttpContext for unit-testing Giraffe handlers.
/// Supports query string params and captures response body + status code.
let createFakeContext (method: string) (path: string) (queryParams: (string * string) list) (body: string option) =
    let ctx = DefaultHttpContext()

    // Register Giraffe serializer so ctx.WriteJsonAsync works
    let services = ServiceCollection()
    services.AddGiraffe() |> ignore
    ctx.RequestServices <- services.BuildServiceProvider()

    // Request
    ctx.Request.Method <- method
    ctx.Request.Path   <- PathString(path)
    ctx.Request.QueryString <-
        queryParams
        |> List.fold (fun (qs: QueryString) (k, v) -> qs.Add(k, v)) QueryString.Empty
    ctx.Request.Query <-
        let d = Dictionary<string, StringValues>()
        queryParams |> List.iter (fun (k, v) -> d.[k] <- StringValues(v))
        QueryCollection(d) :> IQueryCollection

    // Body
    match body with
    | Some b ->
        let bytes = Text.Encoding.UTF8.GetBytes(b)
        ctx.Request.Body          <- new MemoryStream(bytes)
        ctx.Request.ContentLength <- Nullable(int64 bytes.Length)
        ctx.Request.ContentType   <- "application/json"
    | None -> ()

    // Response — writable memory stream to capture output
    ctx.Response.Body <- new MemoryStream()

    ctx

/// Execute a Giraffe HttpHandler against a fake context and return the HTTP status code.
let runHandler (handler: Giraffe.Core.HttpHandler) (ctx: HttpContext) : int =
    let next : Giraffe.Core.HttpFunc = fun c -> Task.FromResult(Some c)
    handler next ctx
    |> Async.AwaitTask
    |> Async.RunSynchronously
    |> ignore
    ctx.Response.StatusCode

/// Shared JWT config for tests
let private testJwtIssuer   = "BookManagementApi"
let private testJwtAudience = "BookManagementClient"
let private testJwtSecret   = "SuperSecretKeyForBookManagementApiProjectXYZ123!"

/// Generate a valid Bearer token for use in tests
let makeTestToken () =
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

let private testErrorHandler (ex: Exception) (logger: Microsoft.Extensions.Logging.ILogger) =
    let statusCode =
        if ex.GetType().Name.Contains("Json") || ex.Message.Contains("Json") then 400 else 500
    clearResponse >=> setStatusCode statusCode >=> json {| message = ex.Message |}

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
                        app.UseGiraffeErrorHandler(testErrorHandler).UseGiraffe(BookManagement.HttpHandler.HttpHandler.webApp)))
                |> ignore)
            .Build()
    host.StartAsync() |> Async.AwaitTask |> Async.RunSynchronously
    host.GetTestServer()

let jsonContent (obj: 'a) =
    new StringContent(JsonSerializer.Serialize(obj), Encoding.UTF8, "application/json")

/// Create an HttpClient with a valid Bearer token pre-attached
let authorizedClient (server: TestServer) =
    let client = server.CreateClient()
    client.DefaultRequestHeaders.Authorization <- AuthenticationHeaderValue("Bearer", makeTestToken())
    client

