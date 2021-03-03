ARG JAVA_VERSION=8-alpine
ARG NETCORE_VERSION=3.1-alpine3.12
ARG VERSION="0.0.0.0"
FROM mcr.microsoft.com/dotnet/core/sdk:${NETCORE_VERSION} AS build-env

WORKDIR /opt/SmppSimCatcher

COPY SmppSimCatcher/SmppSimCatcher/ ./
RUN dotnet restore && dotnet publish -c Release -o out

#install java and net core sdk
FROM mcr.microsoft.com/dotnet/core/aspnet:${NETCORE_VERSION} AS runtime

WORKDIR /
#dotnet run inside
ENTRYPOINT [ "/opt/bin/main.sh"]

RUN apk add supervisor && apk add openjdk8

COPY ./files/SMPPSim.sh /opt/bin/
COPY ./files/main.sh /opt/bin/
COPY ./files/smppsim.jar /opt/SMPPSim/
COPY ./files/SMPPSim.ini /etc/supervisor.d/
COPY ./conf/smppsim.props /opt/SMPPSim/conf/SMPPSim.props

VOLUME /conf
VOLUME /libs

COPY --from=build-env /opt/SmppSimCatcher/out /opt/SmppSimCatcher

LABEL version=${VERSION} \
    description="SMPP Simulator image for testing." \
    maintainer="devs@evicertia.com"
