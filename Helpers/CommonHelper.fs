namespace BookManagement.Helpers

open System
open Giraffe
open System.Net

module CommonHelper = 

    // Handle bad request error and return 400 status code
    let badRequest (msg: string): HttpHandler  =
        setStatusCode(int HttpStatusCode.BadRequest) >=> json {| message = msg |}

    // Handle not found error and return 404 status code
    let notFound (msg: string): HttpHandler  =
        setStatusCode(int HttpStatusCode.NotFound) >=> json {| message = msg |}

    // Handle internal server error and return 500 status code
    let internalError (ex: Exception): HttpHandler  =
        setStatusCode(int HttpStatusCode.InternalServerError) >=> json {| message = ex.Message |}

    // Try parse int from string
    let tryParseInt (s: string) =
        match Int32.TryParse(s) with
        | true, v -> Some v
        | _       -> None