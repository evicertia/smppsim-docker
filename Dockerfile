ARG JAVA_VERSION=8
ARG VERSION=0.0

FROM anapsix/alpine-java:latest

LABEL version=${VERSION}
LABEL description="SMPP Simulator image for testing."
LABEL maintainer="devs@evicertia.com"

RUN  apk add --no-cache supervisor

COPY ./files/functions /etc/rc.d/init.d/functions
COPY ./files/SMPPSim.sh /opt/SMPPSim/SMPPSim.sh
COPY ./files/*.jar /opt/SMPPSim/

VOLUME /opt/SMPPSim/conf

ENTRYPOINT [ "/opt/SMPPSim/SMPPSim.sh", "docker-entrypoint" ]