# Aspire configuration and responsibility comparison

## Scope and conclusion

Source inspection completed on 2026-10-21 for the local Aspire spike.
The baseline is [`run-dashboard.ps1`](../../scripts/run-dashboard.ps1);
the alternative is [`run-aspire.ps1`](../../scripts/run-aspire.ps1) and the
[AppHost](../../src/ServiceBusPoc.AppHost/Program.cs).

**The Compose-owned broker topology and application responsibility boundaries
are preserved. Configuration and lifecycle behavior are not fully equivalent.**
In particular, the AppHost currently overrides all three Producer routing flags
with `true`. Full parity must not be claimed until the gaps below are resolved
or explicitly accepted and deterministic delivery/non-delivery tests pass.

This increment is a source-based comparison, not a new runtime test. It does
not start or stop applications, inspect local secrets, or modify runtime code.
The previous positive routing check does not establish negative-filter parity.

## Ownership and resource graph

| Surface | Existing dashboard workflow | Aspire workflow | Assessment |
|---|---|---|---|
| Emulator and SQL container | Compose | Same Compose file | Preserved; Aspire does not provision an alternative broker. |
| Broker configuration | Mounted `config.json` | Same mount | Preserved; AppHost does not create topics, subscriptions, or rules. |
| Contact consumers | DigitalChannels, Insurance, ParksResorts, Carwash | Same four project references | Same application implementations. |
| Producer | Existing console project | Same project, with AppHost defaults | Different startup input; see Producer findings. |
| Verifier | Not started by `run-dashboard.ps1` | Started with `--connectivity-probe` | Intentional diagnostic addition, not a filter test. |
| Carwash API | Hosted inside the Carwash process | Same process | Same HTTP implementation; not an Aspire proxy. |
| Custom dashboard | Node UI, event publishing, reporting, and log files | Not started | Intentional scope limitation; Aspire's dashboard is not a replacement for the POC UI. |
| Digital Site | CommerceToolsStub, CommerceApi, CartProcessor, FulfilmentStub | Not started | Intentional scope limitation; not a complete replacement launcher. |

Sources: the baseline launcher's `$apps` list and `Start-NodeDashboardWithLogging`,
the [AppHost project references](../../src/ServiceBusPoc.AppHost/ServiceBusPoc.AppHost.csproj),
and [Compose](../../infra/servicebus/compose.yaml).
The configured `commerce.events` topic remains in the emulator even though
Aspire does not launch its Digital Site consumers.

## Topic and subscription configuration

Both workflows use [`config.json`](../../infra/servicebus/config.json), mounted
at `/ServiceBus_Emulator/ConfigFiles/Config.json`.

| Contact subscription | Broker rule | Baseline assignment | AppHost assignment |
|---|---|---|---|
| `digital-channels` | No explicit rule in configuration; receives all events in the previously observed run | `digital-channels` | `digital-channels` |
| `insurance` | `hasInsurance = true` | `insurance` | `insurance` |
| `parks-resorts` | `hasParksResorts = true` | `parks-resorts` | `parks-resorts` |
| `carwash` | `hasCarwashProduct = true` | `carwash` | `carwash` |

The topology namespace is `sbemulatorns` and the contact topic is
`contact.events`. Neither launcher rewrites the filter definitions.
[`ProducerService`](../../src/ContactEvents/Producer/Services/ProducerService.cs)
places the routing flags on Service Bus application properties as booleans.
[`SubscriptionConsumerRunner`](../../src/ServiceBusPoc.Core/Messaging/SubscriptionConsumerRunner.cs)
does not re-filter those properties; selection remains the broker's job.

This proves configuration ownership, not observed routing. The next increment
must check all eight flag combinations, including non-delivery to excluded
subscriptions, with identical inputs in both workflows.

## Environment variables and precedence

### Launcher configuration

Both launchers load the repository-root `.env` into their own process
environment. File values overwrite variables already set in that shell for
matching keys; matching surrounding single or double quotes are removed.
Neither loader implements full shell expansion or inline-comment parsing.
The `.env` must remain ignored and must never be committed or included in
comparison evidence.

| Setting | Existing dashboard workflow | Aspire workflow |
|---|---|---|
| `ServiceBus__ConnectionString` | Unconditionally sets the local emulator connection using `127.0.0.1` | Retains a nonempty value after `.env` loading; otherwise defaults to `localhost:5672` emulator connection |
| `ServiceBus__Namespace` | Unconditionally `localhost` | Retains a nonempty value; otherwise `sbemulatorns` |
| `ServiceBus__TopicName` | Unconditionally `contact.events` for contact apps | Retains a nonempty value; otherwise `contact.events` |
| `ServiceBus__SubscriptionName` | Sets each consumer's fixed name before starting its process | Explicitly sets each resource's fixed name |
| `Dashboard__*` | Forces reporting enabled, selected URL/port, heartbeat 5s, timeout 5s | No explicit AppHost override; reporting defaults to enabled, URL `http://localhost:5100`, heartbeat 5s, timeout 3s unless configured |
| Environment/logging | Sets `DOTNET_Environment=Development` and several logging variables | No equivalent assignments in the launcher/AppHost; do not assume identical effective environment without runtime inspection |
| Digital Site settings | Sets local commerce URLs and per-app overrides | No Digital Site resources or equivalent wiring |

The two default connection strings address the same local AMQP port, but
`localhost` versus IPv4 loopback is not byte-for-byte equivalence and can behave
differently on some hosts. The SDK client is built from the connection string;
`ServiceBus__Namespace` is required configuration but is not used to construct
that client's endpoint. Its differing default therefore does not itself change
the connection target.

The AppHost reads `ServiceBus:*` from its own configuration and explicitly
writes the resulting values into each resource's environment. Direct AppHost
execution does not run the PowerShell `.env` loader or supply its defaults.
Its null checks reject missing values; shared application options also reject
empty/whitespace Service Bus values.

**Operational caution:** unlike the baseline, Aspire honors a supplied
connection string and topic even while its launcher starts the local emulator.
Confirm the target before running it: its Producer publishes a real message to
the configured broker. Starting Compose does not force the applications to
connect to that emulator.

### Application configuration

The contact applications all retain their existing configuration code:

- The consumers and Verifier use `Host.CreateDefaultBuilder(args)` and append
  `AddEnvironmentVariables()`. For duplicate keys, that appended environment
  provider takes precedence over the default builder's earlier providers,
  including its command-line provider.
- The Producer additionally appends `AddCommandLine(args, commandLineMappings)`
  after environment variables. Its mapped command-line options therefore win
  over environment values when the Producer is invoked directly with them.
- AppHost command-line arguments configure the AppHost; they are not general
  forwarding of Producer arguments. It currently specifies no Producer
  `.WithArgs(...)`.
- The baseline starts contact projects with
  `dotnet run --configuration Debug --project ...` and supplies no application
  arguments. Aspire starts project resources and only explicitly adds the
  Verifier argument described below.

Shared binding/validation remains in
[`ServiceCollectionExtensions`](../../src/ServiceBusPoc.Core/DependencyInjection/ServiceCollectionExtensions.cs).
For inherited settings not explicitly wired by the AppHost, verify effective
resource configuration in the next runtime increment rather than assuming
that the AppHost's configuration providers are automatically forwarded.

## Producer defaults and arguments

The application publishes **one event and exits**; it is not a continuous
publisher. The baseline launcher's introductory comment saying "every 3
seconds" is stale and is not evidence of current behavior.

| Input | Baseline/application behavior | AppHost behavior |
|---|---|---|
| Contact ID, first name, last name | Required; no launcher defaults | Nonblank AppHost setting or `aspire-local-contact`, `Aspire`, `Local` |
| Source | Application default `producer`, or supplied setting | Nonblank AppHost setting or `aspire-apphost` |
| Membership number | Optional except when required by the event's Carwash validation | Nonblank AppHost setting or `ASPIRE-LOCAL-001` |
| `HasInsurance`, `HasParksResorts`, `HasCarwashProduct` | Bound values; application defaults are `false` | **Unconditionally `true`; `Producer__*` false overrides are ignored for these three inputs** |
| Email, phone, correlation ID | Optional environment/mapped CLI inputs | No explicit AppHost assignment or fallback |

Safe sample identity defaults are an intentional convenience for the spike.
Forcing the flags is a **configuration-parity gap**, not a change to the broker
filters. It also prevents using the current Aspire-started Producer to exercise
negative combinations through configuration alone.

Blank-value semantics also differ: the AppHost replaces whitespace in its five
defaulted text settings, whereas direct application execution validates required
inputs and preserves the optional membership input. These differences must be
documented if retained.

The [AppHost README](../../src/ServiceBusPoc.AppHost/README.md) must not be
interpreted as promising that every `Producer__*` variable is overridable.

## Verifier scope

Aspire adds `--connectivity-probe`. That branch creates an SDK sender and
message batch for the configured topic, then exits with a success/failure code.
It does not publish a routing matrix, enumerate subscription rules, call the
Carwash API, or prove consumer delivery.

The normal Verifier path invokes the existing topology validator; it is
different from the connectivity branch and still contains a scenario-verifier
stub. The separate [`run-local-poc.ps1`](../../scripts/run-local-poc.ps1)
invokes that normal path. It is not the interactive baseline used in this
comparison and must not be confused with `run-dashboard.ps1`.

Source: [Verifier entry point](../../src/ContactEvents/Verifier/Program.cs) and
[`VerifierService`](../../src/ContactEvents/Verifier/Services/VerifierService.cs).

## Carwash HTTP exposure and responsibilities

The [Carwash entry point](../../src/ContactEvents/Consumers/Carwash/Program.cs)
starts the API module and then the consumer. Both launchers run that same
process without adding a different HTTP adapter.

- [`CarwashApiServer`](../../src/ContactEvents/Consumers/Carwash/Api/CarwashApiServer.cs)
  uses `HttpListener` bound to `http://localhost:5000/` by its constructor
  default and exposes `POST /carwash/v1/verify`.
- The AppHost does not declare a Carwash endpoint, allocate a port, add a
  proxy, or provide service discovery for the API. The address remains fixed;
  `ASPNETCORE_URLS` does not configure this listener.
- `Carwash__ApiUrl` is not the listener-binding setting. The consumer logs it
  under the legacy "Pulse API URL" label but does not call it.
- [`CarwashConsumerService`](../../src/ContactEvents/Consumers/Carwash/Services/CarwashConsumerService.cs)
  delegates message processing to the shared consumer runner. It does not
  depend on the API server, `IMembershipVerifier`, or an HTTP verification
  client.
- The API calls
  [`MockMembershipVerifier`](../../src/ContactEvents/Consumers/Carwash/Services/MockMembershipVerifier.cs),
  whose result depends on the membership number, not consumed messages.
- The other three consumer entry points use the shared messaging services
  without registering or invoking the Carwash API.

**The ADR-009 boundary is preserved:** Pulse/mock Pulse calls the API; consumers
do not call that API, and message receipt does not populate its verification
store. Co-hosting still shares a process lifetime; this is logical independence,
not separate-process fault isolation. See
[ADR-009](../decisions/009-carwash-integration-boundary.md).

Do not run both workflows concurrently against port 5000 or the same
subscriptions: API port contention and competing consumers invalidate a
side-by-side routing comparison.

## Startup, shutdown, and shared-resource ownership

| Concern | Existing dashboard workflow | Aspire workflow |
|---|---|---|
| Emulator readiness | Calls `Wait-ServiceBusEmulatorReady` before application launch | `docker compose up -d` only; no readiness barrier or AppHost `WaitFor` |
| Application startup | Starts Node, then apps in list order with a two-second stagger | Declares resources without readiness dependencies; declaration order is not a readiness guarantee |
| Early connection failures | Emulator readiness check reduces the startup race | Consumers have existing retry logic, but one-shot Producer/Verifier are not protected by a launcher readiness gate |
| Build | Builds the solution and dashboard frontend | CLI builds the AppHost/project graph unless `-NoBuild` |
| Diagnostics | Custom UI and redirected service/emulator logs | Aspire resources/console dashboard; no equivalent custom UI or launcher log capture |
| Certificates | No Aspire certificate-trust step | Runs .NET and Aspire certificate trust before launch |
| Process lifetime | Script tracks processes and invokes cleanup | Aspire CLI/AppHost owns application resource lifetimes; launcher runs CLI in foreground |
| Emulator cleanup | Cleanup invokes Compose `down`, even for reused containers and even after `-NoEmulator` | `finally` calls `down` only if not `-NoEmulator` and pre-start `compose ps -q` returned no containers |

The Aspire ownership check is coarse: any running container in the Compose
project suppresses cleanup ownership. A partially running project can therefore
be completed by `up -d` and left running. Conversely, `compose down` affects the
whole shared project, not an individually tracked container set. Neither
workflow isolates the emulator by worktree: Compose uses fixed project and
container names and fixed host ports.

The baseline cleanup also enumerates and force-stops remaining `dotnet`
processes globally. This is a pre-existing cross-session risk, **not an Aspire
change**, and is another reason not to launch the baseline in this shared
environment during this static increment. This comparison does not execute or
modify that cleanup.

App shutdown semantics under Ctrl+C, abrupt CLI termination, and partial
startup need targeted runtime validation; source inspection alone does not
prove graceful disposal or absence of orphan processes.

## Findings and next-increment gates

| ID | Finding | Disposition |
|---|---|---|
| C1 | Same Compose configuration and contact subscription names; broker remains responsible for filtering | Source comparison passed; verify actual delivery/non-delivery next. |
| C2 | Producer routing flags ignore false overrides | Unintended parity gap; make overrides effective before using the Aspire Producer for the eight-case routing matrix. |
| C3 | Aspire starts one-shot apps without emulator readiness | Startup reliability gap; add equivalent readiness or explicitly pre-establish readiness for controlled tests. |
| C4 | Service Bus target precedence/defaults differ | Record as a deliberate launcher distinction; explicitly set identical target values for comparison and decide whether broader launcher parity is required. |
| C5 | Custom dashboard absent but reporting defaults remain enabled | Intentional UI scope exclusion with a reporting side effect; use `Dashboard__Enabled=false` for isolated Aspire comparisons and verify the effective setting. |
| C6 | Carwash API not declared as an Aspire endpoint | Current fixed local address preserved; endpoint discovery is absent, not an API regression. |
| C7 | Verifier connectivity branch differs from normal validation | Intentional probe only; do not count it as topology/filter proof. |
| C8 | Container cleanup ownership differs; baseline can affect other .NET sessions | Documented operational difference; run workflows sequentially in a controlled environment. |
| C9 | Digital Site and custom publishing UI are absent from Aspire | Intentional spike scope; no full-workflow replacement claim. |

### Next increment

1. Resolve C2 and establish emulator readiness for the test run (C3).
2. Use identical explicit broker/topic values and Producer payload fields in
   both paths; disable custom reporting for the isolated Aspire run.
3. Start only one workflow at a time. Publish uniquely identified events only
   after consumers are ready and account for existing subscription backlog.
4. Exercise all eight routing combinations, asserting delivery to included
   subscriptions and non-delivery to excluded ones without competing consumers.
5. Test Carwash verification separately from message receipt and recheck the
   Verifier probe. Record lifecycle/cleanup results without disturbing unrelated
   processes or shared containers.

No runtime parity gap is fixed by this document. The comparison increment is
complete; deterministic behavior comparison and the identified runtime
remediations remain.
