# Container image for ExpenseManagement.Api.
#
# Lives at the repository root because that is where Render, Railway and most
# PaaS builders look by default, and because the build needs the whole backend
# tree as context anyway.
#
# Build locally with:
#   docker build -t expense-api .
#   docker run --rm -p 8080:8080 \
#     -e ConnectionStrings__DefaultConnection="..." \
#     -e Jwt__Secret="a-real-32-plus-character-secret" \
#     expense-api

# ---------------------------------------------------------------------------
# Build
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project files are copied on their own first so `restore` lands in a layer that
# only changes when a dependency changes. Copying the whole tree up front would
# invalidate the restore cache on every source edit and add minutes to each
# build.
COPY backend/ExpenseManagement.slnx ./
COPY backend/ExpenseManagement.Domain/ExpenseManagement.Domain.csproj                 ExpenseManagement.Domain/
COPY backend/ExpenseManagement.Application/ExpenseManagement.Application.csproj       ExpenseManagement.Application/
COPY backend/ExpenseManagement.Infrastructure/ExpenseManagement.Infrastructure.csproj ExpenseManagement.Infrastructure/
COPY backend/ExpenseManagement.Api/ExpenseManagement.Api.csproj                       ExpenseManagement.Api/
COPY backend/tests/ExpenseManagement.UnitTests/ExpenseManagement.UnitTests.csproj               tests/ExpenseManagement.UnitTests/
COPY backend/tests/ExpenseManagement.IntegrationTests/ExpenseManagement.IntegrationTests.csproj tests/ExpenseManagement.IntegrationTests/

# Restore only the API's graph. Restoring the solution would pull the test
# projects' packages into the runtime image's build for no reason.
RUN dotnet restore ExpenseManagement.Api/ExpenseManagement.Api.csproj

COPY backend/ ./

RUN dotnet publish ExpenseManagement.Api/ExpenseManagement.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    # The apphost is a native launcher we never use — the entrypoint invokes
    # `dotnet ExpenseManagement.Api.dll` directly.
    #
    # `-p:` rather than `/p:`: both are valid MSBuild, but a leading slash is
    # rewritten into a Windows path by MSYS shells, so the dash form is the one
    # that behaves identically whether this command is run here or pasted into
    # a developer's Git Bash to reproduce a build failure.
    -p:UseAppHost=false

# ---------------------------------------------------------------------------
# Runtime
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl is here purely so the HEALTHCHECK below can run. If you would rather not
# ship it, drop both this line and the HEALTHCHECK — the platform's own probe
# against /health is enough on Render.
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Receipts are written here. On a PaaS with an ephemeral filesystem this is
# wiped on every deploy — mount a volume, or point FileStorage__RootPath at
# object storage, before anyone uploads anything they expect to keep.
RUN mkdir -p /app/storage /app/logs

# The aspnet image ships a non-root `app` user (uid 1654). Running as root
# inside a container is a needless escalation path, and nothing here needs it.
RUN chown -R app:app /app
USER app

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    # Serilog's console sink is the log pipeline in a container; the file sink
    # writes to a filesystem nobody will read.
    DOTNET_gcServer=1

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
    CMD curl -fsS "http://localhost:${PORT:-8080}/health" || exit 1

# Shell form on purpose: ${PORT} has to expand at RUNTIME. Render, Railway and
# Heroku all assign the port dynamically and expect the process to bind it, and
# an `ENV ASPNETCORE_URLS=http://+:${PORT}` would be baked in at build time as
# the literal string. Binding + rather than localhost is what makes the port
# reachable from outside the container at all.
CMD ASPNETCORE_URLS="http://+:${PORT:-8080}" exec dotnet ExpenseManagement.Api.dll
