namespace BookManagement.App

open System
open System.Text
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.Azure.Cosmos
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Configuration
open Microsoft.IdentityModel.Tokens
open Azure
open Azure.Search.Documents
open Giraffe
open BookManagement.Infrastructure.Abstractions
open BookManagement.Infrastructure.CosmosDb
open BookManagement.Infrastructure.Search
open BookManagement.Application
open BookManagement.HttpHandler

type Startup(configuration: IConfiguration) =

    let getRequiredConfig key =
        let value = configuration.[key]
        if String.IsNullOrWhiteSpace(value) then
            failwithf "Configuration error: key '%s' is missing or empty. Please ensure it is correctly defined in appsettings.json." key
        value

    let cosmosConnStr   = getRequiredConfig "CosmosDb:ConnectionString"
    let cosmoDbName     = getRequiredConfig "CosmosDb:DatabaseName"
    let cosmosContainer = getRequiredConfig "CosmosDb:ContainerName"

    let searchEndpoint  = getRequiredConfig "AzureSearch:Endpoint"
    let searchApiKey    = getRequiredConfig "AzureSearch:ApiKey"
    let searchIndexName = getRequiredConfig "AzureSearch:IndexName"

    let jwtIssuer    = getRequiredConfig "Jwt:Issuer"
    let jwtAudience  = getRequiredConfig "Jwt:Audience"
    let jwtSecretKey = getRequiredConfig "Jwt:SecretKey"

    let errorHandler (ex: Exception) (logger: ILogger) =
        logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
        clearResponse >=> setStatusCode 500 >=> json {| message = ex.Message |}

    let configureCors (builder: CorsPolicyBuilder) =
        builder
#if DEBUG
            .AllowAnyOrigin()
#else
            .WithOrigins("http://localhost:5000", "https://localhost:5001")
#endif
            .AllowAnyMethod()
            .AllowAnyHeader()
            |> ignore

    member _.ConfigureServices(services: IServiceCollection) =
        // Cosmos DB — singleton (thread-safe, expensive to create)
        let cosmosOptions = CosmosClientOptions(
                                SerializerOptions = CosmosSerializationOptions(
                                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase))
        let cosmosClient = new CosmosClient(cosmosConnStr, cosmosOptions)
        services.AddSingleton<CosmosClient>(cosmosClient) |> ignore

        // Azure AI Search — SearchClient only (SearchIndexClient not needed at runtime;
        // index schema is managed via scripts/CreateSearchIndex.fsx)
        // Configured with camelCase naming to match field names in the index
        // (FieldBuilder reads [JsonProperty] attrs → creates lowercase fields like 'genre', 'authors')
        let searchCredential  = AzureKeyCredential(searchApiKey)
        let searchEndpointUri = Uri(searchEndpoint)
        let searchClient = new SearchClient(searchEndpointUri, searchIndexName, searchCredential)
        services.AddSingleton<SearchClient>(searchClient) |> ignore

        // Scoped services
        services.AddScoped<ISearchService>(fun sp ->
            let sc  = sp.GetRequiredService<SearchClient>()
            let log = sp.GetRequiredService<ILogger<SearchService>>()
            SearchService(sc, log) :> ISearchService) |> ignore

        services.AddScoped<IBookRepository>(fun sp ->
            let cosmos  = sp.GetRequiredService<CosmosClient>()
            let search  = sp.GetRequiredService<ISearchService>()
            let log     = sp.GetRequiredService<ILogger<BookRepository>>()
            BookRepository(cosmos, cosmoDbName, cosmosContainer, search, log) :> IBookRepository) |> ignore

        services.AddScoped<IBookService>(fun sp ->
            let repo = sp.GetRequiredService<IBookRepository>()
            BookService(repo) :> IBookService) |> ignore

        services.AddScoped<ISearchQueryService>(fun sp ->
            let search = sp.GetRequiredService<ISearchService>()
            let repo = sp.GetRequiredService<IBookRepository>()
            SearchQueryService(search, repo) :> ISearchQueryService) |> ignore

        // JWT Authentication
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(fun options ->
                options.TokenValidationParameters <-
                    TokenValidationParameters(
                        ValidateIssuer           = true,
                        ValidateAudience         = true,
                        ValidateLifetime         = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer              = jwtIssuer,
                        ValidAudience            = jwtAudience,
                        IssuerSigningKey         = SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(jwtSecretKey)))) |> ignore

        services.AddAuthorization() |> ignore
        services.AddCors()          |> ignore
        services.AddGiraffe()       |> ignore

    member _.Configure(app: IApplicationBuilder, env: IWebHostEnvironment) =
        (match env.IsDevelopment() with
        | true  -> app.UseDeveloperExceptionPage()
        | false -> app.UseGiraffeErrorHandler(errorHandler).UseHttpsRedirection())
            .UseCors(configureCors)
            .UseAuthentication()
            .UseAuthorization()
            .UseDefaultFiles()
            .UseStaticFiles()
            .UseGiraffe(HttpHandler.webApp)
