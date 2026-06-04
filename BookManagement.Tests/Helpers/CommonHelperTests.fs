module BookManagement.Tests.Helpers.CommonHelperTests

open Xunit
open FsUnit.Xunit
open BookManagement.Helpers.CommonHelper

// ──────────────────────────────────────────────────────────────────
// tryParseInt
// ──────────────────────────────────────────────────────────────────

[<Fact>]
let ``tryParseInt with valid positive integer returns Some`` () =
    tryParseInt "42" |> should equal (Some 42)

[<Fact>]
let ``tryParseInt with zero returns Some 0`` () =
    tryParseInt "0" |> should equal (Some 0)

[<Fact>]
let ``tryParseInt with negative integer returns Some`` () =
    tryParseInt "-5" |> should equal (Some -5)

[<Fact>]
let ``tryParseInt with large valid integer returns Some`` () =
    tryParseInt "2147483647" |> should equal (Some System.Int32.MaxValue)

[<Fact>]
let ``tryParseInt with non-numeric string returns None`` () =
    tryParseInt "abc" |> should equal None

[<Fact>]
let ``tryParseInt with empty string returns None`` () =
    tryParseInt "" |> should equal None

[<Fact>]
let ``tryParseInt with whitespace string returns None`` () =
    tryParseInt "   " |> should equal None

[<Fact>]
let ``tryParseInt with null returns None`` () =
    tryParseInt null |> should equal None

[<Fact>]
let ``tryParseInt with decimal number string returns None`` () =
    tryParseInt "3.14" |> should equal None

[<Fact>]
let ``tryParseInt with integer overflow returns None`` () =
    // Int32.MaxValue + 1
    tryParseInt "2147483648" |> should equal None

[<Fact>]
let ``tryParseInt with mixed alphanumeric returns None`` () =
    tryParseInt "12abc" |> should equal None

[<Theory>]
[<InlineData("1", 1)>]
[<InlineData("10", 10)>]
[<InlineData("100", 100)>]
[<InlineData("-1", -1)>]
[<InlineData("-999", -999)>]
let ``tryParseInt with various valid values returns expected Some`` (input: string, expected: int) =
    tryParseInt input |> should equal (Some expected)

[<Theory>]
[<InlineData("abc")>]
[<InlineData("")>]
[<InlineData("1.5")>]
[<InlineData("999999999999")>]
let ``tryParseInt with various invalid values returns None`` (input: string) =
    tryParseInt input |> should equal None
