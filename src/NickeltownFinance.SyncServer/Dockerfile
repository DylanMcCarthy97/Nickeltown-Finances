FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/NickeltownFinance.SyncServer/NickeltownFinance.SyncServer.csproj", "NickeltownFinance.SyncServer/"]
RUN dotnet restore "NickeltownFinance.SyncServer/NickeltownFinance.SyncServer.csproj"
COPY src/NickeltownFinance.SyncServer/ NickeltownFinance.SyncServer/
WORKDIR /src/NickeltownFinance.SyncServer
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENV SyncDataRoot=/data
VOLUME ["/data"]
ENTRYPOINT ["dotnet", "NickeltownFinance.SyncServer.dll"]
