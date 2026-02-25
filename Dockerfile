# build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ChatApp.sln ./
COPY src/ChatApp.Domain/ChatApp.Domain.csproj src/ChatApp.Domain/
COPY src/ChatApp.Application/ChatApp.Application.csproj src/ChatApp.Application/
COPY src/ChatApp.Infrastructure/ChatApp.Infrastructure.csproj src/ChatApp.Infrastructure/
COPY src/ChatApp.Api/ChatApp.Api.csproj src/ChatApp.Api/

RUN dotnet restore

COPY . .
RUN dotnet publish src/ChatApp.Api/ChatApp.Api.csproj -c Release -o /app/publish

# runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ChatApp.Api.dll"]