// User Http Handler

namespace BookManagement.UserHttpHandler

open Giraffe
open Microsoft.AspNetCore.Http

module UserHttpHandler =
    let indexHandler (name: string) : HttpHandler =
        let greetings = sprintf "Hello %s, from Giraffe!" name
        text greetings