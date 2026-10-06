FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

RUN apt-get update && apt-get install -y build-essential

WORKDIR /src

COPY Program.cs .
COPY docker-hosts.csproj .

RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:10.0

WORKDIR /app

COPY --from=build /app .

ENTRYPOINT ["/app/docker-hosts"]
