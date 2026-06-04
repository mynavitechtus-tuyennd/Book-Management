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
#load "EnvHelper.fsx"
#load "../Models.fs"

open System
open Azure
open Azure.Search.Documents.Indexes
open Azure.Search.Documents.Indexes.Models
open BookManagement.Domain
open EnvHelper

// ── Config ───────────────────────────────────────────────────────────────────

let endpoint  = EnvHelper.searchEndpoint
let apiKey    = EnvHelper.searchApiKey
let indexName = EnvHelper.searchIndexName

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

let result = indexClient.CreateOrUpdateIndex(index)
printfn "✅ Index '%s' ready." result.Value.Name
printfn "   Fields: %s" (String.Join(", ", result.Value.Fields |> Seq.map (fun f -> f.Name)))
