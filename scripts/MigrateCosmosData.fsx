#!/usr/bin/env dotnet-fsi
// scripts/MigrateCosmosData.fsx
// Script migration để cập nhật cấu trúc data trong Cosmos DB.
//
// Mục đích: Thực hiện các thay đổi schema/data trực tiếp trong Cosmos DB
// mà không ảnh hưởng đến Search index.
//
// use case hiện tại: Đổi tên field author (string) → authors (string list)
//
// Cách chạy:
//   COSMOS_CONNECTION_STRING="AccountEndpoint=..." \
//   COSMOS_DATABASE="BookManagement" \
//   COSMOS_CONTAINER="books" \
//   dotnet fsi scripts/MigrateCosmosData.fsx

#r "nuget: Microsoft.Azure.Cosmos, 3.47.0"
#r "nuget: Newtonsoft.Json, 13.0.3"

open System
open Microsoft.Azure.Cosmos
open Newtonsoft.Json.Linq

// ── Config từ biến môi trường ─────────────────────────────────────────────────

let getEnv (name: string) =
    let v = Environment.GetEnvironmentVariable(name)
    if String.IsNullOrWhiteSpace(v) then
        failwithf "Missing required environment variable: %s" name
    v

let cosmosConnStr   = getEnv "COSMOS_CONNECTION_STRING"
let cosmoDbName     = getEnv "COSMOS_DATABASE"
let cosmosContainer = getEnv "COSMOS_CONTAINER"

// ── Preview ───────────────────────────────────────────────────────────────────

printfn ""
printfn "COSMOS DB MIGRATION SCRIPT"
printfn "   Database  : %s" cosmoDbName
printfn "   Container : %s" cosmosContainer
printfn ""
printfn "This script will update documents in Cosmos DB."
printfn "Type 'yes' to confirm:"

let confirm = Console.ReadLine()
if confirm.Trim() <> "yes" then
    printfn "Aborted."
    exit 0

// ── Connect ───────────────────────────────────────────────────────────────────

let cosmosClient =
    new CosmosClient(
        cosmosConnStr,
        CosmosClientOptions(
            SerializerOptions = CosmosSerializationOptions(
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase)))

let container = cosmosClient.GetContainer(cosmoDbName, cosmosContainer)

// ── Migration logic ───────────────────────────────────────────────────────────
// Migrate field author (string) → authors (string list)
// Chỉ xử lý các documents có field "author" cũ và chưa có field "authors" mới.

let queryDef =
    QueryDefinition("SELECT c.id, c.genre, c.author FROM c WHERE IS_DEFINED(c.author) AND NOT IS_DEFINED(c.authors)")

// Drain all pages into a flat list (functional)
let drainPages (iterator: FeedIterator<'T>) =
    Seq.initInfinite id
    |> Seq.takeWhile (fun _ -> iterator.HasMoreResults)
    |> Seq.collect (fun _ ->
        try
            iterator.ReadNextAsync()
            |> Async.AwaitTask
            |> Async.RunSynchronously
            |> Seq.toList
        with ex ->
            printfn "Error reading page: %s" ex.Message
            [])

// Patch một document: thêm field "authors" và xóa field "author"
// Trả về true nếu thành công, false nếu lỗi
let patchDoc (doc: JObject) =
    let id        = doc.["id"]     |> string
    let genre     = doc.["genre"]  |> string
    let oldAuthor = doc.["author"] |> string
    let patches = [|
        PatchOperation.Add("/authors", [| oldAuthor |])
        PatchOperation.Remove("/author")
    |]
    try
        container.PatchItemAsync<JObject>(id, PartitionKey(genre), patches)
        |> Async.AwaitTask
        |> Async.RunSynchronously
        |> ignore
        printfn "   Patched: %s (author='%s')" id oldAuthor
        true
    with ex ->
        printfn "   Failed : %s — %s" id ex.Message
        false

let docs = drainPages (container.GetItemQueryIterator<JObject>(queryDef)) |> Seq.toList
let count = docs |> List.length

printfn ""
printfn "Found %d document(s) to migrate." count

let results  = docs |> List.map patchDoc
let patched  = results |> List.filter id  |> List.length
let failed   = results |> List.filter not |> List.length

printfn ""
if failed = 0 then
    printfn "Migration complete: %d document(s) patched." patched
else
    printfn "Migration finished: %d patched, %d failed." patched failed
    exit 1
