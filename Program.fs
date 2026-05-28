namespace BookManagement.Program

open System
open System.IO
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open BookManagement.App

module Program =

    let CreateHostBuilder args =
        let contentRoot = Directory.GetCurrentDirectory()
        let webRoot     = Path.Combine(contentRoot, "WebRoot")
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(fun webHostBuilder ->
                webHostBuilder
                    .UseContentRoot(contentRoot)
                    .UseWebRoot(webRoot)
                    .UseStartup<Startup>() |> ignore
                )

    [<EntryPoint>]
    let main args =
        try
            CreateHostBuilder(args).Build().Run()
            0
        with ex ->
            printfn "An error occurred: %s" ex.Message
            1