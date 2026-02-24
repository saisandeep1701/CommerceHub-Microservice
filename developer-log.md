# Developer Log — CommerceHub Microservice

## AI Strategy

### Context and Constraints I Gave the AI

I treated the AI assistant as an implementation partner, but I constrained it with explicit technical and architectural requirements so the output stayed interview-quality and testable.

I provided:

- **Exact API contract requirements** (POST checkout, GET order by id, PUT full replacement, PATCH stock adjustment)
- **Business rules**:
  - stock must not go negative
  - order updates blocked when status is `Shipped`
  - event published only after successful order creation
- **Concurrency requirement**: stock updates must be safe under concurrent requests using **MongoDB atomic conditional updates**
- **Implementation constraints**:
  - async/await throughout
  - layered design (controller → service → repository)
  - RabbitMQ behind an interface for testability
  - DTOs and validation separated from persistence models
- **Testing requirements**:
  - nUnit + mocks
  - mandatory coverage for validation, stock decrement logic, and event emission verification
  - explicit failure-path tests (e.g., event not published on checkout failure)
- **Operational requirements**:
  - Docker Compose for API + MongoDB + RabbitMQ
  - one-step startup (`docker-compose up`)

### How I Used AI (Phased Workflow)

To avoid low-quality bulk generation, I used the AI in **phases** and reviewed each phase before moving forward:

1. **Scaffolding + models + DTOs + configuration**
2. **MongoDB repositories with atomic stock operations**
3. **Services + controllers + validation**
4. **RabbitMQ publisher + event emission wiring**
5. **nUnit tests (happy path + failure paths)**
6. **Docker, README, and final documentation**

This phased approach improved quality because I could catch design and correctness issues early (especially around atomic stock updates and event ordering) before they propagated into tests and docs.

---

## Human Audit (Where I Corrected the AI)

### Correction 1: Unsafe Stock Update Pattern (Read-Then-Write) → Atomic Conditional Update

**What I found**  
The AI initially proposed a stock-check flow that read the product first, validated stock in application code, and then performed a separate update. That pattern is vulnerable to a race condition (TOCTOU): two concurrent requests can both read the same stock value and both proceed.

**What I changed**  
I replaced the logic with a single atomic MongoDB update using a **conditional filter** and `FindOneAndUpdateAsync` (or equivalent conditional update pattern), where the filter included both:
- matching product id, and
- `Stock >= requestedQuantity` (for decrement operations)

If no document was matched/updated, the service treats it as **insufficient stock** (or product not found, depending on repository result details).

**Why it matters**  
This change is critical for correctness under concurrency. It prevents stock from going negative and aligns with the assignment requirement to use MongoDB atomic operations safely.

---

### Correction 2: Event Emission Timing (Published Before Persistence) → Publish Only After Successful Order Creation

**What I found**  
The AI’s first version emitted the `OrderCreated` event too early in the checkout flow (before the order persistence step was guaranteed to succeed). That can cause data inconsistency: downstream systems receive an event for an order that may not actually exist if persistence fails.

**What I changed**  
I moved event publication to occur **only after**:
1. stock decrements succeeded, and
2. the order record was successfully created in MongoDB.

I also verified the failure path in tests so event publication is **not called** on checkout failure.

**Why it matters**  
This improves reliability and consistency between the database and message broker. It also directly satisfies the assignment’s “Messaging & Reliability” expectations.

---

### Correction 3: PUT Endpoint Semantics Were Partial Update-Like → Enforced Idempotent Full Replacement + Shipped Guard

**What I found**  
The AI initially implemented `PUT /api/orders/{id}` more like a partial update (patch-style field mutation), which does not match the assignment requirement for an **idempotent replacement**. It also did not consistently enforce the rule that shipped orders cannot be modified.

**What I changed**  
I refactored the update flow so `PUT` behaves as a full replacement of the mutable order details (while preserving identity and system-managed fields appropriately). I also added an explicit guard in the service layer to block updates when the current order status is `Shipped`, returning a conflict-style response.

**Why it matters**  
This improves API correctness (HTTP semantics), prevents invalid business state changes, and demonstrates stronger architectural control over AI-generated code rather than accepting “almost correct” behavior.

---

### Correction 4: Error Responses Exposed Internal Exception Messages → Sanitized Responses

**What I found**  
One AI-generated error handling path returned raw exception messages directly to API clients. In development this can be convenient, but in a production-style submission it risks leaking internal details (driver errors, broker connection failures, stack traces, etc.).

**What I changed**  
I updated error handling to return sanitized client-facing error messages and use logging for internal exception details. Where applicable, I returned domain/business errors explicitly (e.g., insufficient stock, order not found) and reserved generic messages for unexpected failures.

**Why it matters**  
This improves security and operational hygiene while keeping responses predictable and user-friendly.

---

## Verification

### How AI Helped with Test Generation

I used the AI to generate an initial nUnit test suite structure and then reviewed/refined it for correctness and coverage. The AI was useful for:
- quickly scaffolding tests with mocks/fakes
- generating happy-path and failure-path scenarios
- producing repetitive Arrange-Act-Assert patterns consistently

### Tests I Prioritized (Mandatory + Reliability-Focused)

I ensured coverage for the mandatory areas from the assignment:

- **Validation logic**
  - invalid quantities (negative / zero)
  - empty order items
  - missing identifiers
- **Checkout stock decrement behavior**
  - successful decrement when stock is sufficient
  - failure when stock is insufficient
- **Event emission verification**
  - event published on successful checkout
  - event not published when checkout fails

I also added or refined edge-case tests for:
- product not found during checkout
- `PUT` blocked when order status is `Shipped`
- stock patch rejects updates that would result in negative stock

### How I Validated AI-Generated Tests

1. **Manual review of assertions**  
   I reviewed each AI-generated test to ensure it was checking behavior, not just implementation details.

2. **Mock verification for messaging**  
   I explicitly verified that the event publisher is called exactly once on successful checkout and not called on failure paths.

3. **Failure-path coverage review**  
   I checked that the tests covered business-rule failures (insufficient stock, shipped order update blocked) and not only happy paths.

4. **Ran the full test suite locally**  
   I ran the tests with `dotnet test` and fixed issues in any failing tests before finalizing the suite.

> Note: I treated AI-generated tests as a starting point. I manually audited them for false positives and missing assertions before considering the suite complete.

---

## Trade-offs and Design Notes

### Atomicity Strategy for Stock Updates

For direct stock adjustment (`PATCH /api/products/{id}/stock`), I used an atomic conditional MongoDB update to prevent negative stock values under concurrency.

For checkout, I used atomic decrements per product item and documented the trade-off:
- **Pros**: simple, assignment-friendly, concurrency-safe per item
- **Trade-off**: multi-item checkout across multiple products is not a fully ACID multi-document transaction unless MongoDB transactions are used (replica set required)

Given the 2-day assignment scope, I prioritized a clear and safe implementation with explicit documentation of the trade-off.

### Why I Used a Messaging Abstraction

I placed RabbitMQ publishing behind an interface (e.g., `IEventPublisher`) so:
- checkout service logic stays testable
- unit tests can verify publish behavior without requiring a running broker
- infrastructure concerns stay isolated from business logic

---

## Timeline

| Phase | Approx. Time | Work Completed |
|---|---:|---|
| Phase 1 | ~30 min | Solution structure, models, DTOs, configuration, validation scaffolding |
| Phase 2 | ~30–45 min | MongoDB repositories and atomic stock update methods |
| Phase 3 | ~45 min | Services, controllers, business rules, DI wiring |
| Phase 4 | ~20 min | RabbitMQ publisher abstraction + implementation |
| Phase 5 | ~45–60 min | nUnit tests with mocks, plus failure-path verification |
| Phase 6 | ~30 min | Dockerfile, docker-compose, README, final developer log |

---

## Commands Used for Local Verification

- `dotnet restore`
- `dotnet build`
- `dotnet test`
- `docker-compose up --build`

---

## Final Reflection

The AI significantly accelerated boilerplate generation (project scaffolding, DTOs, test templates, wiring), but the most important part of this assignment was **human review and correction**, especially in:
- concurrency safety,
- event ordering,
- API semantics, and
- failure-path testing.

I used the AI as a productivity multiplier, not as an autopilot, and validated the final implementation against the assignment’s correctness and reliability requirements.