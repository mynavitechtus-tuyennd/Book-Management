# 🧪 Book Management API Tests

This project contains the unit and integration test suite for the **Book Management API**, written in **F#** using **xUnit** and **FsUnit.xUnit**. 

All tests run using stubs and an in-memory test server, meaning **no active Azure Cosmos DB or Azure AI Search resources are required** to execute the tests.

---

## 🛠 Testing Libraries

- **[xUnit](https://xunit.net/)** (2.9.3) — Main test runner and test framework
- **[FsUnit.xUnit](https://github.com/fsprojects/FsUnit)** (6.0.1) — F#-friendly assertion library (using readable DSL syntax like `should equal`, `should be Empty`, etc.)
- **[Microsoft.AspNetCore.TestHost](https://www.nuget.org/packages/Microsoft.AspNetCore.TestHost)** (10.0.0) — Provides `TestServer` for HTTP-level integration testing
- **[NSubstitute](https://nsubstitute.github.io/)** (5.3.0) — Mocking framework (available if dynamic stubs are needed, though lightweight manual stubs are preferred in this codebase)

---

## 📁 Test Project Structure

```text
BookManagement.Tests/
├── Helpers/
│   └── TestHelpers.fs       # Sample test data generators and manual stub implementations
├── BookRepositoryTests.fs  # Unit tests for the repository interface behavior
└── BookHandlerTests.fs     # Integration tests for the HTTP Handlers and middleware pipeline
```

### 1. `Helpers/TestHelpers.fs`
Contains helper functions and stubs to decouple the test suite from external dependencies:
- **Sample Data Generators**: `sampleBook`, `sampleCreateRequest`, and `sampleUpdateRequest` for standard fixtures.
- **`StubBookRepository`**: An in-memory, thread-safe implementation of `IBookRepository` with customizable outputs and call-tracking capabilities (`CreateCallCount`).
- **`StubSearchService`**: A no-op implementation of `ISearchService` to avoid calls to Azure AI Search.

### 2. `BookRepositoryTests.fs` (Unit Tests)
Tests individual CRUD behavior defined by `IBookRepository` using the `StubBookRepository`. It verifies:
- `GetAll`: Returns proper paged results or empty lists.
- `GetById`: Returns `Some book` when found, or `None` if missing.
- `Create`: Generates unique IDs, maps values correctly, and is called exactly once.
- `Update`/`Delete`: Returns correct optional types/booleans based on input states.

### 3. `BookHandlerTests.fs` (Integration Tests)
Starts an in-memory ASP.NET Core `TestServer` loaded with the Giraffe `webApp` routing table. It validates:
- **Route Authorization**: Ensures protected routes (POST/DELETE) return `401 Unauthorized` without a valid token.
- **JWT Middleware**: Includes a local token generator (`makeTestToken`) to generate and sign mock tokens with the test secret to simulate logged-in users.
- **Request Handlers**: 
  - `GET /api/books` returns 200 with the list.
  - `POST /api/books` returns 201 on success or 400 on validation failure (e.g., missing title).
  - `GET /api/books/{id}/{genre}` returns 200 with the book or 404.
  - `DELETE /api/books/{id}/{genre}` returns 204 or 404.

---

## 🚀 Running the Tests

To run the test suite, run the following command from the repository root:

```bash
dotnet test
```

Or from the test directory:

```bash
cd BookManagement.Tests
dotnet test
```

### Expected Output

All tests should pass successfully:

```text
Passed!  - Failed:     0, Passed:    17, Skipped:     0, Total:    17, Duration: ...
```
