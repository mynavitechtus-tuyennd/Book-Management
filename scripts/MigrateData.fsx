#!/usr/bin/env dotnet-fsi
// scripts/MigrateData.fsx
// Script migration cho breaking change: Author (string) → Authors (string list)
//
// Script này sẽ:
//   1. Xóa toàn bộ documents trong Cosmos DB container
//   2. Xóa Azure Search index cũ
//   3. Tạo lại Azure Search index với schema mới (Collection(Edm.String) cho authors)
//
// ⚠️  CẢNH BÁO: Script này XÓA TOÀN BỘ DỮ LIỆU.
//     Hãy backup trước khi chạy trên môi trường production.
//     Trong môi trường dev, chạy trực tiếp.
//
// Cách chạy:
//   COSMOS_CONNECTION_STRING="AccountEndpoint=..." \
//   COSMOS_DATABASE=BookManagement \
//   COSMOS_CONTAINER=Books \
//   AZURE_SEARCH_ENDPOINT=https://xxx.search.windows.net \
//   AZURE_SEARCH_KEY=your-admin-key \
//   AZURE_SEARCH_INDEX=books \
//   dotnet fsi scripts/MigrateData.fsx

#r "nuget: Microsoft.Azure.Cosmos, 3.47.0"
#r "nuget: Azure.Search.Documents, 11.6.0"
#r "nuget: Newtonsoft.Json, 13.0.3"
#load "../Models.fs"

open System
open Azure
open Azure.Search.Documents.Indexes
open Azure.Search.Documents.Indexes.Models
open Microsoft.Azure.Cosmos
open BookManagement.Domain

// ── Config từ environment variables ──────────────────────────────────────────

let getEnv key =
    let v = Environment.GetEnvironmentVariable(key)
    if String.IsNullOrWhiteSpace(v) then
        failwithf "Environment variable '%s' is required but not set." key
    v

let cosmosConnStr   = getEnv "COSMOS_CONNECTION_STRING"
let cosmoDbName     = getEnv "COSMOS_DATABASE"
let cosmosContainer = getEnv "COSMOS_CONTAINER"
let searchEndpoint  = getEnv "AZURE_SEARCH_ENDPOINT"
let searchApiKey    = getEnv "AZURE_SEARCH_KEY"
let searchIndexName = getEnv "AZURE_SEARCH_INDEX"

// ── Confirm trước khi xóa ─────────────────────────────────────────────────────

printfn ""
printfn "⚠️  MIGRATION SCRIPT - Breaking change: Author → Authors"
printfn "   Cosmos DB: %s / %s" cosmoDbName cosmosContainer
printfn "   Search index: %s @ %s" searchIndexName searchEndpoint
printfn ""
printfn "This will DELETE ALL DATA. Type 'yes' to confirm:"
let confirm = Console.ReadLine()
if confirm.Trim() <> "yes" then
    printfn "Aborted."
    exit 0

// ── Step 1: Xóa toàn bộ Cosmos DB container và recreate ──────────────────────

printfn ""
printfn "[1/3] Deleting Cosmos DB container '%s'..." cosmosContainer

let cosmosOpts = CosmosClientOptions(
                    SerializerOptions = CosmosSerializationOptions(
                        PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase))
let cosmosClient = new CosmosClient(cosmosConnStr, cosmosOpts)
let db = cosmosClient.GetDatabase(cosmoDbName)

try
    db.GetContainer(cosmosContainer).DeleteContainerAsync()
    |> Async.AwaitTask |> Async.RunSynchronously |> ignore
    printfn "   Container deleted."
with ex ->
    printfn "   Warning: %s" ex.Message

// Recreate container với partition key /genre
printfn "   Recreating container with partition key /genre..."
let containerProps = ContainerProperties(cosmosContainer, "/genre")
db.CreateContainerIfNotExistsAsync(containerProps, Nullable(400))
|> Async.AwaitTask |> Async.RunSynchronously |> ignore
printfn "   Container '%s' ready." cosmosContainer

// ── Step 2: Xóa Azure Search index cũ ────────────────────────────────────────

printfn ""
printfn "[2/3] Deleting Azure Search index '%s'..." searchIndexName

let searchCredential  = AzureKeyCredential(searchApiKey)
let indexClient = SearchIndexClient(Uri(searchEndpoint), searchCredential)

try
    indexClient.DeleteIndex(searchIndexName) |> ignore
    printfn "   Index deleted."
with ex ->
    printfn "   Warning: %s" ex.Message

// ── Step 3: Tạo lại Search index với schema mới ───────────────────────────────

printfn ""
printfn "[3/3] Creating new Search index with updated schema (authors as Collection)..."

let fields = 
    let builder = FieldBuilder()
    builder.Build(typeof<Book>)

let index  = SearchIndex(searchIndexName, fields)
let result = indexClient.CreateOrUpdateIndex(index)
printfn "   Index '%s' created with %d fields." result.Value.Name result.Value.Fields.Count

printfn ""
printfn "✅ Migration complete."
printfn "   Next steps:"
printfn "   - Re-seed data via POST /api/books with the new { authors: [...] } format"
printfn "   - Documents will be automatically indexed in Azure Search on creation"
