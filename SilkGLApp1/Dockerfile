FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["SilkGLApp1/SilkGLApp1.csproj", "SilkGLApp1/"]
RUN dotnet restore "SilkGLApp1/SilkGLApp1.csproj"
COPY . .
WORKDIR "/src/SilkGLApp1"
RUN dotnet build "./SilkGLApp1.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./SilkGLApp1.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SilkGLApp1.dll"]
