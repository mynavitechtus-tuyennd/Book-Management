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
open Azure.Search.Documents.Indexes
open Giraffe
open BookManagement.Infrastructure.CosmosDb
open BookManagement.Infrastructure.Search
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

        // Azure Search — singletons
        let searchCredential = AzureKeyCredential(searchApiKey)
        let searchEndpointUri = Uri(searchEndpoint)
        let searchClient  = new SearchClient(searchEndpointUri, searchIndexName, searchCredential)
        let indexClient   = new SearchIndexClient(searchEndpointUri, searchCredential)
        services.AddSingleton<SearchClient>(searchClient)     |> ignore
        services.AddSingleton<SearchIndexClient>(indexClient) |> ignore

        // Scoped services
        services.AddScoped<ISearchService>(fun sp ->
            let sc  = sp.GetRequiredService<SearchClient>()
            let ic  = sp.GetRequiredService<SearchIndexClient>()
            let log = sp.GetRequiredService<ILogger<SearchService>>()
            SearchService(sc, ic, searchIndexName, log) :> ISearchService) |> ignore

        services.AddScoped<IBookRepository>(fun sp ->
            let cosmos  = sp.GetRequiredService<CosmosClient>()
            let search  = sp.GetRequiredService<ISearchService>()
            let log     = sp.GetRequiredService<ILogger<BookRepository>>()
            BookRepository(cosmos, cosmoDbName, cosmosContainer, search, log) :> IBookRepository) |> ignore

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
        // Ensure Azure Search index exists on startup
        let sp = app.ApplicationServices
        let searchSvc = sp.CreateScope().ServiceProvider.GetService<ISearchService>()
        match searchSvc with
        | :? SearchService as svc ->
            try
                svc.EnsureIndexExists() |> Async.AwaitTask |> Async.RunSynchronously
            with ex ->
                let log = sp.GetRequiredService<ILogger<Startup>>()
                log.LogWarning(ex, "Could not ensure Azure Search index — check configuration")
        | _ -> ()

        (match env.IsDevelopment() with
        | true  -> app.UseDeveloperExceptionPage()
        | false -> app.UseGiraffeErrorHandler(errorHandler).UseHttpsRedirection())
            .UseCors(configureCors)
            .UseAuthentication()
            .UseAuthorization()
            .UseDefaultFiles()
            .UseStaticFiles()
            .UseGiraffe(HttpHandler.webApp)
