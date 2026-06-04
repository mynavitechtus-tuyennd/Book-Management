module BookManagement.Tests.SearchHandlerTests

open System.Net
open Xunit
open FsUnit.Xunit
open BookManagement.Infrastructure.CosmosDb
open BookManagement.Infrastructure.Search
open BookManagement.Infrastructure.Abstractions
open BookManagement.Tests.Helpers.TestHelpers
open BookManagement.Tests.BookHandlerTests

[<Fact>]
let ``GET /api/books/search with no parameters returns 200 and empty list`` () =
    let searchStub = StubSearchService()
    let repoStub = StubBookRepository()
    
    use server = buildTestServer (repoStub :> IBookRepository) (searchStub :> ISearchService)
    use client = server.CreateClient()
    
    let response = client.GetAsync("/api/books/search").Result
    
    response.StatusCode |> should equal HttpStatusCode.OK
    let body = response.Content.ReadAsStringAsync().Result
    Assert.Contains("\"items\":[]", body)
    
    searchStub.SearchCallCount |> should equal 1
    let lastReq = searchStub.LastSearchRequest.Value
    lastReq.Genre |> should equal None
    lastReq.Page |> should equal 1
    lastReq.Size |> should equal 10

[<Fact>]
let ``GET /api/books/search with genre returns 200 with matching books`` () =
    let sampleBook1 = sampleBook "book-1" "Technology"
    let result = singlePagedResult sampleBook1
    let searchStub = StubSearchService(result)
    let repoStub = StubBookRepository()
    
    use server = buildTestServer (repoStub :> IBookRepository) (searchStub :> ISearchService)
    use client = server.CreateClient()
    
    let response = client.GetAsync("/api/books/search?genre=Technology&page=2&size=5").Result
    
    response.StatusCode |> should equal HttpStatusCode.OK
    let body = response.Content.ReadAsStringAsync().Result
    Assert.Contains("book-1", body)
    Assert.Contains("Clean Code", body)
    
    searchStub.SearchCallCount |> should equal 1
    let lastReq = searchStub.LastSearchRequest.Value
    lastReq.Genre |> should equal (Some "Technology")
    lastReq.Page |> should equal 2
    lastReq.Size |> should equal 5
