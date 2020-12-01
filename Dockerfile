ARG JAVA_VERSION=8-alpine
ARG VERSION=0.0

FROM openjdk:${JAVA_VERSION}

LABEL version=${VERSION}
LABEL description="SMPP Simulator image for testing."
LABEL maintainer="devs@evicertia.com"

RUN  apk add --no-cache supervisor

COPY ./files/SMPPSim.sh /opt/bin/SMPPSim.sh
COPY ./files/main.sh /opt/bin/main.sh
COPY ./files/smppsim.jar /opt/SMPPSim/
COPY ./conf/smppsim.props /opt/SMPPSim/conf/SMPPSim.props

VOLUME /conf
VOLUME /libs

ENTRYPOINT [ "/opt/bin/main.sh"]