module BookManagement.Tests.Handlers.SearchHandlerUnitTests

open System.IO
open System.Net
open Xunit
open FsUnit.Xunit
open Microsoft.Extensions.DependencyInjection
open Giraffe
open BookManagement.Domain
open BookManagement.Infrastructure.Abstractions
open BookManagement.Handlers.SearchHttpHandler
open BookManagement.Tests.Helpers.TestHelpers

let private getSearchCtx (searchStub: StubSearchService) (repoStub: StubBookRepository) (queryParams: (string * string) list) =
    let ctx = createFakeContext "GET" "/api/books/search" queryParams None
    let services = ServiceCollection()
    services.AddGiraffe() |> ignore
    services.AddSingleton<ISearchService>(searchStub :> ISearchService) |> ignore
    services.AddSingleton<IBookRepository>(repoStub :> IBookRepository) |> ignore
    services.AddSingleton<ISearchQueryService>(BookManagement.Application.SearchQueryService(searchStub, repoStub) :> ISearchQueryService) |> ignore
    ctx.RequestServices <- services.BuildServiceProvider()
    ctx

[<Fact>]
let ``search with no query params uses defaults and returns 200`` () =
    let stub = StubSearchService()
    let repo = StubBookRepository()
    let ctx = getSearchCtx stub repo []

    let status = runHandler (bindModel<SearchRequest> None search) ctx

    status |> should equal (int HttpStatusCode.OK)
    
    let lastReq = stub.LastSearchRequest.Value
    lastReq.Query |> should equal "*"
    lastReq.Genre |> should equal None
    lastReq.Page |> should equal 1
    lastReq.Size |> should equal 10

[<Fact>]
let ``search with explicit valid query params constructs correct request`` () =
    let stub = StubSearchService()
    let repo = StubBookRepository()
    let ctx = getSearchCtx stub repo [("query", "clean code"); ("genre", "Technology"); ("page", "2"); ("size", "25")]

    let status = runHandler (bindModel<SearchRequest> None search) ctx

    status |> should equal (int HttpStatusCode.OK)
    
    let lastReq = stub.LastSearchRequest.Value
    lastReq.Query |> should equal "clean code"
    lastReq.Genre |> should equal (Some "Technology")
    lastReq.Page |> should equal 2
    lastReq.Size |> should equal 25

[<Fact>]
let ``search with invalid page parameter uses default page 1`` () =
    let stub = StubSearchService()
    let repo = StubBookRepository()
    let ctx = getSearchCtx stub repo [("page", "not-a-number")]

    let status = runHandler (bindModel<SearchRequest> None search) ctx

    status |> should equal (int HttpStatusCode.OK)
    
    let lastReq = stub.LastSearchRequest.Value
    lastReq.Page |> should equal 1

[<Fact>]
let ``search with invalid size parameter uses default size 10`` () =
    let stub = StubSearchService()
    let repo = StubBookRepository()
    let ctx = getSearchCtx stub repo [("size", "not-a-number")]

    let status = runHandler (bindModel<SearchRequest> None search) ctx

    status |> should equal (int HttpStatusCode.OK)
    
    let lastReq = stub.LastSearchRequest.Value
    lastReq.Size |> should equal 10

[<Fact>]
let ``search clamps page to minimum 1`` () =
    let stub = StubSearchService()
    let repo = StubBookRepository()
    
    let ctx1 = getSearchCtx stub repo [("page", "0")]
    let status1 = runHandler (bindModel<SearchRequest> None search) ctx1
    status1 |> should equal (int HttpStatusCode.OK)
    stub.LastSearchRequest.Value.Page |> should equal 1

    let ctx2 = getSearchCtx stub repo [("page", "-5")]
    let status2 = runHandler (bindModel<SearchRequest> None search) ctx2
    status2 |> should equal (int HttpStatusCode.OK)
    stub.LastSearchRequest.Value.Page |> should equal 1

[<Fact>]
let ``search clamps size to minimum 1`` () =
    let stub = StubSearchService()
    let repo = StubBookRepository()
    
    let ctx1 = getSearchCtx stub repo [("size", "0")]
    let status1 = runHandler (bindModel<SearchRequest> None search) ctx1
    status1 |> should equal (int HttpStatusCode.OK)
    stub.LastSearchRequest.Value.Size |> should equal 10

    let ctx2 = getSearchCtx stub repo [("size", "-10")]
    let status2 = runHandler (bindModel<SearchRequest> None search) ctx2
    status2 |> should equal (int HttpStatusCode.OK)
    stub.LastSearchRequest.Value.Size |> should equal 10

[<Fact>]
let ``search clamps size to maximum 100`` () =
    let stub = StubSearchService()
    let repo = StubBookRepository()
    let ctx = getSearchCtx stub repo [("size", "150")]

    let status = runHandler (bindModel<SearchRequest> None search) ctx
    
    status |> should equal (int HttpStatusCode.OK)
    stub.LastSearchRequest.Value.Size |> should equal 100

[<Fact>]
let ``search returns paged results in json response`` () =
    let sampleBook1 = sampleBook "book-1" "Technology"
    let result = singlePagedResult sampleBook1
    let stub = StubSearchService(result)
    let repo = StubBookRepository()
    let ctx = getSearchCtx stub repo []

    let status = runHandler (bindModel<SearchRequest> None search) ctx

    status |> should equal (int HttpStatusCode.OK)
    
    // Read the response body stream
    ctx.Response.Body.Position <- 0L
    use reader = new StreamReader(ctx.Response.Body)
    let body = reader.ReadToEnd()
    
    Assert.Contains("book-1", body)
    Assert.Contains("Clean Code", body)
