namespace BookManagement.Helpers

open System
open Giraffe
open System.Net
open System.Threading.Tasks
open Microsoft.Azure.Cosmos
open Microsoft.AspNetCore.Http
open FSharp.Control
open Serilog

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

    let bindValue<'T> (ctx: HttpContext) : Task<'T option> =
        task {
            let! value = ctx.BindJsonAsync<'T>()
            return if isNull (box value) then None else Some value
        }

    let GetQueryRequestOptions() =
        let ro = QueryRequestOptions()
        ro.MaxBufferedItemCount <- 100 |> Nullable
        ro.MaxConcurrency <- 4 |> Nullable
        ro
    let queryCosmos<'a> (client: CosmosClient) database containerName (queryDefinition : QueryDefinition) =
        let ro = GetQueryRequestOptions()
        let container = client.GetContainer(database, containerName)
        container.GetItemQueryIterator<'a>(queryDefinition, null, ro)

    let queryCosmosAsyncSeqWithContainer<'a> (container: Container) (qro: QueryRequestOptions) (queryDefinition : QueryDefinition) =
        let feedIterator = container.GetItemQueryIterator<'a>(queryDefinition, null, qro)
        AsyncSeq.unfoldAsync (fun ((cnt : int), (ru: float), (queryResult: FeedIterator<'a>)) ->
            async {
                if queryResult.HasMoreResults then
                    let! result = queryResult.ReadNextAsync() |> Async.AwaitTask
                    let casted = result |> Array.ofSeq
                    return (casted, ((casted.Length + cnt) ,(result.RequestCharge + ru), queryResult)) |> Some
                else
                    return None
            }
        ) (0, 0.0, feedIterator)

    let queryCosmosAsyncSeq<'a> (client: CosmosClient) database containerName (queryDefinition : QueryDefinition) =
        let stopwatch = Diagnostics.Stopwatch.StartNew()
        let feedIterator = queryCosmos<'a> client database containerName queryDefinition
        AsyncSeq.unfoldAsync (fun ((cnt : int), (ru: float), (queryResult: FeedIterator<'a>)) ->
            async {
                if queryResult.HasMoreResults then
                    let! result = queryResult.ReadNextAsync() |> Async.AwaitTask
                    // logDebug "Request charge: %f" result.RequestCharge
                    let casted = result |> Array.ofSeq
                    stopwatch.Stop()
                    // logDebug "Timespent(client) %fs" ((stopwatch.ElapsedMilliseconds|>float) / 1000.0)
                    return (casted, ((casted.Length + cnt) ,(result.RequestCharge + ru), queryResult)) |> Some
                else
                    // logDebug $"Total record count: {cnt}"
                    let qry = queryDefinition.QueryText.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
                    match ru with
                    | x when x > 1000.0  ->
                        Log.Warning("Total RU spent: {ru}, QueryDefinition: {qry}", ru, qry)
                    | x when x > 100.0 ->
                        Log.Information("Total RU spent: {ru}, QueryDefinition: {qry}", ru, qry)
                    | _ ->
                        Log.Verbose("Total RU spent: {ru}, QueryDefinition: {qry}", ru, qry)
                    return None
            }
        ) (0, 0.0, feedIterator)
