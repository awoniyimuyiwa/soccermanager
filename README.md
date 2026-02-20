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

* **SQL Server Database:** Update `DefaultConnection` to point to your SQL instance.

* **Redis:** Update the `Redis` connection string (required for Data Protection key persistence and distributed rate limiting).

* **Data Protection:** Provide a Base64-encoded PFX certificate to encrypt keys at rest.
 You can find the PowerShell script for generating local test certificates in the scripts/ folder of this repository. To execute the generator, run these commands in your PowerShell terminal:

```powershell
#  By default, Windows restricts running scripts. Allow script execution for the current session
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope Process 

# Required: Provide a password (uses default name 'DataProtectionCert')
.\GenerateDataProtectionCert.ps1 -Password "StrongPassword!"

# Optional: Customize the name and duration
.\GenerateDataProtectionCert.ps1 -Password "StrongPassword!" -CertName "ProdCert" -Days 365
```

* **Admin Identity**: Set the default credentials for the initial administrative account.

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
    "Password": "<Password>"
  },
  "RateLimitOptions": {
    "GlobalLimit": 500,
    "UserLimit": 100,
    "GuestLimit": 20
  },
  "DataProtectionOptions": {
    "ApplicationName": "SoccerManager",
    "CertOptions": [
    {
      "Base64": "<Base64>",
      "Password": "<Password>"
    }
  ],
  "_Note": "StorageFlag: Use EphemeralKeySet for Linux/Docker/Azure, MachineKeySet for Windows IIS."
  "StorageFlag": "EphemeralKeySet"
  }
}
```

**🔄 Zero-Downtime Certificate Rotation**

To rotate certificates in a distributed environment without invalidating user sessions, follow this sequence:
Add Secondary: Add the new certificate as the second item in the Certificates array. Deploy to all nodes.
Swap Primary: Move the new certificate to the first position (Index 0). New keys will now be encrypted with this cert, while old keys remain readable via the secondary entry.
Cleanup: After 90 days (default key lifetime), remove the old certificate from the array.

> [!IMPORTANT]
> **Why we don't swap Primary immediately:**
> Making a new cert Primary straight away creates a "race condition." If Server A generates a new key with the new cert, but Server B hasn't updated yet, Server B will fail because it cannot decrypt the new key. Adding it as a secondary first "teaches" all nodes how to read the new cert before any node starts writing with it.

**🔍 Verification (Redis CLI)**

To verify that keys are successfully stored and encrypted:
Locate the Key: redis-cli KEYS *DataProtection*
Inspect Content: redis-cli LRANGE "DataProtection-Keys" 0 -1
Check Encryption: Look for the <encryptedSecret> tag in the XML. If you see <masterKey> in plaintext, encryption is NOT active, and keys are vulnerable if Redis is breached.


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