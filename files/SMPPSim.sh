#!/bin/bash
#
# Script for finding and starting multiple instances of SMPPSim with supervisord.
# description: SMPPSim server

base=${0##*/}
SMPPSIM_HOME=/opt/SMPPSim
NAME=default
JAR="${SMPPSIM_HOME}/smppsim.jar"
CONF="/opt/SMPPSim/conf"
LOG="/var/log/smppsim.log"
LOCK="/var/lock/subsys/smppsim"
JAVA="/opt/jdk/bin/java"
MAIN=com.seleniumsoftware.SMPPSim.SMPPSim
JAVA_ARGS="-Djava.net.preferIPv4Stack=true -Djava.util.logging.config.file=conf/logging.properties"
SUPERVISOR_CONF="/etc/supervisor.d"
SUPERVISOR_PATH="/usr/bin/supervisord"
RETVAL=0

export SMPPSIM_HOME

# Source function library.
. /etc/rc.d/init.d/functions


declare_instances() {

    [ ! -d $SUPERVISOR_CONF ] && mkdir -p $SUPERVISOR_CONF

    echo "Read files of $CONF"

    for file in $(find ${CONF} -name '*.props')
    do \
        filename=${file%.*}
        instance=${filename##*/}

        echo "Creating ini file for instance. $instance"

        cat > "${SUPERVISOR_CONF}/${instance}.ini" <<-__EOF
[program:${instance}]
process_name = master
command=${SMPPSIM_HOME}/`basename "$0"` start ${instance}
startsecs=0
autorestart=false
__EOF

    done
}

start() {

    local instance=$1
    local _CONF="${CONF}/${instance}.props"
    local _LOG="/var/log/${instance}.log"
    local _LOCK="${LOCK}.${instance}"

    [[ "$instance" =~ "Undeliverable" ]] \
        && _CLASSPATH="${SMPPSIM_HOME}/smppsim-extras-undeliverable.jar" \
        || _CLASSPATH="${SMPPSIM_HOME}/smppsim-extras.jar"

    [ -z "${_LOG}" ] && _LOG="/var/log/${instance}.log" 
    [ -e "${LOG}" ] && cnt=`wc -l "${LOG}" | awk '{ print $1 }'` || cnt=1

    echo -n $"Starting SMPPSim (Instance: ${instance}): "

    cd "$SMPPSIM_HOME"
    nohup ${JAVA} ${JAVA_ARGS} -cp "$JAR:${_CLASSPATH}" "${MAIN}" "${_CONF}" >> "${_LOG}" 2>&1 & \
    echo $! > /var/run/${base}.${instance:-0}.pid

    while { pgrep -f "java.* -cp ${JAR}.*${instance}"} > /dev/null ; } &&
        ! { tail -n +$cnt "${LOG}" | grep -q 'Starting DelayedInboundQueue service' ; } ; do
        sleep 0.5
    done

    pgrep -f "java.* -cp ${JAR}.*${instance}" > /dev/null

    RETVAL=$?
    [ $RETVAL = 0 ] && success $"$STRING" || failure $"$STRING"
    echo

    [ $RETVAL = 0 ] && touch "${_LOCK}"
}

main ()
{
    case "$1" in
        start)
            start $2
            ;;
        docker-entrypoint)
            declare_instances
            ${SUPERVISOR_PATH} -n -e debug -c /etc/supervisord.conf
            ;;
    esac
}

main "$@"

# vim: ai ts=2 sw=2 et sts=2 ft=sh