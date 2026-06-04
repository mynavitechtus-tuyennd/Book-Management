module BookManagement.Tests.BookRepositoryTests

open Xunit
open FsUnit.Xunit
open BookManagement.Infrastructure.CosmosDb
open BookManagement.Infrastructure.Abstractions
open BookManagement.Tests.Helpers.TestHelpers

// Helper: cast stub to interface for calling curried methods
let repo (stub: StubBookRepository) : IBookRepository = stub :> IBookRepository

// ──────────────────────────────────────────────────────────────────
// GetAll
// ──────────────────────────────────────────────────────────────────

[<Fact>]
let ``GetAll returns paged result with books`` () =
    let book = sampleBook "book-1" "Technology"
    let stub = StubBookRepository()
    stub.SetGetAll(singlePagedResult book)
    let r = repo stub

    let result = (r.GetAll 1 20).Result

    result.Items.Length   |> should equal 1
    result.TotalCount     |> should equal 1L
    result.Items.[0].Id   |> should equal "book-1"

[<Fact>]
let ``GetAll returns empty result when no books exist`` () =
    let stub = StubBookRepository()
    stub.SetGetAll(emptyPagedResult())

    let result = (repo stub).GetAll 1 20 |> fun t -> t.Result

    result.Items  |> should be Empty
    result.TotalCount |> should equal 0L

// ──────────────────────────────────────────────────────────────────
// GetById
// ──────────────────────────────────────────────────────────────────

[<Fact>]
let ``GetById with valid id returns Some book`` () =
    let book = sampleBook "book-1" "Technology"
    let stub = StubBookRepository()
    stub.SetGetById(Some book)

    let result = (repo stub).GetById "book-1" "Technology" |> fun t -> t.Result

    result       |> should not' (equal None)
    result.Value.Title |> should equal "Clean Code"
    result.Value.Genre |> should equal "Technology"

[<Fact>]
let ``GetById with unknown id returns None`` () =
    let stub = StubBookRepository()
    stub.SetGetById(None)

    let result = (repo stub).GetById "unknown" "Technology" |> fun t -> t.Result

    result |> should equal None

// ──────────────────────────────────────────────────────────────────
// Create
// ──────────────────────────────────────────────────────────────────

[<Fact>]
let ``Create returns the created book with generated id`` () =
    let created = sampleBook "new-id" "Technology"
    let stub    = StubBookRepository()
    stub.SetCreate(created)

    let req    = sampleCreateRequest()
    let result = (repo stub).Create req |> fun t -> t.Result

    result.Id     |> should equal "new-id"
    result.Title  |> should equal "Clean Code"
    result.Authors |> should equal ["Robert C. Martin"]

[<Fact>]
let ``Create is called exactly once per request`` () =
    let stub = StubBookRepository()
    stub.SetCreate(sampleBook "x" "Technology")

    let req = sampleCreateRequest()
    (repo stub).Create req |> fun t -> t.Wait()

    stub.CreateCallCount |> should equal 1

// ──────────────────────────────────────────────────────────────────
// Update
// ──────────────────────────────────────────────────────────────────

[<Fact>]
let ``Update with valid id returns Some updated book`` () =
    let req     = sampleUpdateRequest()
    let updated = { sampleBook "book-1" "Technology" with Title = req.Title; Price = req.Price }
    let stub    = StubBookRepository()
    stub.SetUpdate(Some updated)

    let result = (repo stub).Update "book-1" "Technology" req |> fun t -> t.Result

    result       |> should not' (equal None)
    result.Value.Title |> should equal "Clean Code (Updated)"
    result.Value.Price |> should equal 39.99

[<Fact>]
let ``Update with unknown id returns None`` () =
    let stub = StubBookRepository()
    stub.SetUpdate(None)

    let req    = sampleUpdateRequest()
    let result = (repo stub).Update "unknown" "Technology" req |> fun t -> t.Result

    result |> should equal None

// ──────────────────────────────────────────────────────────────────
// Delete
// ──────────────────────────────────────────────────────────────────

[<Fact>]
let ``Delete with valid id returns true`` () =
    let stub = StubBookRepository()
    stub.SetDelete(true)

    let result = (repo stub).Delete "book-1" "Technology" |> fun t -> t.Result

    result |> should equal true

[<Fact>]
let ``Delete with unknown id returns false`` () =
    let stub = StubBookRepository()
    stub.SetDelete(false)

    let result = (repo stub).Delete "unknown" "Technology" |> fun t -> t.Result

    result |> should equal false
