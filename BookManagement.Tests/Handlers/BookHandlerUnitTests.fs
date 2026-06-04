module BookManagement.Tests.Handlers.BookHandlerUnitTests

open System.Net
open Xunit
open FsUnit.Xunit
open Microsoft.Extensions.DependencyInjection
open BookManagement.Infrastructure.Abstractions
open BookManagement.Handlers.BookHttpHandler
open BookManagement.Tests.Helpers.TestHelpers
open Giraffe

/// Build a fake context with IBookRepository registered in DI
let private ctxWithRepo (stub: StubBookRepository) method path queryParams body =
    let ctx = createFakeContext method path queryParams body
    let services = ServiceCollection()
    services.AddGiraffe() |> ignore
    services.AddSingleton<IBookRepository>(stub :> IBookRepository) |> ignore
    services.AddSingleton<IBookService>(BookManagement.Application.BookService(stub) :> IBookService) |> ignore
    ctx.RequestServices <- services.BuildServiceProvider()
    ctx

let private postCtx (stub: StubBookRepository) =
    ctxWithRepo stub "POST" "/api/books" [] None

let private putCtx (stub: StubBookRepository) (id: string) (genre: string) =
    ctxWithRepo stub "PUT" $"/api/books/{id}/{genre}" [] None

let private deleteCtx (stub: StubBookRepository) (id: string) (genre: string) =
    ctxWithRepo stub "DELETE" $"/api/books/{id}/{genre}" [] None

let private getCtx (stub: StubBookRepository) (queryParams: (string * string) list) =
    ctxWithRepo stub "GET" "/api/books" queryParams None

// ──────────────────────────────────────────────────────────────────
// POST /api/books — validation (unit tests, no TestServer)
// bindJson already parsed body; handler receives the bound model
// ──────────────────────────────────────────────────────────────────

[<Fact>]
let ``create with missing title returns 400`` () =
    let req  = { sampleCreateRequest() with Title = "" }
    let stub = StubBookRepository()
    stub.SetCreate(sampleBook "x" "Technology")
    let ctx  = postCtx stub

    let status = runHandler (create req) ctx

    status |> should equal (int HttpStatusCode.BadRequest)

[<Fact>]
let ``create with whitespace title returns 400`` () =
    let req  = { sampleCreateRequest() with Title = "   " }
    let stub = StubBookRepository()
    let ctx  = postCtx stub

    let status = runHandler (create req) ctx

    status |> should equal (int HttpStatusCode.BadRequest)

[<Fact>]
let ``create with missing genre returns 400`` () =
    let req  = { sampleCreateRequest() with Genre = "" }
    let stub = StubBookRepository()
    stub.SetCreate(sampleBook "x" "Technology")
    let ctx  = postCtx stub

    let status = runHandler (create req) ctx

    status |> should equal (int HttpStatusCode.BadRequest)

[<Fact>]
let ``create with whitespace genre returns 400`` () =
    let req  = { sampleCreateRequest() with Genre = "   " }
    let stub = StubBookRepository()
    let ctx  = postCtx stub

    let status = runHandler (create req) ctx

    status |> should equal (int HttpStatusCode.BadRequest)

[<Fact>]
let ``create with empty authors list returns 400`` () =
    let req  = { sampleCreateRequest() with Authors = [] }
    let stub = StubBookRepository()
    let ctx  = postCtx stub

    let status = runHandler (create req) ctx

    status |> should equal (int HttpStatusCode.BadRequest)

[<Fact>]
let ``create with valid body calls repository once`` () =
    let stub = StubBookRepository()
    stub.SetCreate(sampleBook "new-id" "Technology")
    let ctx  = postCtx stub

    runHandler (create (sampleCreateRequest())) ctx |> ignore

    stub.CreateCallCount |> should equal 1

[<Fact>]
let ``create with valid body returns 201`` () =
    let stub = StubBookRepository()
    stub.SetCreate(sampleBook "new-id" "Technology")
    let ctx  = postCtx stub

    let status = runHandler (create (sampleCreateRequest())) ctx

    status |> should equal (int HttpStatusCode.Created)

// ──────────────────────────────────────────────────────────────────
// PUT /api/books/{id}/{genre} — validation
// ──────────────────────────────────────────────────────────────────

[<Fact>]
let ``update with missing title returns 400`` () =
    let req  = { sampleUpdateRequest() with Title = "" }
    let stub = StubBookRepository()
    stub.SetUpdate(Some (sampleBook "book-1" "Technology"))
    let ctx  = putCtx stub "book-1" "Technology"

    let status = runHandler (update "book-1" "Technology" req) ctx

    status |> should equal (int HttpStatusCode.BadRequest)

[<Fact>]
let ``update with whitespace title returns 400`` () =
    let req  = { sampleUpdateRequest() with Title = "   " }
    let stub = StubBookRepository()
    let ctx  = putCtx stub "book-1" "Technology"

    let status = runHandler (update "book-1" "Technology" req) ctx

    status |> should equal (int HttpStatusCode.BadRequest)

[<Fact>]
let ``update with empty authors returns 400`` () =
    let req  = { sampleUpdateRequest() with Authors = [] }
    let stub = StubBookRepository()
    let ctx  = putCtx stub "book-1" "Technology"

    let status = runHandler (update "book-1" "Technology" req) ctx

    status |> should equal (int HttpStatusCode.BadRequest)

[<Fact>]
let ``update with valid body and existing book returns 200`` () =
    let updated = sampleBook "book-1" "Technology"
    let stub    = StubBookRepository()
    stub.SetUpdate(Some updated)
    let ctx     = putCtx stub "book-1" "Technology"

    let status = runHandler (update "book-1" "Technology" (sampleUpdateRequest())) ctx

    status |> should equal (int HttpStatusCode.OK)

[<Fact>]
let ``update with valid body and missing book returns 404`` () =
    let stub = StubBookRepository()
    stub.SetUpdate(None)
    let ctx  = putCtx stub "unknown" "Technology"

    let status = runHandler (update "unknown" "Technology" (sampleUpdateRequest())) ctx

    status |> should equal (int HttpStatusCode.NotFound)

// ──────────────────────────────────────────────────────────────────
// GET /api/books/{id}/{genre}
// ──────────────────────────────────────────────────────────────────

[<Fact>]
let ``getById with existing book returns 200`` () =
    let stub = StubBookRepository()
    stub.SetGetById(Some (sampleBook "book-1" "Technology"))
    let ctx  = ctxWithRepo stub "GET" "/api/books/book-1/Technology" [] None

    let status = runHandler (getById "book-1" "Technology") ctx

    status |> should equal (int HttpStatusCode.OK)

[<Fact>]
let ``getById with missing book returns 404`` () =
    let stub = StubBookRepository()
    stub.SetGetById(None)
    let ctx  = ctxWithRepo stub "GET" "/api/books/missing/Technology" [] None

    let status = runHandler (getById "missing" "Technology") ctx

    status |> should equal (int HttpStatusCode.NotFound)

// ──────────────────────────────────────────────────────────────────
// DELETE /api/books/{id}/{genre}
// ──────────────────────────────────────────────────────────────────

[<Fact>]
let ``delete with existing book returns 204`` () =
    let stub = StubBookRepository()
    stub.SetDelete(true)
    let ctx  = deleteCtx stub "book-1" "Technology"

    let status = runHandler (delete "book-1" "Technology") ctx

    status |> should equal (int HttpStatusCode.NoContent)

[<Fact>]
let ``delete with missing book returns 404`` () =
    let stub = StubBookRepository()
    stub.SetDelete(false)
    let ctx  = deleteCtx stub "unknown" "Technology"

    let status = runHandler (delete "unknown" "Technology") ctx

    status |> should equal (int HttpStatusCode.NotFound)

// ──────────────────────────────────────────────────────────────────
// GET /api/books — query param parsing
// ──────────────────────────────────────────────────────────────────

[<Fact>]
let ``getAll with no query params uses defaults and returns 200`` () =
    let stub = StubBookRepository()
    stub.SetGetAll(emptyPagedResult())
    let ctx  = getCtx stub []

    let status = runHandler (getAll { Page = None; Size = None }) ctx

    status |> should equal (int HttpStatusCode.OK)

[<Fact>]
let ``getAll with explicit page and size returns 200`` () =
    let stub = StubBookRepository()
    stub.SetGetAll(emptyPagedResult())
    let ctx  = getCtx stub [("page", "2"); ("size", "10")]

    let status = runHandler (getAll { Page = Some 2; Size = Some 10 }) ctx

    status |> should equal (int HttpStatusCode.OK)

[<Fact>]
let ``getAll with page parameter as string returns 200`` () =
    let stub = StubBookRepository()
    stub.SetGetAll(emptyPagedResult())
    let ctx  = getCtx stub [("page", "not-a-number")]

    let status = runHandler (getAll { Page = None; Size = None }) ctx

    status |> should equal (int HttpStatusCode.OK)