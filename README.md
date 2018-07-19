# TP-Link Kasa HS110 Smart Plug Monitoring Daemon

This is a small Daemon designed to be run on Linux using Mono. It will monitor the power usage of TP-Link Kasa HS110 smart plugs by polling it regularly.

If the current drawn is larger than a threshold, it will output a message to Syslog. Once the current drawn is below the threshold for a certain amount of time, it will output a different message to Syslog.

Using your Syslog daemon, e.g. rsyslog, it is then possible to trigger shell scripts that can do all sorts of things, the most useful of which would probably be sending an email or SMS.

These smart plugs use a custom protocol on port 9999 that is "encrypted" using XOR. Included is code to encrypt and decrypt messages, as well as communicating with the smart plug.

Configuration
-------------

The daemon is configured with an INI file that looks like this:

```
[whitegoodsd]
appliances=APPLIANCE1,APPLIANCE2
monitor_interval=60
monitor_interval_nothing_running=900
log_debug=1

[APPLIANCE1]
smartplug=10.0.0.1
threshold_running_mw=4000
minsamples=3

[APPLIANCE2]
smartplug=10.0.0.2
threshold_running_mw=4000
minsamples=3
```

Add the smart plugs to monitor to the `appliances` setting in the `[whitegoodsd]`. Separate multiple smart plugs using commas. `monitor_interval` defines how often to poll (in seconds) when at least one appliance is running and `monitor_interval_nothing_running` defines how often to poll when no appliances are running.

Add an INI section for each appliance using the exact name as specified in the `appliances` setting and then put the IP address of the smart plug in the `smartplug` setting. Use `threshold_running_mw` to define the minimum current that needs to be drawn for the appliance connected to the smart plug to be considered running. The `minsamples` specifies how many consecutive polls need to have a current draw result over the threshold for the daemon to mark the appliance connected to the smart plug as running.


Monitoring
----------

Use your Syslog daemon for monitoring and executing scripts. It is quite simple with rsyslog, for example:

```
:msg, contains, "APPLIANCE1 is FINISHED" ^/opt/whitegoodsd/appliance1.sh
:msg, contains, "APPLIANCE2 is FINISHED" ^/opt/whitegoodsd/appliance2.sh
```

This will run a shell script when the current draw is below the threshold.

To trigger a shell script when the current draw is above the threshold, just replace `is FINISHED` with `is RUNNING`. It's very simple.

For example shell scripts, see the "sample" folder in this repository.
