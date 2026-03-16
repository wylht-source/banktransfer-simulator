# 🏦 Banking Transaction API

A production-grade RESTful API simulating core banking operations, built with Clean Architecture and deployed on Azure.

> **Portfolio project** — built to demonstrate backend engineering skills for fintech positions.

## 🔗 Live Demo

**Swagger UI:** Available on request — contact me for demo access.

> Note: Running on Azure App Service B1. First request may take 1-2 minutes if the container is cold-starting.

---

## 🚀 Tech Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 8, C# |
| Architecture | Clean Architecture + CQRS |
| Database | Azure SQL (Serverless) + EF Core 8 |
| Auth | ASP.NET Identity + JWT Bearer |
| Cloud | Azure App Service, Azure Container Registry, Azure Key Vault |
| Observability | Application Insights |
| CI/CD | GitHub Actions → Docker → ACR → App Service |
| Testing | xUnit, FluentAssertions, Moq, EF Core InMemory |

---

## 📐 Architecture

```
BankingApi/
├── src/
│   ├── BankingApi.Domain/          # Pure business logic — zero external dependencies
│   │   ├── Entities/               # Account, Transaction
│   │   ├── Enums/                  # TransactionType
│   │   └── Exceptions/             # DomainException
│   ├── BankingApi.Application/     # Use cases (CQRS handlers)
│   │   ├── Accounts/               # CreateAccount, GetAccount
│   │   ├── Transactions/           # Deposit, Withdraw, Transfer, GetStatement
│   │   ├── Auth/                   # Register, Login commands
│   │   ├── Common/                 # PagedResult
│   │   └── Interfaces/             # IAccountRepository, ITransactionRepository
│   ├── BankingApi.Infrastructure/  # EF Core, repositories, external concerns
│   │   ├── Persistence/            # BankingDbContext (IdentityDbContext)
│   │   └── Repositories/           # AccountRepository, TransactionRepository
│   └── BankingApi.API/             # Controllers, middleware, DI setup
│       ├── Controllers/            # Auth, Accounts, Transactions
│       └── Middleware/             # CorrelationIdMiddleware
└── tests/
    └── BankingApi.Tests/
        ├── Unit/                   # Domain entity tests
        └── Integration/            # Application handler tests (EF InMemory)
```

**Dependency rule:** all dependencies point inward. Domain has no dependencies. Application depends only on Domain. Infrastructure and API depend on Application.

---

## ✨ Features

### Core Banking
| Feature | Description |
|---------|-------------|
| Account Management | Create accounts, check balance |
| Deposits & Withdrawals | With balance validation and full audit trail |
| Transfers | Atomic transfers between accounts |
| Statement | Paginated transaction history |

### Production-Grade Concerns
| Feature | Description |
|---------|-------------|
| Idempotency | All write operations accept `Idempotency-Key` header — safe to retry |
| JWT Auth | Secure token-based authentication via ASP.NET Identity |
| Ownership enforcement | Users can only operate on their own accounts |
| Correlation ID | Every request tagged with `X-Correlation-ID` for distributed tracing |
| Key Vault | Runtime secrets managed via Azure Key Vault + Managed Identity |
| Application Insights | Request tracing, dependency tracking, performance metrics |
| CI/CD | Automated pipeline: tests → Docker build → ACR push → deploy |

---

## 🔑 Key Design Decisions

### Ledger-Lite (Append-Only Transactions)
Transactions are **immutable** — never updated, never deleted. Balance is derived and updated atomically in the same ACID transaction. This provides a natural audit trail and simplifies reasoning about financial state.

```
❌ Update balance directly
✅ Append transaction → update balance atomically
```

### Idempotency
All write operations accept an `Idempotency-Key` header (UUID). If the same key is received twice (e.g. network retry), the system returns the original response without re-processing. Critical for financial systems where duplicate processing means duplicate money movement.

```http
POST /api/transactions/deposit
Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000

{
  "accountId": "...",
  "amount": 1000.00,
  "description": "Salary"
}
```

### Managed Identity (Zero Secrets in Code)
The App Service authenticates to Azure Container Registry and Azure Key Vault using **Managed Identity** — no passwords, no rotation, no risk of credential leakage. Runtime secrets (connection string, JWT secret) live exclusively in Key Vault.

### CQRS Without a Framework
Commands (write) and Queries (read) are separated at the handler level without introducing MediatR or other frameworks. This keeps the architecture explicit and easy to understand, while still demonstrating the pattern clearly.

---

## 🛠️ Running Locally

### Prerequisites
- .NET 8 SDK
- SQL Server LocalDB (comes with Visual Studio)

### Setup

```bash
git clone https://github.com/wylht-source/banktransfer-simulator.git
cd banktransfer-simulator

# Run migrations (creates local database)
dotnet ef database update --project src/BankingApi.Infrastructure --startup-project src/BankingApi.API

# Start the API
dotnet run --project src/BankingApi.API
```

Swagger UI: `http://localhost:5111/swagger`

### Running Tests

```bash
dotnet test
```

16 tests: 10 unit (Domain layer) + 6 integration (Application handlers with EF InMemory).

---

## ☁️ Cloud Infrastructure

```
Azure Resource Group: rg-banking-api (Brazil South)
│
├── Azure SQL Database (Serverless)     ← BankingApiDb
├── Azure Container Registry            ← wybanking.azurecr.io
├── Azure App Service (B1, Linux)       ← banking-api-demo
│   └── Managed Identity
│       ├── AcrPull → Container Registry
│       └── Key Vault Secrets User → Key Vault
├── Azure Key Vault                     ← kv-banking-api
│   ├── ConnectionStrings--DefaultConnection
│   └── Jwt--Secret
└── Application Insights                ← banking-api-insights
```

### CI/CD Pipeline

Every push to `main` triggers:

```
Run tests (dotnet test)
    ↓ (only if tests pass)
Build Docker image
    ↓
Push to ACR with commit SHA tag + latest
    ↓
Deploy to App Service with SHA tag
    ↓
Restart App Service
```

Pull requests run tests only — no deployment.

---

## 📊 API Endpoints

### Authentication
| Method | Endpoint | Auth |
|--------|----------|------|
| POST | `/api/auth/register` | Public |
| POST | `/api/auth/login` | Public |

### Accounts
| Method | Endpoint | Auth |
|--------|----------|------|
| POST | `/api/accounts` | Required |
| GET | `/api/accounts/{id}` | Required + Owner |
| GET | `/api/accounts/{id}/statement` | Required + Owner |

### Transactions
| Method | Endpoint | Auth |
|--------|----------|------|
| POST | `/api/transactions/deposit` | Required + Owner |
| POST | `/api/transactions/withdraw` | Required + Owner |
| POST | `/api/transactions/transfer` | Required + Owner (source account) |

---

## 🔭 What I Would Do With More Time

- **Key Vault for all config** — move `Jwt__Issuer` and `Jwt__Audience` to Key Vault as well
- **Event Sourcing** — replace ledger-lite with full event sourcing using Azure Service Bus
- **Loan Module** — loan requests, installment generation, payment processing *(in progress)*
- **Credit Risk AI** — ML model analyzing transaction history for loan risk scoring (Python + scikit-learn)
- **Anti-fraud detection** — real-time anomaly detection on transaction patterns
- **KYC** — identity verification flow
- **Multi-currency** — exchange rates and cross-currency transfers
- **Rate limiting** — per-user throttling to prevent abuse
- **Integration tests for API layer** — full HTTP pipeline tests with WebApplicationFactory
- **Always On** — upgrade to S1 plan to eliminate cold starts

---

## 📄 License

MIT