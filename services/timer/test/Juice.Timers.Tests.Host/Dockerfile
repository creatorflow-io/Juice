#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["nuget.config", "."]
COPY ["services/timer/test/Juice.Timers.Tests.Host/Juice.Timers.Tests.Host.csproj", "services/timer/test/Juice.Timers.Tests.Host/"]
COPY ["services/timer/src/Juice.Timers.BackgroundTasks/Juice.Timers.BackgroundTasks.csproj", "services/timer/src/Juice.Timers.BackgroundTasks/"]
COPY ["services/timer/src/Juice.Timers.EF/Juice.Timers.EF.csproj", "services/timer/src/Juice.Timers.EF/"]
COPY ["services/timer/src/Juice.Timers/Juice.Timers.csproj", "services/timer/src/Juice.Timers/"]
COPY ["core/src/Juice/Juice.csproj", "core/src/Juice/"]
COPY ["core/src/Juice.MediatR/Juice.MediatR.csproj", "core/src/Juice.MediatR/"]
COPY ["core/src/Juice.EF/Juice.EF.csproj", "core/src/Juice.EF/"]
COPY ["core/src/Juice.EventBus.IntegrationEventLog.EF.PostgreSQL/Juice.EventBus.IntegrationEventLog.EF.PostgreSQL.csproj", "core/src/Juice.EventBus.IntegrationEventLog.EF.PostgreSQL/"]
COPY ["core/src/Juice.EventBus.IntegrationEventLog.EF/Juice.EventBus.IntegrationEventLog.EF.csproj", "core/src/Juice.EventBus.IntegrationEventLog.EF/"]
COPY ["core/src/Juice.EventBus/Juice.EventBus.csproj", "core/src/Juice.EventBus/"]
COPY ["services/timer/src/Juice.Timers.EF.SqlServer/Juice.Timers.EF.SqlServer.csproj", "services/timer/src/Juice.Timers.EF.SqlServer/"]
COPY ["core/src/Juice.EventBus.RabbitMQ/Juice.EventBus.RabbitMQ.csproj", "core/src/Juice.EventBus.RabbitMQ/"]
COPY ["services/timer/src/Juice.Timers.EF.PostgreSQL/Juice.Timers.EF.PostgreSQL.csproj", "services/timer/src/Juice.Timers.EF.PostgreSQL/"]
COPY ["core/src/Juice.EventBus.IntegrationEventLog.EF.SqlServer/Juice.EventBus.IntegrationEventLog.EF.SqlServer.csproj", "core/src/Juice.EventBus.IntegrationEventLog.EF.SqlServer/"]
COPY ["core/src/Juice.MediatR.RequestManager.EF.SqlServer/Juice.MediatR.RequestManager.EF.SqlServer.csproj", "core/src/Juice.MediatR.RequestManager.EF.SqlServer/"]
COPY ["core/src/Juice.MediatR.RequestManager.EF/Juice.MediatR.RequestManager.EF.csproj", "core/src/Juice.MediatR.RequestManager.EF/"]
COPY ["core/src/Juice.MediatR.RequestManager.EF.PostgreSQL/Juice.MediatR.RequestManager.EF.PostgreSQL.csproj", "core/src/Juice.MediatR.RequestManager.EF.PostgreSQL/"]
COPY ["services/timer/src/Juice.Timers.Api/Juice.Timers.Api.csproj", "services/timer/src/Juice.Timers.Api/"]
COPY ["core/src/Juice.Integrations/Juice.Integrations.csproj", "core/src/Juice.Integrations/"]
COPY ["services/timer/src/Juice.Timers.Api.Contracts/Juice.Timers.Api.Contracts.csproj", "services/timer/src/Juice.Timers.Api.Contracts/"]
RUN dotnet restore "services/timer/test/Juice.Timers.Tests.Host/Juice.Timers.Tests.Host.csproj"
COPY . .
WORKDIR "/src/services/timer/test/Juice.Timers.Tests.Host"
RUN dotnet build "Juice.Timers.Tests.Host.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Juice.Timers.Tests.Host.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Juice.Timers.Tests.Host.dll"]