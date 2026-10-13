# Confirm, revert and reboot

Staging an image only schedules a swap. What happens next - the reboot, the test run and the
decision to keep or reject the new image - is driven by the methods described here.

## `RequestReboot`

```csharp
UpdateManager.RequestReboot();
```

Reboots the device. Any swap that has been scheduled (test, permanent or revert) is applied by
MCUboot during the reboot. Every open update session is released.

Call it right after a successful `CompleteUpdateSession`, or defer it to a convenient moment (end
of a work cycle, maintenance window, user acknowledgement). The pending swap survives until the next
reboot, whatever causes it.

## The deployment test cycle

A completed update session marks the new deployment image for a **test** swap:

1. On reboot MCUboot swaps the new image into the primary slot; `GetStatus(ImageType.Deployment)`
   returns `UpdateStatus.Testing`.
2. The new application runs and checks that it actually works.
3. It then either
   - calls `ConfirmDeploymentImage()` - the image becomes permanent and the status returns to
     `Confirmed`; or
   - does nothing (crashes, hangs, never gets that far) or calls `RequestDeploymentRevert()`
     - on the next reboot the previous image is restored.

This makes a bad update self-healing: an image that cannot get far enough to confirm itself is
rolled back automatically. To make sure that reboot actually happens when the new application
hangs, add a [confirmation timeout](#confirmation-timeout).

### `ConfirmDeploymentImage`

```csharp
bool confirmed = UpdateManager.ConfirmDeploymentImage();
```

Makes the running deployment image permanent. If it is not called while the deployment is in the
`Testing` state, the previous image is restored on the next reboot. Returns `true` if the image
was confirmed.

### `RequestDeploymentRevert`

```csharp
bool scheduled = UpdateManager.RequestDeploymentRevert();
UpdateManager.RequestReboot();
```

Explicitly rejects the new image: the previous deployment image is restored on the next reboot.
Valid only while the deployment image is running in the unconfirmed `Testing` state. Returns `true`
if the revert was scheduled.

Not confirming has the same end result, but an explicit revert followed by a reboot rolls back
straight away instead of waiting for whatever triggers the next reboot.

### Confirm only after a health check

The simplest application confirms unconditionally as the first thing in `Main` - the
[IFU-blink-app sample](../Samples/IFU-blink-app/Program.cs) does that. It proves the image boots,
but not that it works. A more robust pattern checks what matters to the device before confirming:

```csharp
public static void Main()
{
    if (UpdateManager.GetStatus(ImageType.Deployment) == UpdateStatus.Testing)
    {
        if (SelfTest())
        {
            // peripherals initialised, network reachable, update server reachable...
            UpdateManager.ConfirmDeploymentImage();
        }
        else
        {
            UpdateManager.RequestDeploymentRevert();
            UpdateManager.RequestReboot();
        }
    }

    // normal application start
}
```

Things worth checking in `SelfTest`:

- The peripherals and configuration the application depends on initialise.
- The device can still reach the update provider - otherwise a bad image could never be replaced
  by a fixed one.
- Anything else that would leave the device useless in the field.

Keep it bounded in time: until the image is confirmed, any reset brings the previous image back,
so a self-test that never completes means the update does not stick.

## Confirmation timeout

"On the next reboot" needs something to cause that reboot. A new application that hangs,
deadlocks or waits forever never confirms itself, and it never reboots either. The device stays
stuck on the broken image until someone power-cycles it. On a device in the field that can be a
long wait.

Put `DeploymentConfirmationTimeout` on `Main` to close that gap:

```csharp
using nanoFramework.Runtime.InFieldUpdate;

public class Program
{
    // this deployment has 2 minutes to confirm itself, otherwise it is reverted
    [DeploymentConfirmationTimeout(120)]
    public static void Main()
    {
        if (UpdateManager.GetStatus(ImageType.Deployment) == UpdateStatus.Testing && SelfTest())
        {
            UpdateManager.ConfirmDeploymentImage();
        }

        // normal application start
    }
}
```

When the deployment is running as an unconfirmed test image, the nanoCLR starts a countdown just
before `Main` runs (static constructors are included). If `ConfirmDeploymentImage()` isn't called
in time, the device is reset and MCUboot restores the previous deployment.

- **Units**: seconds. `0`, or no attribute at all, leaves the countdown off.
- **Only when it matters**: the countdown is only armed when the deployment is unconfirmed. Once
  the image is confirmed, the attribute has no effect, so it is safe to leave in place.
- **Per deployment**: the value travels with the application. Each deployment can choose its
  own, without a firmware change.
- **Once per power cycle**: the countdown starts once. A CLR-only reboot neither restarts nor
  extends it.
- **What stops it**:
  - a successful `ConfirmDeploymentImage()`;
  - a confirm sent by a tool over the Wire Protocol;
  - the start of a debugger session (source-level debugging), so a developer stepping through
    code isn't reset in the middle of it.

  A failed confirm keeps the countdown running. When it is cancelled by a debugger, the image is
  still unconfirmed, so it is reverted on the next reboot unless it gets confirmed.
- **A deployment that can't start**: this case doesn't need the attribute. If an unconfirmed
  deployment fails to load (corrupted assembly, mismatch with the firmware's native code) or has
  no entry point, the nanoCLR reboots straight away so it is reverted.
- **Platform support**: implemented for ChibiOS targets with MCUboot. On other platforms the
  attribute is ignored.

### Choosing a value

The timeout has to cover the worst-case time from power-on to the `ConfirmDeploymentImage()` call:

- static constructors;
- the self-test, including waiting for the network to come up and the update server to answer;
- any retries the self-test makes.

Leave a generous margin. A timeout that is too short reverts a perfectly good update on a slow day.
That is recoverable, because the update can be retried, but it is still wasted effort. Values in
minutes are typical for applications that check connectivity. A few tens of seconds is enough for
one that only checks local peripherals.

## nanoCLR image

The nanoCLR image behaves differently: **the running nanoCLR is already confirmed at startup**, so
there is no test cycle to confirm and `RequestDeploymentRevert` does not apply to it.

### `RequestClrRevert`

```csharp
bool scheduled = UpdateManager.RequestClrRevert();
UpdateManager.RequestReboot();
```

Schedules a swap of the image currently in the nanoCLR **secondary** slot on the next reboot. It
cannot "un-confirm" the running firmware; it can only swap in a valid image that is already present
there - for example a copy of the previous firmware kept in that slot. Returns `false` if the
nanoCLR secondary slot does not hold a valid image to revert to. A reboot is required to complete
the swap.

Check what is there first with `GetSecondaryImageInfo(ImageType.NanoClr)` (see
[Querying images](querying-images.md)).
