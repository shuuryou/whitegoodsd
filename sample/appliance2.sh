#!/bin/bash
export PATH='/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin'

LOCKFILE=/tmp/appliance2.lock
if [ -e "$LOCKFILE" ] && kill -0 "$(cat $LOCKFILE)"; then
    exit 1
fi
trap "rm -f $LOCKFILE; exit" INT TERM EXIT
echo $$ > "$LOCKFILE"

CURRENTTIME=$(date +%H:%M)
if [[ "$CURRENTTIME" > "23:00" ]] || [[ "$CURRENTTIME" < "07:00" ]]; then
	echo "Exiting because $CURRENTTIME is out of acceptable range." | logger -t "whitegoodsd-appliance2"
	exit 0
fi

### Your custom commands when your appliance is done.

exit 0
