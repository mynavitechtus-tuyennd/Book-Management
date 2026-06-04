module BookManagement.Tests.Handlers.AuthHandlerUnitTests

open System
open System.Text.Json
open System.Collections.Generic
open System.Net
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Configuration
open Giraffe
open Xunit
open FsUnit.Xunit
open BookManagement.Handlers.AuthHttpHandler
open BookManagement.Tests.Helpers.TestHelpers

// ──────────────────────────────────────────────────────────────────
// Helper
// ──────────────────────────────────────────────────────────────────

let private loginCtx (body: obj) =
    createFakeContext "POST" "/api/auth/login" [] (Some (JsonSerializer.Serialize(body)))

/// Register a real in-memory IConfiguration so the handler can read JWT settings.
/// Called only for the 200-success path tests.
/// NOTE: rebuilds RequestServices preserving Giraffe + adding IConfiguration.
let private addConfig (ctx: HttpContext) =
    let pairs : KeyValuePair<string, string> list = [
        KeyValuePair("Jwt:Issuer",    "TestIssuer")
        KeyValuePair("Jwt:Audience",  "TestAudience")
        KeyValuePair("Jwt:SecretKey", "SuperSecretKeyForTestingPurposesOnly123!")
    ]
    let config : IConfiguration =
        ConfigurationBuilder()
            .AddInMemoryCollection(pairs)
            .Build() :> IConfiguration
    let services = ServiceCollection()
    services.AddGiraffe() |> ignore
    services.AddSingleton<IConfiguration>(config) |> ignore
    ctx.RequestServices <- services.BuildServiceProvider()


// ──────────────────────────────────────────────────────────────────
// POST /api/auth/login — validation (400 paths)
// ──────────────────────────────────────────────────────────────────

[<Fact>]
let ``login with null body returns 400`` () =
    // Send a literal JSON null → bindValue returns None
    let ctx = createFakeContext "POST" "/api/auth/login" [] (Some "null")

    let status = runHandler login ctx

    status |> should equal (int HttpStatusCode.BadRequest)

[<Fact>]
let ``login with empty username returns 400`` () =
    let ctx = loginCtx {| Username = ""; Password = "Admin@123" |}

    let status = runHandler login ctx

    status |> should equal (int HttpStatusCode.BadRequest)

[<Fact>]
let ``login with whitespace username returns 400`` () =
    let ctx = loginCtx {| Username = "   "; Password = "Admin@123" |}

    let status = runHandler login ctx

    status |> should equal (int HttpStatusCode.BadRequest)

[<Fact>]
let ``login with empty password returns 400`` () =
    let ctx = loginCtx {| Username = "admin"; Password = "" |}

    let status = runHandler login ctx

    status |> should equal (int HttpStatusCode.BadRequest)

[<Fact>]
let ``login with whitespace password returns 400`` () =
    let ctx = loginCtx {| Username = "admin"; Password = "   " |}

    let status = runHandler login ctx

    status |> should equal (int HttpStatusCode.BadRequest)

[<Fact>]
let ``login with invalid JSON body returns 400`` () =
    let ctx = createFakeContext "POST" "/api/auth/login" [] (Some "{ invalid json }")

    let status = runHandler login ctx

    status |> should equal (int HttpStatusCode.BadRequest)

// ──────────────────────────────────────────────────────────────────
// POST /api/auth/login — wrong credentials (401 path)
// ──────────────────────────────────────────────────────────────────

[<Fact>]
let ``login with wrong password returns 401`` () =
    let ctx = loginCtx {| Username = "admin"; Password = "WrongPassword" |}
    addConfig ctx

    let status = runHandler login ctx

    status |> should equal (int HttpStatusCode.Unauthorized)

[<Fact>]
let ``login with unknown username returns 401`` () =
    let ctx = loginCtx {| Username = "nobody"; Password = "Admin@123" |}
    addConfig ctx

    let status = runHandler login ctx

    status |> should equal (int HttpStatusCode.Unauthorized)

[<Fact>]
let ``login with correct case-sensitive password mismatch returns 401`` () =
    // Password "admin@123" vs stored "Admin@123" → case sensitive
    let ctx = loginCtx {| Username = "admin"; Password = "admin@123" |}
    addConfig ctx

    let status = runHandler login ctx

    status |> should equal (int HttpStatusCode.Unauthorized)

// ──────────────────────────────────────────────────────────────────
// POST /api/auth/login — successful login (200 path)
// ──────────────────────────────────────────────────────────────────

[<Fact>]
let ``login with correct admin credentials returns 200`` () =
    let ctx = loginCtx {| Username = "admin"; Password = "Admin@123" |}
    addConfig ctx

    let status = runHandler login ctx

    status |> should equal (int HttpStatusCode.OK)

[<Fact>]
let ``login with correct user credentials returns 200`` () =
    let ctx = loginCtx {| Username = "user"; Password = "User@123" |}
    addConfig ctx

    let status = runHandler login ctx

    status |> should equal (int HttpStatusCode.OK)
