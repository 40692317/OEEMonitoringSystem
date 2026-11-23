# OEE Monitoring System

A full-stack web platform that tracks Overall Equipment Effectiveness (OEE) for production machines in real time and uses ML.NET to predict OEE and flag machine state (Normal / Warning / Critical).

Built as my final year project for a BSc (Hons) Computer Science at Edinburgh Napier University.

## Contents

- [What it does](#what-it-does)
- [Tech stack](#tech-stack)
- [Screenshots](#screenshots)
- [Setup](#setup)
- [Machine learning results](#machine-learning-results)
- [A note on the results (data leakage)](#a-note-on-the-results-data-leakage)
- [Tests](#tests)
- [Project structure](#project-structure)

## What it does

OEE is the standard factory metric for how well a machine is used:

```
OEE = Availability x Performance x Quality
```

The system:

- Calculates Availability, Performance, Quality and OEE from machine events (start, stop, idle, maintenance, production and defect counts).
- Shows live dashboards. A background service simulates machine events every 10 seconds and pushes updates to the browser with SignalR.
- Predicts OEE with a regression model and classifies each machine as Normal, Warning or Critical with a classification model.
- Trains three algorithms for each task side by side and keeps the best one.
- Imports and exports production data and reports as Excel files.
- Has user accounts with Admin and Operator roles (ASP.NET Core Identity).

## Tech stack

| Area | Tools |
| --- | --- |
| Backend | C#, ASP.NET Core MVC (.NET 8) |
| Database | PostgreSQL, Entity Framework Core 8 (Npgsql) |
| Real time | SignalR |
| Machine learning | ML.NET 3 (FastTree, FastForest, SDCA) |
| Frontend | Razor views, Chart.js, jQuery, Bootstrap |
| Other | ClosedXML (Excel), Serilog (logging) |
| Tests | NUnit 4, Moq |

Why ML.NET: I compared ML.NET, scikit-learn and TensorFlow against the project's needs. ML.NET runs inside the C# backend, so there is no separate Python service to deploy.

## Screenshots

<img width="1072" height="561" alt="OEE trend chart" src="https://github.com/user-attachments/assets/a1fed1f9-c9cc-437b-acdc-0f0aba513588" />
<img width="1919" height="881" alt="Live dashboard" src="https://github.com/user-attachments/assets/26be4b14-60be-496a-9137-f863d8634fca" />
<img width="1919" height="881" alt="Machine state classification" src="https://github.com/user-attachments/assets/7ce5d7d6-9386-4e5a-8a1b-429645ee5b9b" />
<img width="1919" height="881" alt="OEE regression results" src="https://github.com/user-attachments/assets/edff70f3-91e9-4d01-abdd-094e83957890" />

## Setup

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [PostgreSQL](https://www.postgresql.org/download/) running locally
- The EF Core CLI tool: `dotnet tool install --global dotnet-ef`

### Steps

1. Clone the repository:

   ```bash
   git clone https://github.com/40692317/OEEMonitoringSystem.git
   cd OEEMonitoringSystem
   ```

2. Set the database connection string and the admin password as environment variables in the same terminal you will run the app from. Secrets are not stored in `appsettings.json`. PowerShell:

   ```powershell
   $env:ConnectionStrings__DefaultConnection = "Server=localhost;Port=5432;Database=OEEc;User Id=postgres;Password=<your database password>"
   $env:SeedAdmin__Password = "<choose an admin password>"
   ```

   Bash:

   ```bash
   export ConnectionStrings__DefaultConnection="Server=localhost;Port=5432;Database=OEEc;User Id=postgres;Password=<your database password>"
   export SeedAdmin__Password="<choose an admin password>"
   ```

   The admin password must have at least 6 characters, one digit, one lowercase and one uppercase letter (the Identity rules in `Program.cs`).

3. Create the database from the migrations:

   ```bash
   dotnet ef database update --project OEEMonitoringSystem.csproj
   ```

4. Run the app:

   ```bash
   dotnet run --project OEEMonitoringSystem.csproj
   ```

   Open the URL printed in the console (for example `https://localhost:7073`) and log in with the admin email (`SeedAdmin:Email`, default `admin@gmail.com`) and the password you set.

On start the app seeds the Admin and Operator roles and the admin account, then demo machines, events and alerts for that admin.

- If `SeedAdmin__Password` is not set, no admin account is created, no demo data is seeded, and a warning is written to the console and log.
- If the admin account already exists, its password is reset to the configured value on each start, so changing the variable changes the password. If the variable is not set, the existing password is left alone.

### Configuration

| Setting | Default | Meaning |
| --- | --- | --- |
| `ConnectionStrings:DefaultConnection` | placeholder | PostgreSQL connection string |
| `SeedAdmin:Email` | `admin@gmail.com` | Email of the seeded admin account |
| `SeedAdmin:Password` | not set | Admin password. Set it as an environment variable only, never in a committed file |
| `ML:OeeDatasetCsv` | `ML/OEE_dataset.csv` | Training dataset |
| `ML:TestFraction` | `0.2` | Share of rows held out for testing (80/20 split) |
| `BackgroundSimulation:IntervalSeconds` | `10` | How often simulated machine events are generated |

The trained models are saved in `MLModels/OeeRegression.zip` and `MLModels/OeeClassification.zip`.

## Machine learning results

Dataset: `ML/OEE_dataset.csv`, 15,000 rows, about 5,000 per class, built by `ML/prepare_dataset.py`. Most rows are synthetic, generated to cover many operating scenarios of a pharmaceutical packaging line. 80% of rows were used for training and 20% held out for testing. All numbers below are on the held-out test set.

### Regression: predict OEE

| Algorithm | RMSE | MAE | R² | |
| --- | --- | --- | --- | --- |
| FastTree | 0.3967 | 0.2879 | 0.9998 | selected |
| FastForest (Random Forest) | 2.9746 | 2.2274 | 0.9890 | |
| SDCA (linear) | ~3.12 | | ~0.985 | |

### Classification: Normal / Warning / Critical

| Algorithm | Macro accuracy | Log-loss | |
| --- | --- | --- | --- |
| FastForest, One-vs-All | 98.7% | 0.0403 | selected |
| FastTree, One-vs-All | ~98.3% | ~0.049 | |
| SDCA Maximum Entropy (logistic regression) | 97.97% | 0.0567 | |

## A note on the results (data leakage)

These scores are much higher than you should expect on real factory data, and the reason is in the design, not in clever modelling:

- Both models take **Availability** and **Performance** as input features (`Services/RegressionModelService.cs`, `Services/ClassificationModelService.cs`).
- OEE is defined as Availability x Performance x Quality, so the regression model mostly relearns a known formula. An R² of 0.9998 shows that it learned the formula well, not that it can forecast OEE.
- The Normal / Warning / Critical label is set by fixed thresholds on Availability, Performance, Quality and OEE (`Services/MachineStateLabelHelper.cs`). The classifier mostly relearns those thresholds.
- The data is mostly simulated, so it is cleaner than real sensor data.

What I would change next: predict a **future** OEE value (for example the next shift) from past readings only, leave the current A/P/Q values out of the inputs, and test on real line data with a time-based split instead of a random one.

## Tests

The `OEEMonitoringSystem.Tests` project has **39 NUnit tests** (Moq for the database and Identity layers). All pass.

```bash
dotnet test OEEMonitoringSystem.sln
```

- `OEECalculationServiceTests` (18 tests): invalid and empty periods, events outside the period, runtime from start/stop/idle/maintenance segments, out-of-order events, a machine still running at period end, quality from production and defect counts, negative quantities, performance capped at 100%, and OEE as the product of the three factors.
- `MachineStateLabelHelperTests` (13 test cases): Normal, Warning and Critical boundaries for each factor, the enum mapping and the threshold constants.
- `RoleSeederTests` (8 tests): the admin account is only created when a password is configured, a blank password counts as missing, an existing admin's password follows the setting, and failures are logged without the password.

The tests cover the OEE calculation, state labelling and admin seeding logic. Controllers, views and the ML training pipeline are not covered by automated tests yet.

## Project structure

```
Controllers/              MVC controllers
Data/                     DbContext, role and demo data seeders
Hubs/                     SignalR hub for live updates
ML/                       Dataset and the script that builds it
MLModels/                 Trained ML.NET models (.zip)
Migrations/               EF Core migrations
Models/                   Entities and view models
Services/                 OEE calculation, simulation, ML training and prediction, reports
Utilities/                Helpers
Views/, wwwroot/          Razor views and static files
OEEMonitoringSystem.Tests/  NUnit tests
```
