#!/usr/bin/env dotnet-fsi
// scripts/RebuildSearchIndex.fsx
// Xóa và tạo lại Azure Search index, sau đó re-index toàn bộ data từ Cosmos DB.
//
// Dùng khi:
//   - Thay đổi schema của Search index (thêm/xóa field, đổi filterable/searchable)
//   - Cosmos DB data vẫn còn nguyên, chỉ cần đồng bộ lại lên Search
//
// Cách chạy:
//   COSMOS_CONNECTION_STRING="AccountEndpoint=..." \
//   COSMOS_DATABASE="BookManagement" \
//   COSMOS_CONTAINER="books" \
//   AZURE_SEARCH_ENDPOINT="https://xxx.search.windows.net" \
//   AZURE_SEARCH_KEY="your-admin-key" \
//   AZURE_SEARCH_INDEX="books-index" \
//   dotnet fsi scripts/RebuildSearchIndex.fsx

#r "nuget: Microsoft.Azure.Cosmos, 3.47.0"
#r "nuget: Azure.Search.Documents, 11.6.0"
#r "nuget: Newtonsoft.Json, 13.0.3"
#load "../Models.fs"

open System
open Azure
open Azure.Search.Documents
open Azure.Search.Documents.Indexes
open Azure.Search.Documents.Indexes.Models
open Azure.Search.Documents.Models
open Microsoft.Azure.Cosmos
open BookManagement.Domain

// ── Config từ biến môi trường ─────────────────────────────────────────────────

let getEnv (name: string) =
    let v = Environment.GetEnvironmentVariable(name)
    if String.IsNullOrWhiteSpace(v) then
        failwithf "Missing required environment variable: %s" name
    v

let cosmosConnStr   = getEnv "COSMOS_CONNECTION_STRING"
let cosmoDbName     = getEnv "COSMOS_DATABASE"
let cosmosContainer = getEnv "COSMOS_CONTAINER"
let searchEndpoint  = getEnv "AZURE_SEARCH_ENDPOINT"
let searchApiKey    = getEnv "AZURE_SEARCH_KEY"
let searchIndexName = getEnv "AZURE_SEARCH_INDEX"

// ── Preview ───────────────────────────────────────────────────────────────────

printfn ""
printfn " REBUILD AZURE SEARCH INDEX"
printfn "   Cosmos DB (data kept): %s / %s" cosmoDbName cosmosContainer
printfn "   Search index (will be recreated): %s @ %s" searchIndexName searchEndpoint
printfn ""
printfn "This will DELETE and RECREATE the Search index, then re-index all Cosmos DB data."
printfn "Cosmos DB data will NOT be deleted. Type 'yes' to confirm:"

let confirm = Console.ReadLine()
if confirm.Trim() <> "yes" then
    printfn "Aborted."
    exit 0

let searchCredential = AzureKeyCredential(searchApiKey)
let indexClient      = SearchIndexClient(Uri(searchEndpoint), searchCredential)
let searchClient     = SearchClient(Uri(searchEndpoint), searchIndexName, searchCredential)

// ── Step 1: Xóa Search index cũ ──────────────────────────────────────────────

printfn ""
printfn "[1/3] Deleting Azure Search index '%s'..." searchIndexName

try
    indexClient.DeleteIndex(searchIndexName) |> ignore
    printfn "Index deleted."
with ex ->
    printfn "Warning: %s" ex.Message

// ── Step 2: Tạo lại Search index với schema từ BookSearchDocument ─────────────

printfn ""
printfn "[2/3] Creating new Search index from BookSearchDocument schema..."

let fields =
    let builder = FieldBuilder()
    builder.Build(typeof<BookSearchDocument>)

let index = SearchIndex(searchIndexName, fields)

try
    let result = indexClient.CreateOrUpdateIndex(index)
    printfn "Index '%s' created with %d fields." result.Value.Name result.Value.Fields.Count
    printfn "Fields: %s" (String.Join(", ", result.Value.Fields |> Seq.map (fun f -> f.Name)))
with ex ->
    printfn "Failed to create index: %s" ex.Message
    exit 1

// ── Step 3: Re-index toàn bộ data từ Cosmos DB ───────────────────────────────

printfn ""
printfn "[3/3] Re-indexing all documents from Cosmos DB to Azure Search..."

let cosmosClient =
    new CosmosClient(
        cosmosConnStr,
        CosmosClientOptions(
            SerializerOptions = CosmosSerializationOptions(
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase)))

let container = cosmosClient.GetContainer(cosmoDbName, cosmosContainer)

// Drain all pages
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

// Index một batch, trả về (succeeded, failed)
let indexBatch (books: Book array) =
    let docs  = books |> Array.map BookSearchIndexConversion.toSearchDoc
    let batch = IndexDocumentsBatch.MergeOrUpload(docs)
    try
        let result    = searchClient.IndexDocuments(batch)
        let succeeded = result.Value.Results |> Seq.filter (fun r -> r.Succeeded)     |> Seq.length
        let failed    = result.Value.Results |> Seq.filter (fun r -> not r.Succeeded) |> Seq.length
        printfn "Batch: %d indexed, %d failed" succeeded failed
        (succeeded, failed)
    with ex ->
        printfn "Error indexing batch: %s" ex.Message
        (0, books.Length)

let allBooks =
    container.GetItemQueryIterator<Book>(QueryDefinition("SELECT * FROM c"))
    |> drainPages
    |> Seq.chunkBySize 100
    |> Seq.toList

let totals =
    allBooks
    |> List.map indexBatch
    |> List.fold (fun (accS, accF) (s, f) -> (accS + s, accF + f)) (0, 0)

let totalIndexed, totalFailed = totals

printfn ""
if totalFailed = 0 then
    printfn "Done. Total indexed: %d documents" totalIndexed
else
    printfn "Done. Indexed: %d, Failed: %d" totalIndexed totalFailed
    exit 1
