# syntax=docker/dockerfile:1
# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia le csproj prima per cache dei restore
COPY OnCallendar.sln ./
COPY src/OnCallendar.Domain/OnCallendar.Domain.csproj         src/OnCallendar.Domain/
COPY src/OnCallendar.Application/OnCallendar.Application.csproj src/OnCallendar.Application/
COPY src/OnCallendar.Infrastructure/OnCallendar.Infrastructure.csproj src/OnCallendar.Infrastructure/
COPY src/OnCallendar.Api/OnCallendar.Api.csproj               src/OnCallendar.Api/

RUN dotnet restore src/OnCallendar.Api/OnCallendar.Api.csproj

# Copia il resto e pubblica
COPY . .
RUN dotnet publish src/OnCallendar.Api/OnCallendar.Api.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

# Railway inietta $PORT (di solito 8080); l'app legge env e usa quella
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
EXPOSE 8080

ENTRYPOINT ["dotnet", "OnCallendar.Api.dll"]
