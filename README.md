# CommerceHub Microservice

A production-ready .NET 8 Commerce Hub API with MongoDB for persistence, RabbitMQ for event-driven messaging, and comprehensive nUnit test coverage.

---

## Architecture

```
┌─────────────────────────────────────────────────────┐
│                    API Layer                         │
│  OrdersController          ProductsController       │
│  POST /checkout  GET/{id}  PATCH /{id}/stock        │
│  PUT /{id}                                          │
├─────────────────────────────────────────────────────┤
│                 Service Layer                       │
│  OrderService (checkout + rollback)                 │
│  ProductService (stock adjustment)                  │
├─────────────────────────────────────────────────────┤
│               Repository Layer                      │
│  ProductRepository (atomic FindOneAndUpdate)        │
│  OrderRepository (CRUD)                             │
├────────────────────┬────────────────────────────────┤
│     MongoDB        │       RabbitMQ                 │
│  products / orders │  OrderCreated events           │
└────────────────────┴────────────────────────────────┘
```

### Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| **Simplified layered architecture** | 2-day assignment scope; clean folder separation without multi-project overhead |
| **Atomic `FindOneAndUpdate`** | MongoDB conditional update prevents race conditions on stock at the database level |
| **Sequential decrement + compensating rollback** | Multi-item checkout: each item decremented atomically; partial failure triggers best-effort rollback (see Trade-offs) |
| **`IEventPublisher` abstraction** | Enables full unit testing of event emission without a running RabbitMQ broker |
| **FluentValidation** | Declarative, testable validation rules separated from controller logic |
| **`ServiceResult<T>`** | Typed result wrapper avoids throwing exceptions for business logic errors |

### Concurrency & Atomicity

**Stock Safety**:
- `PATCH /products/{id}/stock`: Single `FindOneAndUpdate` with `{stock: {$gte: abs(adjustment)}}` guard — if stock is insufficient, the update is rejected atomically
- `POST /orders/checkout`: Sequential atomic decrements per item. If item N fails, items 1..N-1 are rolled back via compensating `$inc` increments

> **Important**: The rollback is a _best-effort compensating rollback_, not equivalent to a database transaction. If the application crashes between a successful stock decrement and order creation, stock may be "lost." A production system would use the Outbox Pattern or MongoDB multi-document transactions. This trade-off is acceptable for assignment scope.

---

## Prerequisites

- [Docker](https://docs.docker.com/get-docker/) & [Docker Compose](https://docs.docker.com/compose/install/)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (for local development & running tests)

## Quick Start

```bash
# Clone and start all services
docker-compose up --build
```

Once running, access:

| Service | URL |
|---------|-----|
| **Swagger UI** | [http://localhost:5100/swagger](http://localhost:5100/swagger) |
| **RabbitMQ Management** | [http://localhost:15672](http://localhost:15672) (guest / guest) |
| **MongoDB** | `localhost:27017` |

> **Note**: The API listens on port 5000 inside the container, mapped to **port 5100** on the host to avoid conflicts with macOS AirPlay Receiver.

## Seed Data

Before testing, insert sample products into MongoDB:

```bash
docker exec -it kibo_mini_project-mongodb-1 mongosh commercehub --eval '
db.products.insertMany([
  { name: "Wireless Keyboard", sku: "KB-001", stock: 100, price: 49.99, updatedAt: new Date() },
  { name: "USB-C Hub", sku: "HUB-002", stock: 50, price: 29.99, updatedAt: new Date() },
  { name: "Monitor Stand", sku: "MS-003", stock: 25, price: 79.99, updatedAt: new Date() }
])'
```

Note the returned `_id` values for use in the API calls below.

---

## API Endpoints

### POST /api/orders/checkout

```bash
curl -X POST http://localhost:5100/api/orders/checkout \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "cust-001",
    "items": [
      { "productId": "<PRODUCT_ID>", "quantity": 2 }
    ]
  }'
```

**Responses**: `201 Created` | `400 Bad Request` | `404 Not Found` | `409 Conflict`

<details>
<summary>Sample 201 Response</summary>

```json
{
  "id": "699d1b4a50008d49a304f4bd",
  "customerId": "cust-001",
  "items": [
    {
      "productId": "699d1b4193b559119b591667",
      "quantity": 2,
      "unitPrice": 49.99
    }
  ],
  "status": "Pending",
  "totalAmount": 99.98,
  "createdAt": "2026-02-24T03:30:18.154Z",
  "updatedAt": "2026-02-24T03:30:18.154Z"
}
```

</details>

### GET /api/orders/{id}

```bash
curl http://localhost:5100/api/orders/<ORDER_ID>
```

**Responses**: `200 OK` | `404 Not Found`

### PUT /api/orders/{id}

Idempotent full replacement of the order. Blocked if current status is `Shipped`.

```bash
curl -X PUT http://localhost:5100/api/orders/<ORDER_ID> \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "cust-001",
    "items": [
      { "productId": "<PRODUCT_ID>", "quantity": 3, "unitPrice": 49.99 }
    ],
    "status": "Paid",
    "totalAmount": 149.97
  }'
```

**Responses**: `200 OK` | `400 Bad Request` | `404 Not Found` | `409 Conflict (if Shipped)`

### PATCH /api/products/{id}/stock

Atomic inventory adjustment. Prevents negative stock levels.

```bash
# Add stock
curl -X PATCH http://localhost:5100/api/products/<PRODUCT_ID>/stock \
  -H "Content-Type: application/json" \
  -d '{ "adjustment": 10 }'

# Remove stock
curl -X PATCH http://localhost:5100/api/products/<PRODUCT_ID>/stock \
  -H "Content-Type: application/json" \
  -d '{ "adjustment": -5 }'
```

**Responses**: `200 OK` | `400 Bad Request` | `404 Not Found` | `409 Conflict`

<details>
<summary>Sample 200 Response (stock adjustment)</summary>

```json
{
  "productId": "699d1b4193b559119b591668",
  "name": "USB-C Hub",
  "stock": 55,
  "updatedAt": "2026-02-24T03:31:06.646Z"
}
```

</details>

---

## Running Tests

```bash
# From the solution root
dotnet test --verbosity normal
```

> **Note**: Tests use Moq to mock all infrastructure dependencies (`IProductRepository`, `IOrderRepository`, `IEventPublisher`). No running MongoDB or RabbitMQ instance is required to execute the test suite.

### Test Coverage (21 tests)

| Area | Tests | What They Verify |
|------|-------|------------------|
| **Validation** | 10 | Empty items, negative/zero quantities, missing product IDs, zero adjustment, invalid status |
| **Checkout** | 5 | Happy path with correct total, insufficient stock + rollback, product not found |
| **Event publishing** | 2 | Event published on successful checkout; event NOT published on failure |
| **Stock adjustment** | 5 | Positive increment, negative decrement, would-go-negative rejected, product not found |
| **Order update** | 4 | Happy path, blocked when shipped, order not found, invalid status |

---

## Project Structure

```
CommerceHub/
├── src/CommerceHub.Api/
│   ├── Controllers/      # API endpoints
│   ├── Services/          # Business logic
│   ├── Repositories/      # MongoDB data access
│   ├── Models/            # Domain entities
│   ├── DTOs/              # Request/response objects
│   ├── Validators/        # FluentValidation rules
│   ├── Messaging/         # RabbitMQ abstraction
│   ├── Configuration/     # Settings POCOs
│   ├── Middleware/         # Global error handling
│   └── Program.cs         # DI + pipeline setup
├── tests/CommerceHub.Tests/
│   ├── Services/          # OrderService, ProductService tests
│   └── Validators/        # Validation rule tests
├── docker-compose.yml
├── Dockerfile
├── README.md
└── developer-log.md
```

## Configuration

All settings are overridable via environment variables using the `__` (double underscore) ASP.NET convention:

| Setting | Default | Env Override |
|---------|---------|--------------|
| MongoDB Connection | `mongodb://localhost:27017` | `MongoDb__ConnectionString` |
| MongoDB Database | `commercehub` | `MongoDb__DatabaseName` |
| RabbitMQ Host | `localhost` | `RabbitMq__HostName` |
| RabbitMQ Port | `5672` | `RabbitMq__Port` |
| RabbitMQ User | `guest` | `RabbitMq__UserName` |
| RabbitMQ Password | `guest` | `RabbitMq__Password` |
| Exchange Name | `commerce-hub-events` | `RabbitMq__ExchangeName` |

---

## Assumptions & Trade-offs

1. **No authentication/authorization** — out of scope for this assignment
2. **Compensating rollback, not Outbox Pattern** — best-effort rollback is simpler and suitable for demonstration; a production system would use the Outbox Pattern or MongoDB multi-document transactions for full ACID guarantees
3. **Product prices stored on the product** — checkout reads price at time of order creation (no cart concept)
4. **Order items don't reference product names** — keeps the domain lean; join at read time if needed
5. **RabbitMQ.Client 6.x (synchronous API)** — async API available in v7 but 6.x is more stable and widely deployed
6. **Error responses are sanitized** — internal exception details are logged server-side only; clients receive domain-specific error messages or generic 500 responses
