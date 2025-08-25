FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build-env
ARG TARGETPLATFORM
ARG TARGETOS
ARG TARGETARCH
ARG TARGETVARIANT
ARG BUILDPLATFORM
ARG BUILDOS
ARG BUILDARCH
ARG BUILDVARIANT

RUN apt-get update && apt-get install -y fontconfig libfontconfig1 libfreetype6
WORKDIR /src
COPY *.csproj .
RUN dotnet restore -a $TARGETARCH
COPY * .
RUN dotnet publish -a $TARGETARCH --no-restore -c Release -o /publish

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=0
ENV CULTURE=en-us
WORKDIR /publish
COPY --from=build-env /publish .
RUN mkdir -p /publish/output/ && apt-get update && apt-get install -y fontconfig libfontconfig1 libfreetype6
ENTRYPOINT ["dotnet", "emoncms-masto.dll"]
