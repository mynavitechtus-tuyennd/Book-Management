module BookManagement.Tests.Infrastructure.MappingTests

open System
open Xunit
open FsUnit.Xunit
open BookManagement.Domain
open BookManagement.Tests.Helpers.TestHelpers

[<Fact>]
let ``toSearchDoc maps all fields correctly`` () =
    let book = { sampleBook "doc-1" "Technology" with
                    Title   = "Clean Code"
                    Authors = ["Robert C. Martin"; "Co Author"]
                    Price   = 35.99
                    Stock   = 10 }

    let doc = BookSearchIndexConversion.toSearchDoc book

    doc.Id            |> should equal "doc-1"
    doc.Title         |> should equal "Clean Code"
    doc.Authors       |> should equal ["Robert C. Martin"; "Co Author"]
    doc.Isbn          |> should equal book.Isbn
    doc.Publisher     |> should equal book.Publisher
    doc.PublishedYear |> should equal book.PublishedYear
    doc.Genre         |> should equal "Technology"
    doc.Description   |> should equal book.Description
    doc.Price         |> should equal 35.99
    doc.Stock         |> should equal 10
    doc.CreatedAt     |> should equal book.CreatedAt
    doc.UpdatedAt     |> should equal book.UpdatedAt

[<Fact>]
let ``toBook maps all fields correctly`` () =
    let doc : BookSearchDocument =
        {
            Id            = "doc-2"
            Title         = "F# for Fun and Profit"
            Authors       = ["Scott Wlaschin"]
            Isbn          = "9781234567890"
            Publisher     = "Manning"
            PublishedYear = 2023
            Genre         = "Technology"
            Description   = "A great F# book"
            Price         = 29.99
            Stock         = 50
            CreatedAt     = DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            UpdatedAt     = DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        }

    let book = BookSearchIndexConversion.toBook doc

    book.Id            |> should equal "doc-2"
    book.Title         |> should equal "F# for Fun and Profit"
    book.Authors       |> should equal ["Scott Wlaschin"]
    book.Isbn          |> should equal "9781234567890"
    book.Publisher     |> should equal "Manning"
    book.PublishedYear |> should equal 2023
    book.Genre         |> should equal "Technology"
    book.Description   |> should equal "A great F# book"
    book.Price         |> should equal 29.99
    book.Stock         |> should equal 50
    book.CreatedAt     |> should equal doc.CreatedAt
    book.UpdatedAt     |> should equal doc.UpdatedAt
