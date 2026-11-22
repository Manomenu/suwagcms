FROM mcr.microsoft.com/dotnet/sdk:10.0.401-alpine3.23 AS build
WORKDIR /src
COPY .editorconfig global.json nuget.config Directory.Build.props Directory.Build.rsp Directory.Packages.props ./
COPY Suwagcms.Sdk/Suwagcms.Sdk.csproj Suwagcms.Sdk/packages.lock.json Suwagcms.Sdk/
COPY Suwagcms.Contracts/Suwagcms.Contracts.csproj Suwagcms.Contracts/packages.lock.json Suwagcms.Contracts/
COPY Suwagcms.Server/Suwagcms.Server.csproj Suwagcms.Server/packages.lock.json Suwagcms.Server/
RUN dotnet restore Suwagcms.Server/Suwagcms.Server.csproj --locked-mode
COPY Suwagcms.Sdk/ Suwagcms.Sdk/
COPY Suwagcms.Contracts/ Suwagcms.Contracts/
COPY Suwagcms.Server/ Suwagcms.Server/
RUN dotnet publish Suwagcms.Server/Suwagcms.Server.csproj -c Release --no-restore -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12-alpine3.23
WORKDIR /app
COPY --from=build /out ./
ENV ASPNETCORE_URLS=http://+:6330
USER app
EXPOSE 6330
ENTRYPOINT ["dotnet", "Suwagcms.Server.dll"]
