FROM mcr.microsoft.com/dotnet/sdk:7.0 as build-env

WORKDIR /src
COPY *.csproj .
RUN dotnet restore
COPY * .
RUN dotnet publish -c Release -o /publish

FROM mcr.microsoft.com/dotnet/runtime:7.0 as runtime
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=0
ENV CULTURE=en-us
WORKDIR /publish
COPY --from=build-env /publish .
ENTRYPOINT ["dotnet", "emoncms-masto.dll"]