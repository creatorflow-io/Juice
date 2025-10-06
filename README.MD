# JUICE
`Juice is a simple and lightweigh code base that help developers to build microservices system.`

## Structure

### Core projects
Define the abstractions and default implementation for the API, event bus, audit...
that you need to build a micro, auditable and multi-tenants service.

#### Juice.*

Define base entity, common, utils, helper, extensions... class

#### Juice.EventBus.*

Implement event bus with related feature.

#### Juice.Extensions.*

Implement logging, configuration, options extenstion.

#### Juice.MediatR

Implement MediatR pattern with request, request handler, pipeline behavior.

``` ini
Juice.MediatR
MediatorBench.SendAsync: DefaultJob
Runtime = .NET 8.0.19 (8.0.19, 8.0.1925.36514), X64 RyuJIT x86-64-v3; GC = Concurrent Workstation
Mean = 3.513 us, StdErr = 0.018 us (0.51%), N = 24, StdDev = 0.087 us
Min = 3.369 us, Q1 = 3.454 us, Median = 3.521 us, Q3 = 3.578 us, Max = 3.655 us
IQR = 0.124 us, LowerFence = 3.269 us, UpperFence = 3.764 us
ConfidenceInterval = [3.446 us; 3.580 us] (CI 99.9%), Margin = 0.067 us (1.92% of Mean)
Skewness = -0.11, Kurtosis = 1.89, MValue = 2
-------------------- Histogram --------------------
[3.342 us ; 3.425 us) | @@@@
[3.425 us ; 3.514 us) | @@@@@@
[3.514 us ; 3.594 us) | @@@@@@@@@@
[3.594 us ; 3.674 us) | @@@@
---------------------------------------------------

// * Summary *

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26100.6584/24H2/2024Update/HudsonValley)
12th Gen Intel Core i9-12900 2.40GHz, 1 CPU, 24 logical and 16 physical cores
.NET SDK 9.0.304
  [Host]     : .NET 8.0.19 (8.0.19, 8.0.1925.36514), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 8.0.19 (8.0.19, 8.0.1925.36514), X64 RyuJIT x86-64-v3


| Method    | Mean     | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|---------- |---------:|----------:|----------:|-------:|-------:|----------:|
| SendAsync | 3.513 us | 0.0673 us | 0.0875 us | 0.7782 | 0.1831 |  11.96 KB |
```

#### Juice.MultiTenant.*

Integrated with [Finbuckle.MultiTenant](https://github.com/Finbuckle/Finbuckle.MultiTenant)

### Services
Define the base services like background service, storage... to build a file management system.

## Road map

Version 6.0

- [x] Entity framework
    - [x] Full auditable with data event handler
    - [x] Dynamic expandable with JSON property
    - [x] Id string generator
    - [x] Dynamic schema migration
    - [x] Unit of work pattern
- [x] Microservices pattern
    - [x] RabbitMQ event bus
    - [x] IntegrationEventLog with EF store
    - [x] MediatR Identified command
- [x] Extensions
    - [x] Options mutable
- [x] MultiTenant as microservice
    - [x] [Finbuckle.MultiTenant](https://github.com/Finbuckle/Finbuckle.MultiTenant) integration
    - [x] Tenant gRPC store
    - [x] Tenant options
    - [x] Tenant JSON configuration provider
    - [x] Tenant EF configuration provider
    - [x] Tenant gRPC configuration provider
    - [x] EF DbContext base supports multi-tenant, audit, dynamic entity, unit of work
- [x] Background service
    - [x] File watcher (watch folder) service
    - [x] Scheduled service
    - [x] Service managable via API
    - [x] Custom file logging provider: write to custom folder by service and custom file by job.
- [x] File storage
    - [x] Transfer large file size by multiple request
    - [x] Resumable with asset store (currently InMemory store)
    - [x] Write file to FTP, UNC path
    - [x] Extended file's metadata
    - [ ] EF store 
- [x] Workflow
    - [x] [Bpmn](https://bpmn.io/toolkit/bpmn-js/) compatible
    - [x] Yaml support
    - [x] Sub-workflow
    - [ ] Workload/ lightweigh workflow engine
    - [x] gRPC/ Web API
- [ ] App templates
    - [ ] MVC target
    - [ ] API target
    - [ ] SPA target
    - [ ] Microservice target