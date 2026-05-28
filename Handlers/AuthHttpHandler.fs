namespace BookManagement.Handlers

open System
open System.Text
open System.IdentityModel.Tokens.Jwt
open System.Security.Claims
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.IdentityModel.Tokens
open Giraffe
open BookManagement.Domain

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
                    let! req = ctx.BindJsonAsync<LoginRequest>()

                    if isNull (box req) || String.IsNullOrWhiteSpace(req.Username) || String.IsNullOrWhiteSpace(req.Password) then
                        return! (setStatusCode 400 >=> json {| message = "Username and Password are required" |}) next ctx
                    else
                        let config    = ctx.RequestServices.GetRequiredService<IConfiguration>()
                        let issuer    = config.["Jwt:Issuer"]
                        let audience  = config.["Jwt:Audience"]
                        let secretKey = config.["Jwt:SecretKey"]

                        let mutable storedPassword = ""
                        if validCredentials.TryGetValue(req.Username, &storedPassword) && storedPassword = req.Password then
                            let result = generateToken issuer audience secretKey req.Username
                            return! json result next ctx
                        else
                            return! (setStatusCode 401 >=> json {| message = "Invalid username or password" |}) next ctx
                with ex ->
                    return! (setStatusCode 400 >=> json {| message = $"Invalid request: {ex.Message}" |}) next ctx
            }
