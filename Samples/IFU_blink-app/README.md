# Deployment test app

This app can be used to visually test the update workflow.

Adjust the `blinkCount` var around line 26 to the number of blinks matching the version build number. For example, when setting `AssemblyVersion` to 1.0.3, change it to 3 so it blinks 3 times. For version 1.0.2 make 2 and so on.

Note: if needed, change/add a new GPIO configuration to open the LED pin. Around line 19.

`Main` carries `[DeploymentConfirmationTimeout(60)]`. To see the failsafe at work, comment out the
`ConfirmDeploymentImage()` call: about 60 seconds after booting the new version the device resets, and the
previous version (with its blink count) comes back. See
[Confirmation timeout](../../docs/confirm-and-revert.md#confirmation-timeout).
