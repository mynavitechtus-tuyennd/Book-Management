module BookManagement.Tests.BookHandlerTests

open System.Net
open Xunit
open FsUnit.Xunit
open BookManagement.Infrastructure.Abstractions
open BookManagement.Tests.Helpers.TestHelpers



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
let ``POST /api/books with missing title returns 422`` () =
    let req  = { sampleCreateRequest() with Title = "" }
    let stub = StubBookRepository()

    use server = buildTestServer (stub :> IBookRepository) (StubSearchService())
    use client = authorizedClient server

    let response = client.PostAsync("/api/books", jsonContent req).Result

    response.StatusCode |> should equal HttpStatusCode.UnprocessableEntity

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
