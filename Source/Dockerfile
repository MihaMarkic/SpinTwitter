FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["SpinTwitter/SpinTwitter.csproj", "SpinTwitter/"]
RUN dotnet restore "SpinTwitter/SpinTwitter.csproj"
COPY . .
WORKDIR "/src/SpinTwitter"
RUN dotnet build "SpinTwitter.csproj" -c Release -o /app/build
WORKDIR "/src/SpinTwitter.Test"
RUN dotnet test "SpinTwitter.Test.csproj" -c Release

WORKDIR "/src/SpinTwitter"
FROM build AS publish
RUN dotnet publish "SpinTwitter.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
USER 9000
ENTRYPOINT ["dotnet", "SpinTwitter.dll"]
VOLUME /app/state
VOLUME /app/log