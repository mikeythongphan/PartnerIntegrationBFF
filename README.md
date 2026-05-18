# 🌟 Overview

- Backend-for-Frontend (BFF) microservice in .NET 8
- This solution was built as a technical assessment for a Partner Integration Backend service.

* The service provides:
  - Transaction intake endpoint
  - Payload validation
  - Partner verification via external API
  - Resilience handling for transient failures
  - Asynchronous message publishing
  - Containerized local execution
  - Unit testing support
  
1. Architect: Clean Architecture (4 layers)
  Domain → Application → Infrastructure → API

- The project is organized into clear layers to improve maintainability, separation of concerns, and testability.
- I separated the solution into API, Application, Domain, and Infrastructure layers to enforce separation of concerns.
- This keeps business logic independent from transport and infrastructure concerns, improves testability, and allows implementation details like RabbitMQ or HTTP clients to be swapped without affecting core logic.

src/
* #️⃣ - PartnerIntegration.API -> Contains specific settings for the interfaces defined in Application
* #️⃣ - PartnerIntegration.Application -> Contains the specific business logic of the application (Use Cases).
* #️⃣ - PartnerIntegration.Domain -> Contains Controllers, DTOs, and configurations related to the web framework.
* #️⃣ - PartnerIntegration.Infrastructure -> Contains custom Entities, Value Objects, Domain Events, and Exceptions.

tests/
* #️⃣ - PartnerIntegration.Tests -> Contains Test Functions.

## 🎯 Why install this?
- Used for lightweight endpoint definition and reduced boilerplate while keeping performance high.

✅ Endpoint: POST /api/v1/partner/transactions
* 🔗 - Receive JSON payload, return 202 Accepted
* 🔗 - Validate with FluentValidation: amount > 0, currency ISO 4217, all fields required, timestamp not in the future

✅ Mock Partner Verification API (MockPartnerVerificationController)
* 🔗 - Same project, endpoint GET /api/v1/mock/partners/{id}/verify
* 🔗 - Random 30% throws TimeoutException, 70% returns valid response
* 🔗 - 4 mock partners available (P-1001, P-1002, P-1003 inactive, P-9999)

✅ Resilience with Polly
* 🔗 - Per-attempt timeout: 5 seconds
* 🔗 - Retry: 3 times, exponential backoff + jitter
* 🔗 - Circuit Breaker: opens after 50% failure rate in 30 seconds

✅ RabbitMQ Messaging
* 🔗 - IMessagePublisher interface + RabbitMqMessagePublisher implementation
* 🔗 - Durable queue, persistent messages, auto-recovery

✅ Unit Tests: (xUnit + Moq + FluentAssertions)
* 🔗 - Validator tests: 15+ test cases
* 🔗 - TransactionService tests: happy path, validation failed, partner failed, broker failed
* 🔗 - Middleware tests: all error scenarios
* 🔗 - PartnerVerificationService tests

✅ Bonus items: docker-compose.yml spin up API + RabbitMQ
* 🔗 - GlobalExceptionHandlerMiddleware format error nhất quán
* 🔗 - ApiKeyAuthenticationMiddleware bảo mật endpoint
* 🔗 - Serilog structured logging + rolling files
* 🔗 - Swagger UI with API Key authentication

# ✨ Tutorial
### Step 1: You need to install two things:
1. .NET 8 SDK
Go to https://dotnet.microsoft.com/download/dotnet/8.0 → download the SDK (not the Runtime) that is compatible with your OS (Windows/Mac/Linux).
After installation is complete, check:
dotnet --version
-> Phải hiện: 8.0.xxx

2. Docker Desktop
Go to https://www.docker.com/products/docker-desktop → download and install.
Check:
docker --version
docker compose version

## Step 2 — Unzip the source code.

### Unzip the downloaded zip file.
Windows: Right-click → Extract All
Mac/Linux: unzip PartnerIntegrationBFF.zip

### Go to the project folder.
cd PartnerIntegrationBFF

## Step 3 — Choose how to run
### You have two options:

✅ Method A: Docker Compose (Easiest — 1 command)
* This method automatically spins up both the API and RabbitMQ, no additional installation required.
* Located in the PartnerIntegrationBFF folder.
docker compose up --build

* Wait about 1-2 minutes the first time (building the Docker image). When you see logs like this, it's successful:

partner-rabbitmq  | Server startup complete
partner-integration-api | [HH:mm:ss INF] Now listening on: http://[::]:8080

* Access:
Swagger UI: http://localhost:5000/swagger
RabbitMQ Dashboard: http://localhost:15672 (login: guest / guest)

* To stop:
Ctrl+C

* Or delete it completely:
docker compose down -v

✅ Method B: Run Local (Requires .NET SDK)
* Step B1 — Start RabbitMQ using Docker:
docker run -d --name rabbitmq \
  -p 5672:5672 \
  -p 15672:15672 \
  rabbitmq:3.13-management-alpine

* Step B2 — Restore packages:
dotnet restore

* Step B3 — Chạy API:
cd src/PartnerIntegration.API
dotnet run

* This log looks OK:
[INF] Now listening on: http://localhost:5000

* Access:
Swagger UI: http://localhost:5000/swagger

## Step 4 — Run Tests
* Open a new terminal and navigate to the root directory PartnerIntegrationBFF:
dotnet test

* Expected results:
Passed! - Failed: 0, Passed: 35+, Skipped: 0

* To view coverage:
dotnet test --collect:"XPlat Code Coverage"

## Step 5 — Test API reality
* Open Swagger in http://localhost:5000/swagger
🔒 Important: Click the Authorize button (key 🔒) in the upper right corner → enter the API key:

```bash
test-api-key-12345
```

* 🛠️ - Test 1: Submit transaction success
* Use endpoint POST /api/v1/partner/transactions, body:
json{
  "partnerId": "P-1001",
  "transactionReference": "TXN-99823",
  "amount": 250.00,
  "currency": "USD",
  "timestamp": "2024-05-10T14:30:00Z"
}

→ Expected result 202 Accepted:
json{
  "transactionId": "xxxxxxxx-...",
  "status": "Accepted",
  "message": "Transaction has been validated and queued for processing."
}

### Note: Mock APIs have a 30% chance of timeout; Polly will automatically retry. If it still fails, it will call again.
* Copy the transactionId from the response and use it for Test 2.

* 🛠️ - Test 2: Query transaction status
* Use endpoint GET /api/v1/partner/transactions/{transactionId} → paste the copied ID.
→ The result is 200 OK:
json{
  "transactionId": "...",
  "partnerId": "P-1001",
  "status": "Queued"
}

* 🛠️ - Test 3: Validation error
json{
  "partnerId": "",
  "transactionReference": "TXN-001",
  "amount": -5,
  "currency": "INVALID",
  "timestamp": "2024-05-10T14:30:00Z"
}
→ The result is 422 Unprocessable Entity with a detailed list of errors.

* 🛠️ - Test 4: Partner does not exist.
json{
  "partnerId": "P-UNKNOWN",
  "transactionReference": "TXN-001",
  "amount": 100,
  "currency": "USD",
  "timestamp": "2024-05-10T14:30:00Z"
}
→ The result is a 502 Bad Gateway.

* View messages in RabbitMQ:
→ Go to http://localhost:15672 → log in guest/guest → Queues tab → click queue partner-transactions → Get messages to view the transactions that have been enqueued.

* Common Errors:
"Connection refused" when starting API:
→ RabbitMQ is not ready. Wait 10-15 seconds and try again.

* Docker Compose build fail:
→ Ensure Docker Desktop is running (Docker icon appears in taskbar/menubar).

dotnet: command not found:
→ Reinstall .NET SDK and restart terminal.

* Port 5000 is occupied:
→ Change the port in docker-compose.yml to "5001:8080" and then access http://localhost:5001/swagger.

# ⚠️ Noted
### If using Docker Compose, Swagger is only enabled in the Development environment. Check the environment variables in docker-compose.yml:

```bash
environment:
  - ASPNETCORE_ENVIRONMENT=Development   # Only the Development team has Swagger.
```

### Currently, the file is in Production mode, so Swagger is disabled. Please fix it:
  
* Open the docker-compose.yml file and find the line:
```bash
  - ASPNETCORE_ENVIRONMENT=Production
```

* Change to:
```bash
  - ASPNETCORE_ENVIRONMENT=Development
```
  
* Then run back:

docker compose down

docker compose up --build
