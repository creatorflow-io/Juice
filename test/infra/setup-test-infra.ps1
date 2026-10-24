<#
.SYNOPSIS
    Starts the shared local infrastructure (Docker; also usable by other projects) and writes the Juice connection settings into the
    user secrets of every test project, so the IgnoreOnCIFact integration tests can run.

.DESCRIPTION
    Containers (test/infra/docker-compose.yml, ports and credentials in test/infra/.env): SQL Server 2022, PostgreSQL 16, Redis 7,
    RabbitMQ 3.13, NATS 2.10 with JetStream, and a user/password NATS server for the auth test.

    Settings are merged into %APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json for each
    project under core/test and test/ that declares a UserSecretsId. Nothing is written to the repo.

.EXAMPLE
    ./test/infra/setup-test-infra.ps1            # start containers + write user secrets
    ./test/infra/setup-test-infra.ps1 -SecretsOnly -SkipMigrations
    ./test/infra/setup-test-infra.ps1 -Down      # stop containers (keeps data volumes)
    ./test/infra/setup-test-infra.ps1 -Down -RemoveVolumes
#>
[CmdletBinding()]
param(
    [switch]$Down,
    [switch]$RemoveVolumes,
    [switch]$SecretsOnly,
    [switch]$SkipMigrations
)

$ErrorActionPreference = 'Stop'
$infraDir = $PSScriptRoot
$repoRoot = Resolve-Path (Join-Path $infraDir '..\..')
$composeFile = Join-Path $infraDir 'docker-compose.yml'
$envFile = Join-Path $infraDir '.env'

# Ports, credentials and the test database name are shared with docker compose through .env
$cfg = @{}
foreach ($line in Get-Content $envFile) {
    if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*?)\s*$') {
        $key = $Matches[1]
        $value = $Matches[2]
        # quoted values keep special characters ($, &, !) literal for docker compose
        if ($value.Length -ge 2 -and ($value[0] -eq "'" -or $value[0] -eq '"') -and $value[-1] -eq $value[0]) {
            $value = $value.Substring(1, $value.Length - 2)
        }
        $cfg[$key] = $value
    }
}
$db = $cfg.TEST_DATABASE

if ($Down) {
    $downArgs = @('compose', '-f', $composeFile, '--env-file', $envFile, 'down')
    if ($RemoveVolumes) { $downArgs += '-v' }
    & docker @downArgs
    exit $LASTEXITCODE
}

if (-not $SecretsOnly) {
    Write-Host 'Starting test infrastructure (first run pulls images)...'
    & docker compose -f $composeFile --env-file $envFile up -d --wait
    if ($LASTEXITCODE -ne 0) { throw 'docker compose up failed.' }

    # Create the test database up front: several test projects migrate them, and letting them race
    # to create the database on first run fails with "already exists" / "does not exist".
    Write-Host "Ensuring test database '$db' exists..."
    & docker exec dev-sqlserver /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P $cfg.SQLSERVER_SA_PASSWORD -b `
        -Q "IF DB_ID('$db') IS NULL CREATE DATABASE [$db];" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Could not create the SQL Server database $db." }
    # the health check (ping) can pass before the broker app has finished booting on first start
    & docker exec dev-rabbitmq rabbitmqctl -q await_startup --timeout 120 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'RabbitMQ did not finish starting.' }
    & docker exec dev-rabbitmq rabbitmqctl -q add_vhost juice1 2>$null | Out-Null
    & docker exec dev-rabbitmq rabbitmqctl -q set_permissions -p juice1 $cfg.RABBITMQ_USER '.*' '.*' '.*' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not set up the RabbitMQ virtual host juice1.' }
    $exists = & docker exec dev-postgres psql -U $cfg.POSTGRES_USER -tAc "SELECT 1 FROM pg_database WHERE datname = '$db'"
    if ("$exists".Trim() -ne '1') {
        & docker exec dev-postgres psql -U $cfg.POSTGRES_USER -c "CREATE DATABASE \`"$db\`"" | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Could not create the PostgreSQL database $db." }
    }
}

$sqlServer = "Server=localhost,$($cfg.SQLSERVER_PORT);Database=$db;User Id=sa;Password=$($cfg.SQLSERVER_SA_PASSWORD);TrustServerCertificate=True;MultipleActiveResultSets=True"
$postgres = "Host=localhost;Port=$($cfg.POSTGRES_PORT);Database=$db;Username=$($cfg.POSTGRES_USER);Password=$($cfg.POSTGRES_PASSWORD)"
$redis = "localhost:$($cfg.REDIS_PORT)"

$settings = [ordered]@{
    'ConnectionStrings:Default'             = $sqlServer
    'ConnectionStrings:SqlServerConnection' = $sqlServer
    'ConnectionStrings:PostgreConnection'   = $postgres
    'ConnectionStrings:Redis'               = $redis
    'ConnectionStrings:RedisSentinel'       = $redis
    'Redis:ConfigurationOptions:EndPoints:0' = $redis
    'Juice:EventBus:Connections:Nats:Url'   = "nats://localhost:$($cfg.NATS_PORT)"
    'Nats:DockerContainer'                  = 'dev-nats'
    'Nats:Auth:Url'                         = "nats://localhost:$($cfg.NATS_AUTH_PORT)"
    'Nats:Auth:UserName'                    = $cfg.NATS_AUTH_USER
    'Nats:Auth:Password'                    = $cfg.NATS_AUTH_PASSWORD
}
foreach ($section in 'Juice:EventBus:Connections:RabbitMQ', 'Juice:EventBus:Connections:RabbitMQ1', 'RabbitMQ') {
    $settings["${section}:Connection"] = 'localhost'
    $settings["${section}:Port"] = $cfg.RABBITMQ_PORT
    $settings["${section}:UserName"] = $cfg.RABBITMQ_USER
    $settings["${section}:Password"] = $cfg.RABBITMQ_PASSWORD
}
# RabbitMQ1 is a second, independent broker in the tests (it re-declares exchanges with different
# arguments), so it gets its own virtual host.
$settings['Juice:EventBus:Connections:RabbitMQ1:VirtualHost'] = 'juice1'

$secretsRoot = Join-Path $env:APPDATA 'Microsoft\UserSecrets'
$projects = Get-ChildItem -Path (Join-Path $repoRoot 'core\test'), (Join-Path $repoRoot 'test') -Filter *.csproj -Recurse |
    Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }

foreach ($project in $projects) {
    $match = Select-String -Path $project.FullName -Pattern '<UserSecretsId>([^<]+)</UserSecretsId>'
    if (-not $match) { continue }
    $id = $match.Matches[0].Groups[1].Value
    $dir = Join-Path $secretsRoot $id
    $file = Join-Path $dir 'secrets.json'
    New-Item -ItemType Directory -Force -Path $dir | Out-Null

    $secrets = [ordered]@{}
    if (Test-Path $file) {
        $existing = Get-Content $file -Raw | ConvertFrom-Json
        if ($existing) {
            foreach ($p in $existing.PSObject.Properties) { $secrets[$p.Name] = $p.Value }
        }
    }
    foreach ($key in $settings.Keys) { $secrets[$key] = $settings[$key] }

    $json = $secrets | ConvertTo-Json -Depth 5
    [System.IO.File]::WriteAllText($file, $json, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host ("  secrets -> {0} ({1})" -f $project.BaseName, $id)
}

if (-not $SkipMigrations) {
    # Several test classes use tables that another class migrates; when the classes run in parallel on a fresh
    # database they race. Apply the migrations once, up front, by running only the migration tests.
    Write-Host 'Applying test database migrations...'
    $migrations = @(
        @{ Project = 'core\test\Juice.EF.Tests'; Filter = 'FullyQualifiedName~EF_should_be_migration_Async' },
        @{ Project = 'core\test\Juice.Messaging.Idempotency.Tests'; Filter = 'FullyQualifiedName~Schema_MigrationAsync' }
    )
    foreach ($m in $migrations) {
        & dotnet test (Join-Path $repoRoot $m.Project) -f net10.0 --nologo -v q --filter $m.Filter | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Migration tests failed in $($m.Project). Run them directly to see the error." }
        Write-Host "  migrated -> $($m.Project)"
    }
}

Write-Host ''
Write-Host 'Test infrastructure is ready (values from test/infra/.env):'
Write-Host "  SQL Server  localhost,$($cfg.SQLSERVER_PORT)   sa / $($cfg.SQLSERVER_SA_PASSWORD)"
Write-Host "  PostgreSQL  localhost:$($cfg.POSTGRES_PORT)   $($cfg.POSTGRES_USER) / $($cfg.POSTGRES_PASSWORD)"
Write-Host "  Redis       localhost:$($cfg.REDIS_PORT)   (no password)"
Write-Host "  RabbitMQ    localhost:$($cfg.RABBITMQ_PORT)   $($cfg.RABBITMQ_USER) / $($cfg.RABBITMQ_PASSWORD)   (UI http://localhost:$($cfg.RABBITMQ_UI_PORT))"
Write-Host "  NATS        localhost:$($cfg.NATS_PORT)   JetStream, no auth (monitor http://localhost:$($cfg.NATS_MONITOR_PORT))"
Write-Host "  NATS auth   localhost:$($cfg.NATS_AUTH_PORT)   $($cfg.NATS_AUTH_USER) / $($cfg.NATS_AUTH_PASSWORD)"
Write-Host ''
Write-Host 'Run test projects and target frameworks one at a time (they share the test databases), e.g.:'
Write-Host '  dotnet test Juice.sln -m:1 -p:TestTfmsInParallel=false'
