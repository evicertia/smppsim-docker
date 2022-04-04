#!/bin/sh
#
# Script for starting multiple instances of SMPPSim with supervisord.
# description: SMPPSim server

base=${0##*/}
SMPPSIM_HOME=/opt/SMPPSim
NAME=default
JAR="${SMPPSIM_HOME}/smppsim.jar"
LOCK="/var/lock/subsys/smppsim"
JAVA="/usr/bin/java"
MAIN=com.seleniumsoftware.SMPPSim.SMPPSim
JAVA_ARGS="-Djava.net.preferIPv4Stack=true -Djava.util.logging.config.file=/conf/logging.properties"
LIBS=

function die ()
{
  rc=$1
  message=$2
  [ -z "$message" ] && message="Died"
  echo "${BASH_SOURCE[1]}:${BASH_LINENO[0]} (${FUNCNAME[1]}): $message" >&2
  exit $rc
}

[ -n "${1:-}" ] || die 1 "Missing settings file argument."

# Reset message-id to avoid duplicates in db when restarting the service
# See 26530
sed -i -e "s|START_MESSAGE_ID_AT=.*|START_MESSAGE_ID_AT=$(date +%s)|" $1

instance=$(basename $1 .props)
_CONF=$1
_LOG="/var/log/${instance}.log"
_LOCK="${LOCK}.${instance}"

echo -n $"Starting SMPPSim (Instance: ${instance}): "

[ -d /libs -a -n "$(ls /libs)" ]&& LIBS=$(printf %s: /libs/*.jar)

cd "$SMPPSIM_HOME"
exec ${JAVA} ${JAVA_ARGS} -cp "$JAR:${LIBS:-}" "${MAIN}" "${_CONF}" >> "${_LOG}" 2>&1

# vim: ai ts=2 sw=2 et sts=2 ft=sh