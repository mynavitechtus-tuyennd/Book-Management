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
    let login : HttpHandler =
        fun next ctx ->
            task {
                try
                    let! req = CommonHelper.bindValue<LoginRequest> ctx

                    match req with
                    | None ->
                        return! CommonHelper.badRequest "Username and Password are required" next ctx
                    | Some r when String.IsNullOrWhiteSpace(r.Username) || String.IsNullOrWhiteSpace(r.Password) ->
                        return! CommonHelper.badRequest "Username and Password are required" next ctx
                    | Some r ->
                        let config    = ctx.RequestServices.GetRequiredService<IConfiguration>()
                        let issuer    = config.["Jwt:Issuer"]
                        let audience  = config.["Jwt:Audience"]
                        let secretKey = config.["Jwt:SecretKey"]

                        match validCredentials.TryGetValue(r.Username) with
                        | true, storedPassword when storedPassword = r.Password ->
                            let result = generateToken issuer audience secretKey r.Username
                            return! json result next ctx
                        | _ ->
                            return! (setStatusCode (int HttpStatusCode.Unauthorized) >=> json {| message = "Invalid username or password" |}) next ctx
                with ex ->
                    return! CommonHelper.badRequest $"Invalid request: {ex.Message}" next ctx
            }
