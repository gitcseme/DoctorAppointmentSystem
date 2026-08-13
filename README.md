# Appointer — Doctor Appointment System

A .NET 9 hobby project for booking doctor appointments with **serial numbers** (1, 2, 3… per doctor/hospital/day) instead of time slots. The interesting part isn't CRUD — it's that the same booking operation is implemented **three different ways** (Postgres row lock, Redis distributed lock, RabbitMQ queue) behind a single `Mode`/`Provider` switch, so this doubles as a playground for comparing concurrency strategies.

> **Read this first when you come back to the project.** The diagrams below are the fastest way to re-load the mental model without re-reading the code.

## Quick start

```bash
# requires Docker Desktop running
dotnet run --project src/DoctorAppointmentSystem.AppHost
# -> Aspire dashboard: http://localhost:15000 (Postgres/Redis/RabbitMQ containers + API)
```

Switch behavior via `src/DoctorAppointmentSystem.Api/appsettings.json`:

```json
"Appointment": { "Mode": "Sync", "Provider": "Postgres" }  // Postgres | Redis
"Appointment": { "Mode": "Async", "Provider": "RabbitMq" }
```

This is read **once at startup** — changing it requires a restart, not just a config reload.

## The big picture

Everything hangs off one decision: is booking handled **synchronously in the request** (client waits for a DB write) or **asynchronously via a queue** (client gets a ticket and polls)?

```mermaid
flowchart TB
    Client(["🖥️ <b>Client</b>"])

    subgraph API[" 🌐 Api layer "]
        direction LR
        V1["<b>V1AppointmentsController</b><br/>/api/v1/appointments"]
        V2["<b>V2AppointmentsController</b><br/>/api/v2/appointments"]
    end

    subgraph APP[" ⚙️ Application layer "]
        direction LR
        SyncSvc["<b>SyncAppointmentService</b>"]
        AsyncSvc["<b>AsyncAppointmentService</b>"]
        PgProc["PostgresAppointmentProcessor"]
        RedisProc["RedisAppointmentProcessor"]
        MqProc["RabbitMqAppointmentProcessor"]
    end

    subgraph INFRA[" 🧱 Infrastructure layer "]
        Worker["<b>AppointmentConsumerWorker</b><br/>(BackgroundService)"]
    end

    Postgres[("🐘 <b>PostgreSQL</b><br/>appointments<br/>appointment_counters")]
    Redis[("🔴 <b>Redis</b><br/>locks · counters<br/>status · in-flight")]
    RabbitMQ{{"🐰 <b>RabbitMQ</b><br/>appointments exchange<br/>appointment-creation queue"}}

    Client ==>|"201 Created<br/>(waits)"| V1
    Client ==>|"202 Accepted<br/>(polls)"| V2

    V1 --> SyncSvc
    SyncSvc -.->|"Provider=Postgres"| PgProc
    SyncSvc -.->|"Provider=Redis"| RedisProc
    PgProc ==>|"① FOR UPDATE lock<br/>② INSERT — 1 txn"| Postgres
    RedisProc ==>|"① lock + INCR"| Redis
    RedisProc ==>|"② INSERT"| Postgres

    V2 --> AsyncSvc
    AsyncSvc ==>|"① mark in-flight"| Redis
    AsyncSvc --> MqProc
    MqProc ==>|"② reserve serial"| Redis
    MqProc ==>|"③ publish"| RabbitMQ
    MqProc ==>|"④ SetProcessing"| Redis
    RabbitMQ ==>|"consume"| Worker
    Worker ==>|"⑤ INSERT"| Postgres
    Worker ==>|"⑥ SetStatus<br/>clear in-flight"| Redis

    classDef client fill:#eef2fb,stroke:#5b7fbd,stroke-width:2px,color:#1c2b45,font-weight:bold;
    classDef api fill:#dceaf9,stroke:#2f6fac,stroke-width:2px,color:#0d2438;
    classDef application fill:#ece3f9,stroke:#7c4fd1,stroke-width:2px,color:#2c1a4a;
    classDef infra fill:#fde8d8,stroke:#c9711f,stroke-width:2px,color:#4a2a0d;
    classDef pg fill:#dceefc,stroke:#336791,stroke-width:2px,color:#122c40;
    classDef redis fill:#fbdede,stroke:#c8302a,stroke-width:2px,color:#450e0c;
    classDef mq fill:#ffe9d6,stroke:#e08325,stroke-width:2px,color:#4a2905;

    class Client client;
    class V1,V2 api;
    class SyncSvc,AsyncSvc,PgProc,RedisProc,MqProc application;
    class Worker infra;
    class Postgres pg;
    class Redis redis;
    class RabbitMQ mq;
```

Colors map to layer/technology, arrows are numbered where order matters. Solid double arrows (`==>`) are "does the work," dotted arrows (`-.->`) are "picks which processor based on config." Three layers, top to bottom: **Api** (controllers only route + shape HTTP), **Application** (services validate, processors do the actual work), **Infrastructure** (EF Core, RabbitMQ publisher/consumer, Redis-backed helpers). `DoctorAppointmentSystem.Core` sits underneath all of them with entities/interfaces/DTOs and isn't pictured — nothing depends on the outer layers.

## Flow 1 — Sync + Postgres (`Mode=Sync`, `Provider=Postgres`, default)

The default mode. One HTTP request, one DB transaction, client waits for the `INSERT` to commit.

```mermaid
%%{init: {"sequence": {"actorFontWeight": "bold", "noteFontWeight": "bold"}}}%%
sequenceDiagram
    autonumber
    participant C as 🖥️ Client
    participant Ctrl as V1Controller
    participant Svc as SyncAppointmentService
    participant Proc as PostgresProcessor
    participant DB as 🐘 PostgreSQL

    C->>Ctrl: POST /api/v1/appointments

    Note over Ctrl,DB: 🔎 — Validate —
    Ctrl->>Svc: CreateAsync(...)
    Svc->>DB: patient / doctor+hospital exist?
    Svc->>DB: already booked this day?
    alt not found or duplicate
        Svc-->>Ctrl: ❌ throw (404 / 409)
    end

    Note over Proc,DB: 🔒 — Lock, check limit, write (one transaction) —
    Svc->>Proc: ProcessAsync(...)
    Proc->>DB: BEGIN (ReadCommitted)
    Proc->>DB: SELECT counter ... FOR UPDATE
    Note right of DB: single-row lock — no MAX()/COUNT() scan
    Proc->>Proc: count >= daily_limit?
    alt limit reached
        Proc-->>Svc: ❌ DailyLimitReachedException (409)
        Proc->>DB: ROLLBACK
    else within limit
        Proc->>DB: UPDATE counter (serial++, count++)
        Proc->>DB: INSERT appointment (Scheduled)
        Proc->>DB: COMMIT
        Proc-->>Svc: ✅ appointmentId, serialNumber
    end

    Svc-->>Ctrl: SyncAppointmentResponse
    Ctrl-->>C: ✅ 201 Created {appointmentId, serialNumber}
```

The `appointment_counters` table exists purely so this lock only ever touches **one row** — no scanning existing appointments to compute the next serial or check the count.

## Flow 2 — Sync + Redis (`Mode=Sync`, `Provider=Redis`)

Same controller/service, different processor: no DB transaction at all for the increment — the daily counter and the lock both live in Redis, Postgres only gets a plain `INSERT`.

```mermaid
%%{init: {"sequence": {"actorFontWeight": "bold", "noteFontWeight": "bold"}}}%%
sequenceDiagram
    autonumber
    participant Svc as SyncAppointmentService
    participant Proc as RedisProcessor
    participant RSN as RedisSerialNumberService
    participant Redis as 🔴 Redis
    participant DB as 🐘 PostgreSQL

    Svc->>Proc: ProcessAsync(...)
    Proc->>RSN: GetNextSerialNumberAsync(id, date, dailyLimit)

    Note over RSN,Redis: 🔒 — Reserve serial: Redis lock + INCR, no DB txn —
    RSN->>Redis: acquire distributed lock (30s timeout)
    RSN->>Redis: INCR appt:serial:{id}:{date}
    alt over daily limit
        RSN->>Redis: DECR (undo)
        RSN-->>Proc: null
        Proc-->>Svc: ❌ DailyLimitReachedException (409)
    else within limit
        RSN->>Redis: set TTL to next midnight
        RSN-->>Proc: ✅ serialNumber
    end
    RSN->>Redis: release lock (finally)

    Note over Proc,DB: 💾 — Persist: plain single-row INSERT —
    Proc->>DB: INSERT appointment (Scheduled)
    alt insert fails
        Proc->>RSN: DecrementSerialNumberAsync (compensate)
        Proc-->>Svc: ❌ AppointmentCreationFailedException
    end
```

Trade-off to remember: this path is faster under contention (no DB row lock held) but the serial counter and the appointments table can drift if the compensating decrement itself fails — acceptable for a hobby project, not something to trust in production as-is.

## Flow 3 — Async + RabbitMQ (`Mode=Async`, `Provider=RabbitMq`)

The request returns immediately with a reference; the actual `Appointment` row is written later by a background worker. This is the only mode where a `BackgroundService` (`AppointmentConsumerWorker`) is registered at all.

```mermaid
%%{init: {"sequence": {"actorFontWeight": "bold", "noteFontWeight": "bold"}}}%%
sequenceDiagram
    autonumber
    participant C as 🖥️ Client
    participant Ctrl as V2Controller
    participant Svc as AsyncAppointmentService
    participant Proc as RabbitMqProcessor
    participant Redis as 🔴 Redis
    participant MQ as 🐰 RabbitMQ
    participant Worker as ConsumerWorker
    participant DB as 🐘 PostgreSQL

    C->>Ctrl: POST /api/v2/appointments

    Note over Ctrl,MQ: 📨 — Request phase: synchronous, fast —
    Ctrl->>Svc: CreateAsync(...)
    Svc->>DB: patient / doctor / hospital + duplicate-day check
    Svc->>Redis: MarkAsInFlightAsync (SET NX, 5 min TTL)
    alt already in flight
        Svc-->>Ctrl: ❌ DuplicateAppointmentException (409)
    end
    Svc->>Proc: ProcessAsync(...)
    Proc->>Redis: reserve next serial (INCR)
    Proc->>Proc: generate appointmentRef (guid)
    Proc->>MQ: publish AppointmentCreationMessage
    Proc->>Redis: SetProcessingAsync(ref)
    Proc-->>Svc: appointmentRef + statusUrl
    Svc-->>Ctrl: AsyncAppointmentResponse
    Ctrl-->>C: ✅ 202 Accepted {appointmentReference, statusUrl}

    Note over MQ,Worker: ⏱️ decoupled — happens whenever the worker gets to it

    Note over Worker,DB: ⚙️ — Background phase: persist + report status —
    MQ->>Worker: deliver message (prefetch 50, manual ack)
    Worker->>DB: INSERT appointment (serial already reserved)
    alt insert succeeds
        Worker->>Redis: SetCompletedAsync(ref, appointmentId)
        Worker->>MQ: ack ✅
    else insert fails
        Worker->>Redis: SetFailedAsync(ref, error)
        Worker->>MQ: nack + requeue 🔁 (retries forever on transient errors)
    end
    Worker->>Redis: RemoveInFlightMarkerAsync (always)

    C->>Ctrl: GET /api/v2/appointments/status/{ref}
    Ctrl->>Redis: GetStatusAsync(ref)
    Ctrl-->>C: 200 {status: Completed|Failed, appointmentId?}
```

**Two independent Redis mechanisms, don't conflate them:**
- **In-flight marker** (`appointment:inflight:{patientId}:{doctorHospitalId}:{date}`) — set by `AsyncAppointmentService` *before* publishing, so a second request for the same patient/day is rejected with 409 while the first is still processing. Cleared by the worker.
- **Status tracker** (`appointment:status:{ref}`) — what the client polls. `Success=false` means either **still processing** or **failed**; the controller doesn't distinguish them, so a lingering `errorMessage: null` response is "still going," not broken.

**Known rough edge:** if the DB insert keeps failing (e.g. Postgres is down), the worker nacks with `requeue: true` and RabbitMQ redelivers the same message forever — there's no dead-letter queue or retry cap yet.

## Data model

```mermaid
erDiagram
    DOCTOR ||--o{ DOCTOR_HOSPITAL : "works at"
    HOSPITAL ||--o{ DOCTOR_HOSPITAL : "employs"
    DOCTOR_HOSPITAL ||--o{ APPOINTMENT : "has"
    DOCTOR_HOSPITAL ||--o{ APPOINTMENT_COUNTER : "tracks"
    PATIENT ||--o{ APPOINTMENT : "books"

    DOCTOR {
        int Id PK
        string Email "unique"
    }
    HOSPITAL {
        int Id PK
    }
    DOCTOR_HOSPITAL {
        int Id PK
        int DoctorId FK
        int HospitalId FK
        int DailyPatientLimit
    }
    PATIENT {
        int Id PK
        string Email "unique"
    }
    APPOINTMENT {
        int Id PK
        int DoctorHospitalId FK
        int PatientId FK
        date AppointmentDate
        int SerialNumber
        int Status "Scheduled/Completed/Cancelled/NoShow"
    }
    APPOINTMENT_COUNTER {
        int Id PK
        int DoctorHospitalId FK
        date AppointmentDate
        int CurrentSerial
        int AppointmentCount
    }
```

Two unique indexes carry all the correctness guarantees, everything above is just how they get satisfied:
- `Appointment(DoctorHospitalId, PatientId, AppointmentDate)` — one booking per patient per doctor-hospital per day (this is the DB-level backstop behind the app-level duplicate checks in both flows).
- `Appointment(DoctorHospitalId, AppointmentDate, SerialNumber)` — no two appointments share a serial for the same doctor-hospital-day.
- `AppointmentCounter(DoctorHospitalId, AppointmentDate)` — one counter row per doctor-hospital-day (only populated/used by the **Postgres** processor; the Redis and RabbitMQ paths use a Redis key instead and never touch this table).

## Project layout

```
src/
├── DoctorAppointmentSystem.Core            # entities, interfaces, DTOs, exceptions — no dependencies
├── DoctorAppointmentSystem.Application     # Services (validate) + Processors (do the work)
├── DoctorAppointmentSystem.Infrastructure  # EF Core, repositories, RabbitMQ pub/sub, Redis-backed services, migrations
├── DoctorAppointmentSystem.Api             # Controllers, middleware, Program.cs (DI + Mode/Provider wiring)
├── DoctorAppointmentSystem.AppHost         # .NET Aspire entry point — actually starts Postgres/Redis/RabbitMQ + the API
├── DoctorAppointmentSystem.ServiceDefaults # shared Aspire config (health checks, OTel)
└── Tests/DoctorAppointmentSystem.ConsoleApp # Bogus-based data seeder (not a test suite — no automated tests exist yet)
```

`AppHost` is the actual thing you run locally, not `Api` — it wires up and waits for the three containers before starting the API.

## Endpoint reference

| Mode | Endpoint | Response |
|------|----------|----------|
| Sync | `POST /api/v1/appointments` | `201 Created` `{appointmentId, serialNumber}` |
| Sync | `GET /api/v1/appointments/{id}` | `200` appointment detail |
| Async | `POST /api/v2/appointments` | `202 Accepted` `{appointmentReference, statusUrl}` |
| Async | `GET /api/v2/appointments/status/{reference}` | `200` `{status: Completed\|Failed, appointmentId?}` |
| — | `GET /health` | `200`/`503` — DB connectivity |

Common error codes across both versions: `404` (doctor/hospital/patient not found), `409` (duplicate booking or daily limit reached), `400` (bad request shape).

## Known gaps (hobby project, not production)

- No automated tests — `Tests/DoctorAppointmentSystem.ConsoleApp` only seeds fake data.
- No dead-letter queue / retry cap on the RabbitMQ consumer — a permanently-failing insert requeues forever.
- Async status endpoint can't tell "still processing" apart from "failed" except by `errorMessage` being null.
- No auth — `UseAuthorization()` is called but no auth scheme is registered.
- `Mode`/`Provider` switch is startup-only, not hot-swappable.
