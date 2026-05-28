# 📚 Book Management API

A pet project — a RESTful Web API for managing a book library, built with **F#** and the **Giraffe** framework on top of ASP.NET Core. Data is stored in **Azure Cosmos DB** and full-text search is powered by **Azure AI Search**, with API routes secured using **JWT Bearer authentication**.

---

## ✨ Features

- 📖 **CRUD** — Create, Read, Update, Delete books via a clean REST API
- 🔍 **Full-text Search** — Search books by keyword with optional genre filtering and pagination, powered by Azure AI Search
- 🔐 **JWT Authentication** — Mutation routes (POST / PUT / DELETE) are protected; read routes are public
- ☁️ **Azure Native** — Cosmos DB as the primary datastore with real-time index sync to Azure AI Search
- 🧪 **Unit Tested** — In-memory test server using `xUnit` + `FsUnit` with stub repositories; no cloud dependency required

---

## 🛠 Tech Stack

| Layer | Technology |
|---|---|
| Language | F# 10.0 |
| Web Framework | [Giraffe](https://github.com/giraffe-fsharp/Giraffe) 6.4 on ASP.NET Core |
| Database | Azure Cosmos DB 3.47 |
| Search Engine | Azure AI Search (`Azure.Search.Documents` 11.6) |
| Authentication | JWT Bearer (`Microsoft.AspNetCore.Authentication.JwtBearer` 10.0) |
| Unit Testing | xUnit + FsUnit + Microsoft.AspNetCore.TestHost |

---

## 📁 Project Structure

```text
BookManagement/                         # Main API project
├── BookManagement.fsproj
├── appsettings.json                    # Configuration (CosmosDB, AzureSearch, JWT)
├── Program.fs                          # Entry point — sets up the WebHost
├── Startup.fs                          # DI registration + ASP.NET Core middleware pipeline
├── HttpHandler.fs                      # Central route registry (Giraffe router)
├── Models.fs                           # Domain models & DTOs
│
├── Configuration/
│   └── AppSettings.fs
│
├── Handlers/                           # HTTP request handlers (Controllers)
│   ├── AuthHttpHandler.fs              # POST /api/auth/login → issues JWT
│   ├── BookHttpHandler.fs              # CRUD endpoints for /api/books
│   └── SearchHttpHandler.fs           # GET /api/books/search
│
└── Infrastructure/                     # Data access & external integrations
    ├── CosmosDb/
    │   ├── IBookRepository.fs
    │   └── BookRepository.fs           # CRUD against Cosmos DB; also syncs to Search
    └── Search/
        ├── ISearchService.fs
        └── SearchService.fs            # Full-text search via Azure AI Search

BookManagement.Tests/                   # xUnit test project
├── Helpers/
│   └── TestHelpers.fs                  # Stub repositories & shared fixtures
├── BookRepositoryTests.fs
└── BookHandlerTests.fs                 # HTTP handler tests using in-memory TestServer
```

---

## 🌐 API Endpoints

### Authentication

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `POST` | `/api/auth/login` | Public | Login and receive a JWT token |

**Request body:**
```json
{
  "username": "admin",
  "password": "Admin@123"
}
```

**Response:**
```json
{
  "token": "<jwt_token>",
  "expiresAt": "2026-05-29T04:00:00Z"
}
```

> **Demo credentials:** `admin` / `Admin@123` · `user` / `User@123`

---

### Books

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `GET` | `/api/books` | Public | Get all books (paged) |
| `GET` | `/api/books/{id}/{genre}` | Public | Get a book by ID and genre |
| `GET` | `/api/books/search?query=&genre=&page=&size=` | Public | Full-text search |
| `POST` | `/api/books` | 🔐 JWT | Create a new book |
| `PUT` | `/api/books/{id}/{genre}` | 🔐 JWT | Update an existing book |
| `DELETE` | `/api/books/{id}/{genre}` | 🔐 JWT | Delete a book |

**Example — Create a book:**
```bash
# 1. Login to get token
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"username":"admin","password":"Admin@123"}' | jq -r '.token')

# 2. Create book with token
curl -X POST http://localhost:5000/api/books \
  -H 'Content-Type: application/json' \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "title": "Domain-Driven Design",
    "author": "Eric Evans",
    "isbn": "978-0321125217",
    "publisher": "Addison-Wesley",
    "publishedYear": 2003,
    "genre": "Technology",
    "description": "The blue book.",
    "price": 45.99,
    "stock": 10
  }'
```

---

## 🚀 Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- An **Azure Cosmos DB** account (or use the [Cosmos DB Emulator](https://learn.microsoft.com/en-us/azure/cosmos-db/local-emulator))
- An **Azure AI Search** service

### Configuration

Fill in `appsettings.json` with your own resource details:

```json
{
  "CosmosDb": {
    "ConnectionString": "<your-cosmos-connection-string>",
    "DatabaseName": "BookManagement",
    "ContainerName": "books"
  },
  "AzureSearch": {
    "Endpoint": "https://<your-search-service>.search.windows.net",
    "ApiKey": "<your-search-api-key>",
    "IndexName": "books-index"
  },
  "Jwt": {
    "Issuer": "BookManagementApi",
    "Audience": "BookManagementClient",
    "SecretKey": "<your-secret-key-min-32-chars>"
  }
}
```

> **Cosmos DB Emulator on macOS:** Add `DisableServerCertificateValidation=True` to the connection string to bypass the self-signed certificate error.

### Run

```bash
cd BookManagement
dotnet run
# API is available at http://localhost:5000
```

### Run Tests

```bash
dotnet test BookManagement.Tests/BookManagement.Tests.fsproj
# Expected: 17 passed, 0 failed
```

---

## 🔄 Execution Flow

### Login Flow
```
Client
  │── POST /api/auth/login ──────────────────────────────────────▶ AuthHttpHandler
  │                                                                       │
  │                                                     Validate credentials
  │                                                                       │
  │◀── 200 OK { token, expiresAt } ──────── Generate HS256 JWT Token ────┘
```

### Create Book Flow (with Search sync)
```
Client
  │── POST /api/books ──▶ JWT Middleware ──▶ requiresAuth ──▶ BookHttpHandler
  │                             │                                    │
  │                       Validate Token                    Parse JSON body
  │                             │                                    │
  │                         401 if invalid           BookRepository.CreateAsync
  │                                                         │          │
  │                                                   Cosmos DB    Azure AI Search
  │                                                   (Write)       (Index sync)
  │◀── 201 Created { book } ───────────────────────────────────────────┘
```

### Search Flow
```
Client
  │── GET /api/books/search?query=ddd&genre=Technology
  │                                      │
  │                             SearchHttpHandler
  │                                      │
  │                          SearchService.SearchAsync
  │                                      │
  │                              Azure AI Search
  │                          (filter + full-text query)
  │◀── 200 OK { items, totalCount, page, size } ──────────────────────┘
```

---

## 🧪 Testing Strategy

Tests run using an **in-memory TestServer** — no cloud services required:

- `StubBookRepository` — replaces Cosmos DB with a thread-safe in-memory store
- `StubSearchService` — replaces Azure AI Search with a no-op stub
- `authorizedClient` — generates a valid HS256 JWT token and attaches it to the `HttpClient` for testing protected routes

```
dotnet test
→ BookRepositoryTests   (unit — stub data layer)
→ BookHandlerTests      (integration — full HTTP request/response cycle)
```

---

## 📄 License

This is a personal pet project for learning purposes. Feel free to use it as a reference.
