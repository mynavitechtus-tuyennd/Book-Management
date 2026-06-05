namespace BookManagement.Handlers

open System
open System.Text
open System.Net
open System.IdentityModel.Tokens.Jwt
open System.Security.Claims
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.IdentityModel.Tokens
open Giraffe
open BookManagement.Domain
open BookManagement.Helpers

module AuthHttpHandler =

    /// Simple in-memory credential store for demo purposes.
    /// In production: query a user database and use password hashing (BCrypt, PBKDF2, etc.)
    let private validCredentials =
        dict [
            "admin", "Admin@123"
            "user",  "User@123"
        ]

    let private generateToken (issuer: string) (audience: string) (secretKey: string) (username: string) : TokenResult =
        let key       = SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        let creds     = SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        let expiresAt = DateTime.UtcNow.AddHours(8.0)

        let claims = [|
            Claim(JwtRegisteredClaimNames.Sub,  username)
            Claim(JwtRegisteredClaimNames.Jti,  Guid.NewGuid().ToString())
            Claim(JwtRegisteredClaimNames.Iat,  DateTimeOffset.UtcNow.ToUnixTimeSeconds() |> string, ClaimValueTypes.Integer64)
            Claim(ClaimTypes.Name, username)
        |]

        let token =
            JwtSecurityToken(
                issuer    = issuer,
                audience  = audience,
                claims    = claims,
                notBefore = DateTime.UtcNow,
                expires   = expiresAt,
                signingCredentials = creds)

        { Token     = JwtSecurityTokenHandler().WriteToken(token)
          ExpiresAt = expiresAt }

    // POST /api/auth/login
    let login (req: LoginRequest) : HttpHandler =
        fun next ctx ->
            task {
                try
                    let config    = ctx.RequestServices.GetRequiredService<IConfiguration>()
                    let issuer    = config.["Jwt:Issuer"]
                    let audience  = config.["Jwt:Audience"]
                    let secretKey = config.["Jwt:SecretKey"]

                    match CommonHelper.validate req with
                    | Error err -> return! CommonHelper.unprocessableEntity err next ctx
                    | Ok () ->
                        match validCredentials.TryGetValue(req.Username) with
                        | true, storedPassword when storedPassword = req.Password ->
                            let result = generateToken issuer audience secretKey req.Username
                            return! json result next ctx
                        | _ ->
                            return! (setStatusCode (int HttpStatusCode.Unauthorized) >=> json {| message = "Invalid username or password" |}) next ctx
                with ex ->
                    return! CommonHelper.badRequest $"Invalid request: {ex.Message}" next ctx
            }
