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
 ├── Partner.Bff.Api
 ├── Partner.Bff.Application
 ├── Partner.Bff.Domain
 └── Partner.Bff.Infrastructure

tests/
 └── Partner.Bff.UnitTests

## 🎯 Why install this?
- Used for lightweight endpoint definition and reduced boilerplate while keeping performance high.

✅ Endpoint POST /api/v1/partner/transactions
Receive JSON payload, return 202 Accepted
Validate with FluentValidation: amount > 0, currency ISO 4217, all fields required, timestamp not in the future

✅ Mock Partner Verification API (MockPartnerVerificationController)
Same project, endpoint GET /api/v1/mock/partners/{id}/verify
Random 30% throws TimeoutException, 70% returns valid response
4 mock partners available (P-1001, P-1002, P-1003 inactive, P-9999)

✅ Resilience with Polly
Per-attempt timeout: 5 seconds
Retry: 3 times, exponential backoff + jitter
Circuit Breaker: opens after 50% failure rate in 30 seconds

✅ RabbitMQ Messaging
IMessagePublisher interface + RabbitMqMessagePublisher implementation
Durable queue, persistent messages, auto-recovery

✅ Unit Tests (xUnit + Moq + FluentAssertions)
Validator tests: 15+ test cases
TransactionService tests: happy path, validation failed, partner failed, broker failed
Middleware tests: all error scenarios
PartnerVerificationService tests

✅ Bonus items
docker-compose.yml spin up API + RabbitMQ
GlobalExceptionHandlerMiddleware format error nhất quán
ApiKeyAuthenticationMiddleware bảo mật endpoint
Serilog structured logging + rolling files
Swagger UI with API Key authentication

# Tutorial
# Step1: You need to install two things.:
1. .NET 8 SDK
Go to https://dotnet.microsoft.com/download/dotnet/8.0 → download the SDK (not the Runtime) that is compatible with your OS (Windows/Mac/Linux).
After installation is complete, check:
bashdotnet --version
-> Phải hiện: 8.0.xxx

2. Docker Desktop
Go to https://www.docker.com/products/docker-desktop → download and install.
Check:
bashdocker --version
docker compose version

Bước 1 — Giải nén source code
bash# Giải nén file zip vừa download
# Windows: chuột phải → Extract All
# Mac/Linux:
unzip PartnerIntegrationBFF.zip

# Vào thư mục project
cd PartnerIntegrationBFF

# Bước 2 — Chọn cách chạy
Bạn có 2 lựa chọn:

✅ Cách A: Docker Compose (Dễ nhất — 1 lệnh)
Cách này tự động spin up cả API lẫn RabbitMQ, không cần cài thêm gì.
bash# Đứng trong thư mục PartnerIntegrationBFF
docker compose up --build
Đợi khoảng 1-2 phút lần đầu (build Docker image). Khi thấy log như này là thành công:
partner-rabbitmq      | Server startup complete
partner-integration-api | [HH:mm:ss INF] Now listening on: http://[::]:8080
Truy cập:

Swagger UI: http://localhost:5000/swagger
RabbitMQ Dashboard: http://localhost:15672 (login: guest / guest)

Để dừng:
bashCtrl+C
# Hoặc xóa hoàn toàn:
docker compose down -v

✅ Cách B: Chạy Local (Cần .NET SDK)
Bước B1 — Khởi động RabbitMQ bằng Docker:
bashdocker run -d --name rabbitmq \
  -p 5672:5672 \
  -p 15672:15672 \
  rabbitmq:3.13-management-alpine
Bước B2 — Restore packages:
bashdotnet restore
Bước B3 — Chạy API:
bashcd src/PartnerIntegration.API
dotnet run
Thấy log này là OK:
[INF] Now listening on: http://localhost:5000
Truy cập:

Swagger UI: http://localhost:5000/swagger


Bước 3 — Chạy Tests
Mở terminal mới, đứng tại thư mục gốc PartnerIntegrationBFF:
bashdotnet test
Kết quả mong đợi:
Passed! - Failed: 0, Passed: 35+, Skipped: 0
Muốn xem coverage:
bashdotnet test --collect:"XPlat Code Coverage"

Bước 4 — Test API thực tế
Mở Swagger tại http://localhost:5000/swagger
🔒 Quan trọng: Click nút Authorize (khóa 🔒) ở góc phải → nhập API key:
test-api-key-12345

Test 1: Submit transaction thành công
Dùng endpoint POST /api/v1/partner/transactions, body:
json{
  "partnerId": "P-1001",
  "transactionReference": "TXN-99823",
  "amount": 250.00,
  "currency": "USD",
  "timestamp": "2024-05-10T14:30:00Z"
}
Kết quả mong đợi 202 Accepted:
json{
  "transactionId": "xxxxxxxx-...",
  "status": "Accepted",
  "message": "Transaction has been validated and queued for processing."
}

Lưu ý: Mock API có 30% xác suất timeout, Polly sẽ tự retry. Nếu vẫn fail thì gọi lại.

Copy transactionId từ response, dùng cho Test 2.
Test 2: Query trạng thái transaction
Dùng endpoint GET /api/v1/partner/transactions/{transactionId} → paste ID vừa copy.
Kết quả 200 OK:
json{
  "transactionId": "...",
  "partnerId": "P-1001",
  "status": "Queued"
}
Test 3: Validation error
json{
  "partnerId": "",
  "transactionReference": "TXN-001",
  "amount": -5,
  "currency": "INVALID",
  "timestamp": "2024-05-10T14:30:00Z"
}
Kết quả 422 Unprocessable Entity với danh sách lỗi chi tiết.
Test 4: Partner không tồn tại
json{
  "partnerId": "P-UNKNOWN",
  "transactionReference": "TXN-001",
  "amount": 100,
  "currency": "USD",
  "timestamp": "2024-05-10T14:30:00Z"
}
Kết quả 502 Bad Gateway.

Xem message trong RabbitMQ
Vào http://localhost:15672 → login guest/guest → tab Queues → click queue partner-transactions → Get messages để xem các transaction đã được enqueue.

Lỗi thường gặp
"Connection refused" khi start API:
→ RabbitMQ chưa ready. Đợi thêm 10-15 giây rồi thử lại.
Docker Compose build fail:
→ Đảm bảo Docker Desktop đang chạy (icon Docker xuất hiện ở taskbar/menubar).
dotnet: command not found:
→ Cài lại .NET SDK và restart terminal.
Port 5000 bị chiếm:
→ Đổi port trong docker-compose.yml: "5001:8080" rồi truy cập http://localhost:5001/swagger.

4. Noted:
- Nếu dùng Docker Compose, Swagger chỉ bật ở môi trường Development. Kiểm tra biến môi trường trong docker-compose.yml:
yamlenvironment:
- ASPNETCORE_ENVIRONMENT=Development   # phải là Development mới có Swagger

Hiện tại file đang để Production nên Swagger bị tắt. Sửa lại:
bash# Mở file docker-compose.yml, tìm dòng:
- ASPNETCORE_ENVIRONMENT=Production

# Đổi thành:
- ASPNETCORE_ENVIRONMENT=Development

Rồi chạy lại:
bashdocker compose down
docker compose up --build
