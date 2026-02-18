## Soccer Manager

A robust, scalable .NET 10 Web API for managing soccer teams and and player transfers, built with **Onion Architecture** and **Domain-Driven Design (DDD)** principles. 

### 🚀 Technologies:

* **Runtime:** .NET 10 SDK

* **Architecture:** Onion Architecture / Clean Architecture

* **Patterns:** Domain-Driven Design (DDD), Repository Pattern, Unit of Work and Optimistic Concurrency

* **Database:** SQL Server via Entity Framework Core

* **Identity:** ASP.NET Core Identity API Endpoints (Cookie and Bearer Token auth)

* **Rate Limiting:** Redis

* **Documentation:** Swagger, Scalar, OpenAPI	

### 🏗️ Architecture Overview

The project is divided into four concentric layers following Onion Architecture principles: 

* **Domain:** Core business logic, entities, value objects, and domain exceptions. No external dependencies.

* **Application:** Use cases, service interfaces, DTOs, and mapping logic.

* **Infrastructure:** Implementation of data access (SQL Server), Identity services, and external integrations.

* **Presentation (API):** Entry point, controllers, Identity API endpoint configuration, middlewares for unit of work and global exception handling, authorization etc. 

### 🔑 Authentication & Authorization

This project uses the native .NET Identity API Endpoints for a streamlined auth experience:

* **POST** /auth/register: Create a new user account.

* **POST** /auth/login: Exchange credentials for a Bearer token.

* **POST** /auth/refresh: Renew expired sessions.

* **Auth Type:** Cookie (for web browsers) & JWT / Bearer Token (for native clients). 


### 🛠️ Getting Started

### Prerequisites

* .NET 10 SDK

* SQL Server (Express or LocalDB)

* Visual Studio 2022 (v17.12+)

### Installation

1) **Clone the repository:**

```bash
git clone <repository-url>
cd your-project
```

2) **Configuration:**
Update the settings in `appsettings.json` or use [User Secrets](https://learn.microsoft.com) for local development:

*   **SQL Server Database**: Update `DefaultConnection` to point to your SQL instance.
*   **Redis**: Update the `Redis` connection string (required for distributed rate limiting).
*   **Admin Identity**: Set the default credentials for the initial administrative account.

**Example `appsettings.json` structure:**

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=YOUR_DB;User Id=YOUR_USER;Password=YOUR_PASSWORD;TrustServerCertificate=True;",
    "Redis": "localhost:6379"
  },
  "AdminUser": {
    "Email": "admin@yourdomain.com",
    "UserName": "admin",
    "Password": "Secret@123"
  },
  "RateLimitOptions": {
    "GlobalLimit": 500,
    "UserLimit": 100,
    "GuestLimit": 20
  }
}
```

3) **Run Migrations:**

```bash
dotnet ef database update --project src/EntityFrameworkCore --startup-project src/Api
```

4) **Launch the Application:**

Run the project and navigate to /swagger or /scalar to test the Api endpoints. 

### 🧪 Testing

Run the following command to execute unit and integration tests across all layers.

```bash
dotnet test
```