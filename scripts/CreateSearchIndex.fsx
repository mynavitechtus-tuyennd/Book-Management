#!/usr/bin/env dotnet-fsi
// scripts/CreateSearchIndex.fsx
// Tạo (hoặc recreate) Azure AI Search index từ schema được định nghĩa bằng
// [SimpleField] / [SearchableField] attributes trên kiểu Book trong Models.fs.
//
// Cách chạy:
//   AZURE_SEARCH_ENDPOINT=https://xxx.search.windows.net \
//   AZURE_SEARCH_KEY=your-admin-key \
//   AZURE_SEARCH_INDEX=books \
//   dotnet fsi scripts/CreateSearchIndex.fsx

#r "nuget: Azure.Search.Documents, 11.6.0"
#r "nuget: Newtonsoft.Json, 13.0.3"
#load "../Models.fs"

open System
open Azure
open Azure.Search.Documents.Indexes
open Azure.Search.Documents.Indexes.Models
open BookManagement.Domain

// ── Config ───────────────────────────────────────────────────────────────────

let getEnv (name: string) =
    let v = Environment.GetEnvironmentVariable(name)
    if String.IsNullOrWhiteSpace(v) then
        failwithf "Missing required environment variable: %s" name
    v

let endpoint  = getEnv "AZURE_SEARCH_ENDPOINT"
let apiKey    = getEnv "AZURE_SEARCH_KEY"
let indexName = getEnv "AZURE_SEARCH_INDEX"

let buildIndexFields () =
    let builder = FieldBuilder()
    builder.Build(typeof<BookSearchDocument>)

// ── Create or update index ────────────────────────────────────────────────────

printfn "Connecting to Azure AI Search: %s" endpoint
printfn "Index name: %s" indexName

let credential  = AzureKeyCredential(apiKey)
let indexClient = SearchIndexClient(Uri(endpoint), credential)
let fields      = buildIndexFields()
let index       = SearchIndex(indexName, fields)

printfn "Creating/updating index with %d fields..." fields.Count

try
    let result = indexClient.CreateOrUpdateIndex(index)
    printfn "Index '%s' ready." result.Value.Name
    printfn "   Fields: %s" (String.Join(", ", result.Value.Fields |> Seq.map (fun f -> f.Name)))
with ex ->
    printfn "Failed to create/update index: %s" ex.Message
    exit 1
