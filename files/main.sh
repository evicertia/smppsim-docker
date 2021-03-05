#!/bin/sh
#
# Script for starting multiple instances of SMPPSim and SmppSimCatcher with supervisord.
# description: SMPPSim server
CONF="/conf"

SUPERVISOR_CONF="/etc/supervisor.d"
SUPERVISOR_PATH="/usr/bin/supervisord"

SMPPSIMCATCHER_PATH="/opt/SmppSimCatcher"
SMPPSIM_HOME="/opt/SMPPSim"

[ ! -d $SUPERVISOR_CONF ] && mkdir -p $SUPERVISOR_CONF

echo "Read files of $CONF"

for file in $(find ${CONF} -name '*.props')
do \
    filename=${file%.*}
    instance=${filename##*/}

    echo "Creating ini file for instance. ${instance}"

    cat > "${SUPERVISOR_CONF}/${instance}.ini" <<-__EOF
[program:${instance}]
process_name = master
command=/opt/bin/SMPPSim.sh ${file}
startsecs=0
autorestart=true
__EOF

    if grep -q "CAPTURE_SME_DECODED=true" "${file}"; then
        SMPPSIM_SME_DECODED_FILE="$(awk '/CAPTURE_SME_DECODED_TO_FILE/{print $NF}' ${file})"
        # We choose the port number for the SMPPSim Catcher service by adding 100 to SMPPSim's SMPP port number
        SMPPSIMCATCHER_PORT="$(($(awk '/SMPP_PORT/{print $NF}' ${file}) + 100))"

        cat > "${SUPERVISOR_CONF}/${instance}.catcher.ini" <<-__EOF
[program:${instance}.catcher]
process_name = master
command=dotnet ${SMPPSIMCATCHER_PATH}/SmppSimCatcher.dll --file ${SMPPSIM_HOME}/${SMPPSIM_SME_DECODED_FILE} --urls "http://0.0.0.0:${SMPPSIMCATCHER_PORT}"
startsecs=0
autorestart=true
__EOF
    fi
done

exec ${SUPERVISOR_PATH} -n -e debug -c /etc/supervisord.conf

# vim: ai ts=2 sw=2 et sts=2 ft=sh