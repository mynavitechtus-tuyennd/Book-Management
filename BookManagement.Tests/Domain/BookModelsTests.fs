module BookManagement.Tests.Domain.BookModelsTests

open System
open Xunit
open FsUnit.Xunit
open BookManagement.Domain
open BookManagement.Tests.Helpers.TestHelpers

// ──────────────────────────────────────────────────────────────────
// Book.fromCreateRequest
// ──────────────────────────────────────────────────────────────────

[<Fact>]
let ``fromCreateRequest maps all fields correctly`` () =
    let req = sampleCreateRequest()

    let book = Book.fromCreateRequest req

    book.Title         |> should equal req.Title
    book.Authors       |> should equal req.Authors
    book.Isbn          |> should equal req.Isbn
    book.Publisher     |> should equal req.Publisher
    book.PublishedYear |> should equal req.PublishedYear
    book.Genre         |> should equal req.Genre
    book.Description   |> should equal req.Description
    book.Price         |> should equal req.Price
    book.Stock         |> should equal req.Stock

[<Fact>]
let ``fromCreateRequest generates a valid GUID for Id`` () =
    let req  = sampleCreateRequest()
    let book = Book.fromCreateRequest req

    let mutable guid = Guid.Empty
    Guid.TryParse(book.Id, &guid) |> should equal true
    guid |> should not' (equal Guid.Empty)

[<Fact>]
let ``fromCreateRequest sets CreatedAt and UpdatedAt close to UtcNow`` () =
    let before = DateTime.UtcNow
    let book   = Book.fromCreateRequest (sampleCreateRequest())
    let after  = DateTime.UtcNow

    book.CreatedAt |> should be (greaterThanOrEqualTo before)
    book.CreatedAt |> should be (lessThanOrEqualTo after)
    book.UpdatedAt |> should be (greaterThanOrEqualTo before)
    book.UpdatedAt |> should be (lessThanOrEqualTo after)

[<Fact>]
let ``fromCreateRequest sets CreatedAt equal to UpdatedAt`` () =
    let book = Book.fromCreateRequest (sampleCreateRequest())

    // Both are set from the same `now` binding — should be equal
    book.CreatedAt |> should equal book.UpdatedAt

[<Fact>]
let ``fromCreateRequest generates unique Id for each call`` () =
    let req  = sampleCreateRequest()
    let id1  = (Book.fromCreateRequest req).Id
    let id2  = (Book.fromCreateRequest req).Id

    id1 |> should not' (equal id2)

// ──────────────────────────────────────────────────────────────────
// Book.applyUpdate
// ──────────────────────────────────────────────────────────────────

[<Fact>]
let ``applyUpdate updates all mutable fields`` () =
    let existing = sampleBook "book-1" "Technology"
    let req      = sampleUpdateRequest()

    let updated = Book.applyUpdate req existing

    updated.Title         |> should equal req.Title
    updated.Authors       |> should equal req.Authors
    updated.Isbn          |> should equal req.Isbn
    updated.Publisher     |> should equal req.Publisher
    updated.PublishedYear |> should equal req.PublishedYear
    updated.Description   |> should equal req.Description
    updated.Price         |> should equal req.Price
    updated.Stock         |> should equal req.Stock

[<Fact>]
let ``applyUpdate preserves Id`` () =
    let existing = sampleBook "book-original-id" "Technology"
    let updated  = Book.applyUpdate (sampleUpdateRequest()) existing

    updated.Id |> should equal "book-original-id"

[<Fact>]
let ``applyUpdate preserves Genre (partition key)`` () =
    let existing = sampleBook "book-1" "Science"
    let updated  = Book.applyUpdate (sampleUpdateRequest()) existing

    updated.Genre |> should equal "Science"

[<Fact>]
let ``applyUpdate preserves CreatedAt`` () =
    let fixedDate = DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc)
    let existing  = { sampleBook "book-1" "Technology" with CreatedAt = fixedDate }
    let updated   = Book.applyUpdate (sampleUpdateRequest()) existing

    updated.CreatedAt |> should equal fixedDate

[<Fact>]
let ``applyUpdate sets UpdatedAt to a time after CreatedAt`` () =
    let oldDate  = DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    let existing = { sampleBook "book-1" "Technology" with UpdatedAt = oldDate }
    let before   = DateTime.UtcNow

    let updated = Book.applyUpdate (sampleUpdateRequest()) existing

    updated.UpdatedAt |> should be (greaterThanOrEqualTo before)
