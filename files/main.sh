#!/bin/sh
#
# Script for finding multiple instances of SMPPSim with supervisord.
# description: SMPPSim server

SUPERVISOR_CONF="/etc/supervisor.d"
SUPERVISOR_PATH="/usr/bin/supervisord"
CONF="/conf"

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
command=/opt/bin/SMPPSim.sh ${file}
startsecs=0
autorestart=true
__EOF

done

exec ${SUPERVISOR_PATH} -n -e debug -c /etc/supervisord.conf

# vim: ai ts=2 sw=2 et sts=2 ft=sh