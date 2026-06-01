namespace BookManagement.Helpers

open System
open Giraffe
open System.Net
open System.Threading.Tasks
open Microsoft.Azure.Cosmos
open  Microsoft.AspNetCore.Http

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

    /// Drains all pages of a count FeedIterator, summing the values.
    let rec sumPages (iterator: FeedIterator<int>) (acc: int64) : Task<int64> =
        task {
            if not iterator.HasMoreResults then
                return acc
            else
                let! page  = iterator.ReadNextAsync()
                let total  = page |> Seq.sumBy int64
                return! sumPages iterator (acc + total)
        }

    /// Drains all pages of a FeedIterator into a single list using tail recursion.
    let rec collectPages (iterator: FeedIterator<'T>) (acc: 'T list) : Task<'T list> =
        task {
            if not iterator.HasMoreResults then
                return List.rev acc
            else
                let! page = iterator.ReadNextAsync()
                let acc'  = page |> Seq.fold (fun a item -> item :: a) acc
                return! collectPages iterator acc'
        }
