# syntax=docker/dockerfile:1
# Multi-stage build for the ReeTrack API (.NET 10 / ASP.NET Core).
# Produces the runtime image plus a self-contained EF Core migration bundle (./efbundle)
# that the compose "migrate" service runs before the API starts.
#
# Build context: the backend/ repo root.
#   docker build -t reetrack-api .

# ---- build stage ----------------------------------------------------------
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (cached layer) — copy only project files, restore the API graph.
COPY src/ReeTrack.Domain/ReeTrack.Domain.csproj          src/ReeTrack.Domain/
COPY src/ReeTrack.Application/ReeTrack.Application.csproj src/ReeTrack.Application/
COPY src/ReeTrack.Infrastructure/ReeTrack.Infrastructure.csproj src/ReeTrack.Infrastructure/
COPY src/ReeTrack.Api/ReeTrack.Api.csproj                src/ReeTrack.Api/
RUN dotnet restore src/ReeTrack.Api/ReeTrack.Api.csproj

# Copy the rest of the source and publish.
COPY src/ src/
RUN dotnet publish src/ReeTrack.Api/ReeTrack.Api.csproj -c Release -o /app/publish --no-restore

# Build a self-contained EF migration bundle. Applying migrations is a separate deploy step
# (Program.cs only auto-migrates in Development). The bundle embeds all migrations and applies
# them against the connection string passed at run time.
# DesignTimeDbContextFactory requires ConnectionStrings__Default to construct the context while
# building the model — a dummy value is enough (no DB connection is made during bundling), and it
# lives only in this throwaway build stage.
ENV PATH="$PATH:/root/.dotnet/tools"
ENV ConnectionStrings__Default="Host=localhost;Port=5432;Database=reetrack;Username=build;Password=build"
RUN dotnet tool install --global dotnet-ef --version 10.0.9
RUN dotnet ef migrations bundle \
        --project src/ReeTrack.Infrastructure/ReeTrack.Infrastructure.csproj \
        --startup-project src/ReeTrack.Api/ReeTrack.Api.csproj \
        --configuration Release --self-contained -r linux-x64 \
        -o /app/efbundle

# ---- runtime stage --------------------------------------------------------
FROM --platform=linux/amd64 mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# QuestPDF (PDF export) and ClosedXML (Excel column sizing) need fontconfig + a base font
# on the slim image, otherwise server-side rendering throws at request time.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 fontconfig fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish ./
COPY --from=build /app/efbundle ./efbundle
RUN chmod +x ./efbundle

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ReeTrack.Api.dll"]
