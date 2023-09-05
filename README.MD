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

#### Juice.MediatR.*

Integrated with MediatR.

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