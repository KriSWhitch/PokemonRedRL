# Multi-stage build for PokemonRedRL.Agent
# Stage 1: SDK — restore and publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only project files first for layer caching
COPY src/PokemonRedRL.Agent/PokemonRedRL.Agent.csproj src/PokemonRedRL.Agent/
COPY src/PokemonRedRL.Core/PokemonRedRL.Core.csproj src/PokemonRedRL.Core/
COPY src/PokemonRedRL.Models/PokemonRedRL.Models.csproj src/PokemonRedRL.Models/
COPY src/PokemonRedRL.DAL/PokemonRedRL.DAL.csproj src/PokemonRedRL.DAL/
COPY src/PokemonRedRL.Utils/PokemonRedRL.Utils.csproj src/PokemonRedRL.Utils/

RUN dotnet restore src/PokemonRedRL.Agent/PokemonRedRL.Agent.csproj -r linux-x64

# Copy the rest of the source
COPY src/ src/

RUN dotnet publish src/PokemonRedRL.Agent/PokemonRedRL.Agent.csproj -c Release -o /app -r linux-x64 --no-restore --self-contained false

# Stage 2: runtime
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

# Copy published output from build stage
COPY --from=build /app .

# Create directories for model checkpoints and data
RUN mkdir -p /app/src/data/models /app/data/redis_backups /app/data/checkpoints

ENTRYPOINT ["dotnet", "PokemonRedRL.Agent.dll"]
